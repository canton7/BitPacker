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
                return this.SerializeScalarValue(value, property.Type, property);
            }
        }

        private TypeDetails SerializeScalarValue(Expression value, Type type, PropertyAttributes property)
        {
            if (PrimitiveTypes.Types.ContainsKey(type))
            {
                return this.SerializePrimitive(value, type, property);
            }

            if (typeof(Enum).IsAssignableFrom(type))
            {
                return this.SerializeEnum(value, type, property);
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

            var blockMembers = new List<Expression>();

            var valueToSerialize = Expression.Variable(type, "valueToSerialize");
            blockMembers.Add(Expression.IfThenElse(
                Expression.Equal(value, Expression.Constant(null, type)),
                Expression.Assign(valueToSerialize, Expression.New(type)),
                Expression.Assign(valueToSerialize, value)
            ));

            var properties = this.DiscoverProperties(type, attribute.Endianness).ToArray();
            blockMembers.Add(this.HandleVariableLengthArrays(valueToSerialize, properties));

            var typeDetails = properties.Select(property => this.SerializeProperty(valueToSerialize, property)).ToArray();

            blockMembers.AddRange(typeDetails.Select(x => x.OperationExpression));

            var block = Expression.Block(new[] { valueToSerialize }, blockMembers.Where(x => x != null));

            return new TypeDetails(typeDetails.All(x => x.HasFixedSize), typeDetails.Sum(x => x.MinSize), block);
        }

        private TypeDetails SerializePrimitive(Expression value, Type type, PropertyAttributes property)
        {
            // Even through EndiannessUtilities has now Swap(byte) overload, we get an AmbiguousMatchException
            // when we try and find such a method (maybe the byte is being coerced into an int or something?).
            // Therefore, handle this...

            var info = PrimitiveTypes.Types[type];

            if (property.Endianness != EndianUtilities.HostEndianness && info.Size > 1)
            {
                // If EndianUtilities has a Swap method for this type, then we can convert it
                var swapMethod = typeof(EndianUtilities).GetMethod("Swap", new[] { type } );
                if (swapMethod != null)
                    value = Expression.Call(swapMethod, value);
            }

            return new TypeDetails(true, info.Size, info.SerializeExpression(this.writer, value));
        }

        private TypeDetails SerializeEnum(Expression value, Type type, PropertyAttributes property)
        {
            Type intType = property.EnumType == null ? typeof(int) : property.EnumType;
            // Check that no value in the enum exceeds the given size
            var length = PrimitiveTypes.Types[intType].Size;
            var maxVal = Math.Pow(2, length * 8);
            // Can't use linq, as it's an non-generic IEnumerable of value types
            foreach (var enumVal in Enum.GetValues(type))
            {
                if ((int)enumVal > maxVal)
                    throw new Exception(String.Format("Enum type {0} has a size of {1} bytes, but has a member which is greater than this", type, length));
            }
                

            return this.SerializePrimitive(Expression.ConvertChecked(value, intType), intType, property);
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
            var typeDetails =  this.SerializeScalarValue(loopVar, property.ElementType, property);
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

                var initAndSerialize = this.SerializeScalarValue(emptyInstanceVar, property.ElementType, property).OperationExpression;
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
