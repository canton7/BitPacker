using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace BitPacker
{
    internal class BitPackerExpressionBuilder
    {
        private Expression writer;

        public BitPackerExpressionBuilder(Expression writer)
        {
            this.writer = writer;
        }

        private IEnumerable<PropertyDetails> DiscoverProperties(Type objectType, Endianness defaultEndianness)
        {
            var properties = from property in objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             let attribute = property.GetCustomAttribute<BitPackerMemberAttribute>(false)
                             where attribute != null
                             orderby attribute.Order
                             select new PropertyDetails(objectType, property, attribute, defaultEndianness);
            return properties;
        }

        private Expression HandleVariableLengthArrays(Expression subject, IEnumerable<PropertyDetails> propertyDetails)
        {
            // For each property which is a VLA, ynthesize an expression to assign said length field
            // When serializing, we can handle arrays which have neither Length nor LengthField properties - we just serialize that number of elements

            var blockMembers = propertyDetails.Where(x => x.IsEnumable && x.LengthProperty != null).Select(property =>
            {
                return Expression.Assign(
                    Expression.MakeMemberAccess(subject, property.LengthProperty),
                    ExpressionHelpers.LengthOfEnumerable(
                        Expression.MakeMemberAccess(subject, property.PropertyInfo),
                        property.ElementType
                    )
                );
            });

            return blockMembers.Any() ? Expression.Block(blockMembers) : null;
        }

        private TypeDetails SerializeProperty(Expression subject, PropertyDetails property)
        {
            Expression member = Expression.MakeMemberAccess(subject, property.PropertyInfo);
            return this.SerializeValue(member, property);
        }

        private TypeDetails SerializeValue(Expression value, PropertyDetails property)
        {
            if (property.IsEnumable)
            {
                return this.SerializeEnumerable(value, property);
            }
            else
            {
                return this.SerializeScalarValue(value, property.Type, property.Endianness);
            }
        }

        private TypeDetails SerializeScalarValue(Expression value, Type type, Endianness endianness)
        {
            if (PrimitiveTypes.Types.ContainsKey(type))
            {
                return this.SerializePrimitive(value, type, endianness);
            }

            var attribute = type.GetCustomAttribute<BitPackerObjectAttribute>(false);
            if (attribute != null)
            {
                return this.SerializeCustomType(value, type);
            }

            throw new Exception(String.Format("Don't know how to serialize type {0}", type.Name));
        }

        public TypeDetails SerializeCustomType(Expression value, Type type)
        {
            // If it's not marked with our attribute, we're not serializing it
            var attribute = type.GetCustomAttribute<BitPackerObjectAttribute>(false);
            if (attribute == null)
                return null;

            var properties = this.DiscoverProperties(type, attribute.Endianness);
            var blockMembers = new List<Expression>() { this.HandleVariableLengthArrays(value, properties) };

            var typeDetails = this.DiscoverProperties(type, attribute.Endianness).Select(property => this.SerializeProperty(value, property));

            blockMembers.AddRange(typeDetails.Select(x => x.OperationExpression));

            var block = Expression.Block(blockMembers.Where(x => x != null));
            var nullCheck = Expression.IfThen(Expression.NotEqual(value, Expression.Constant(null, type)), block);

            return new TypeDetails(typeDetails.All(x => x.HasFixedSize), typeDetails.Sum(x => x.MinSize), nullCheck);
        }

        private TypeDetails SerializePrimitive(Expression value, Type type, Endianness endianness)
        {
            if (endianness != EndianUtilities.HostEndianness)
            {
                // If EndianUtilities has a Swap method for this type, then we can convert it
                var swapMethod = typeof(EndianUtilities).GetMethod("Swap", new[] { type });
                if (swapMethod != null)
                {
                    value = Expression.Call(swapMethod, value);
                }
            }

            var info = PrimitiveTypes.Types[type];

            return new TypeDetails(true, info.Size, info.SerializeExpression(this.writer, value));
        }

        private TypeDetails SerializeEnumerable(Expression enumerable, PropertyDetails property)
        {
            var blockMembers = new List<Expression>();
            var blockVars = new List<ParameterExpression>();

            ParameterExpression lengthVar = null;
            bool hasFixedLength = property.EnumerableLength > 0;

            // If they specified an explicit length, throw if the actual enumerable is longer
            if (hasFixedLength)
            {
                lengthVar = Expression.Variable(typeof(int), "length");
                blockVars.Add(lengthVar);

                var enumerableLength = ExpressionHelpers.LengthOfEnumerable(enumerable, property.ElementType);
                blockMembers.Add(Expression.Assign(lengthVar, enumerableLength));

                var test = Expression.GreaterThan(lengthVar, Expression.Constant(property.EnumerableLength));
                var throwExpr = Expression.Throw(Expression.Constant(new Exception("You specified an explicit length for an array member, but the actual member is longer")));
                blockMembers.Add(Expression.IfThen(test, throwExpr));
            }

            // If they specified a length field, we've already assigned it (yay how organised as we?!)

            var loopVar = Expression.Variable(property.ElementType, "loopVariable");
            var typeDetails =  this.SerializeScalarValue(loopVar, property.ElementType, property.Endianness);
            blockMembers.Add(ExpressionHelpers.ForEach(enumerable, property.ElementType, loopVar, typeDetails.OperationExpression));

            // If it's a fixed-length array, we might need to pad it out
            // if (lengthVar < property.EnumerableLength)
            // {
            //     var emptyInstance = new SomeType(); // Or whatever
            //     for (int i = lengthVar; i < property.EnumerableLength; i++)
            //     {
            //         writer.Write(emptyInstance); // Or whatever the writing expression happens to be
            //     }
            // }
            if (hasFixedLength)
            {
                var emptyInstanceVar = Expression.Variable(property.ElementType, "emptyInstance");
                blockVars.Add(emptyInstanceVar);
                var emptyInstanceAssignment = Expression.Assign(emptyInstanceVar, Expression.New(property.ElementType));

                var initAndSerialize = this.SerializeScalarValue(emptyInstanceVar, property.ElementType, property.Endianness).OperationExpression;
                var i = Expression.Variable(typeof(int), "i");

                var padding = Expression.IfThen(
                    Expression.LessThan(lengthVar, Expression.Constant(property.EnumerableLength)),
                    Expression.Block(new[] { emptyInstanceVar },
                        emptyInstanceAssignment,
                        ExpressionHelpers.For(
                            i,
                            lengthVar,
                            Expression.LessThan(i, Expression.Constant(property.EnumerableLength)),
                            Expression.PostIncrementAssign(i),
                            initAndSerialize
                        )
                    )
                );

                blockMembers.Add(padding);
            }

            var block = Expression.Block(blockVars, blockMembers);

            return new TypeDetails(hasFixedLength && typeDetails.HasFixedSize, hasFixedLength ? property.EnumerableLength * typeDetails.MinSize : 0, block);
        }
    }
}
