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
    internal class SerializerExpressionBuilder
    {
        private readonly Expression writer;
        private readonly Type objectType;

        public SerializerExpressionBuilder(Expression writer, Type objectType)
        {
            this.writer = writer;
            this.objectType = objectType;
        }

        public TypeDetails Serialize(Expression subject)
        {
            var objectDetails = new ObjectDetails(this.objectType, subject, new BitPackerMemberAttribute());
            objectDetails.Discover();

            var blockMembers = new List<Expression>();

            blockMembers.Add(this.HandleVariableLengthArrays(subject, objectDetails));

            var serialized = this.SerializeCustomType(subject, objectDetails);
            blockMembers.Add(serialized.OperationExpression);

            return new TypeDetails(serialized.HasFixedSize, serialized.MinSize, Expression.Block(blockMembers.Where(x => x != null)));
        }

        private Expression HandleVariableLengthArrays(Expression subject, ObjectDetails objectDetails)
        {
            // Find all properties which share the same LengthKey
            var groups = objectDetails.RecursiveFlatProperties().GroupBy(x => x.LengthKey).ToArray();

            // For each, synthesize an assign to the integral field, assigning the length of the array field
            var blockMembers = groups.Where(x => x.Key != null).Select(group =>
            {
                var arrays = group.Where(x => x.IsEnumerable).ToArray();
                var keys = group.Where(x => PrimitiveTypes.IsPrimitive(x.Type) && PrimitiveTypes.Types[x.Type].IsIntegral).ToArray();

                if (arrays.Length != 1)
                    throw new Exception(String.Format("Found zero, or more than one arrays fields for Length Key {0}", group.Key));

                if (keys.Length != 1)
                    throw new Exception(String.Format("Found zero, or more than one integral fields for Length Key {0}", group.Key));

                return Expression.Assign(
                    keys[0].Value,
                    ExpressionHelpers.LengthOfEnumerable(
                        arrays[0].Value,
                        arrays[0].ElementType
                    )
                );
            });

            return blockMembers.Any() ? Expression.Block(blockMembers) : null;
        }

        private TypeDetails SerializeValue(Expression value, ObjectDetails objectDetails)
        {
            if (objectDetails.IsEnumerable)
                return this.SerializeEnumerable(value, objectDetails);

            if (PrimitiveTypes.Types.ContainsKey(objectDetails.Type))
                return this.SerializePrimitive(value, objectDetails);

            if (objectDetails.IsEnum)
                return this.SerializeEnum(value, objectDetails);

            if (objectDetails.IsCustomType)
                return this.SerializeCustomType(value, objectDetails);

            throw new Exception(String.Format("Don't know how to serialize type {0}", objectDetails.Type.Name));
        }

        public TypeDetails SerializeCustomType(Expression value, ObjectDetails objectDetails)
        {
            // If it's not marked with our attribute, we're not serializing it
            if (!objectDetails.IsCustomType)
                return null;

            Expression result;
            var typeDetails = objectDetails.Properties.Select(property => this.SerializeValue(property.AccessExpression(value), property)).ToArray();

            // If they claim to be able to serialize themselves, let them
            if (typeof(ISerialize).IsAssignableFrom(objectDetails.Type))
            {
                var method = typeof(ISerialize).GetMethod("Serialize");
                result = Expression.Call(value, method, this.writer);
            }
            else
            {
                var blockMembers = typeDetails.Select(x => x.OperationExpression);

                result = Expression.Block(blockMembers.Where(x => x != null));
            }

            return new TypeDetails(typeDetails.All(x => x.HasFixedSize), typeDetails.Sum(x => x.MinSize), result);
        }

        private TypeDetails SerializePrimitive(Expression value, ObjectDetails objectDetails)
        {
            // Even through EndiannessUtilities has now Swap(byte) overload, we get an AmbiguousMatchException
            // when we try and find such a method (maybe the byte is being coerced into an int or something?).
            // Therefore, handle this...

            var info = PrimitiveTypes.Types[objectDetails.Type];
            if (objectDetails.Endianness != EndianUtilities.HostEndianness && info.Size > 1)
            {
                // If EndianUtilities has a Swap method for this type, then we can convert it
                var swapMethod = typeof(EndianUtilities).GetMethod("Swap", new[] { objectDetails.Type } );
                if (swapMethod != null)
                    value = Expression.Call(swapMethod, value);
            }

            return new TypeDetails(true, info.Size, info.SerializeExpression(this.writer, value));
        }

        public TypeDetails SerializeEnum(Expression value, ObjectDetails objectDetails)
        {
            return this.SerializePrimitive(Expression.Convert(value, objectDetails.EnumEquivalentType), objectDetails.EnumEquivalentObjectDetails);
        }

        private TypeDetails SerializeEnumerable(Expression enumerable, ObjectDetails objectDetails)
        {
            var blockMembers = new List<Expression>();
            var blockVars = new List<ParameterExpression>();

            ParameterExpression lengthVar = null;
            bool hasFixedLength = objectDetails.EnumerableLength > 0;

            // If they specified an explicit length, throw if the actual enumerable is longer
            if (hasFixedLength)
            {
                lengthVar = Expression.Variable(typeof(int), "length");
                blockVars.Add(lengthVar);

                var enumerableLength = ExpressionHelpers.LengthOfEnumerable(enumerable, objectDetails.ElementType);
                blockMembers.Add(Expression.Assign(lengthVar, enumerableLength));

                var test = Expression.GreaterThan(lengthVar, Expression.Constant(objectDetails.EnumerableLength));
                var throwExpr = Expression.Throw(Expression.Constant(new Exception("You specified an explicit length for an array member, but the actual member is longer")));
                blockMembers.Add(Expression.IfThen(test, throwExpr));
            }

            // If they specified a length field, we've already assigned it (yay how organised as we?!)

            var loopVar = Expression.Variable(objectDetails.ElementType, "loopVariable");
            var typeDetails = this.SerializeValue(loopVar, objectDetails.ElementObjectDetails);
            blockMembers.Add(ExpressionHelpers.ForEach(enumerable, objectDetails.ElementType, loopVar, typeDetails.OperationExpression));

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
                var emptyInstanceVar = Expression.Variable(objectDetails.ElementType, "emptyInstance");
                blockVars.Add(emptyInstanceVar);
                var emptyInstanceAssignment = Expression.Assign(emptyInstanceVar, Expression.New(objectDetails.ElementType));

                var initAndSerialize = this.SerializeValue(emptyInstanceVar, objectDetails.ElementObjectDetails).OperationExpression;
                var i = Expression.Variable(typeof(int), "i");

                var padding = Expression.IfThen(
                    Expression.LessThan(lengthVar, Expression.Constant(objectDetails.EnumerableLength)),
                    Expression.Block(new[] { emptyInstanceVar },
                        emptyInstanceAssignment,
                        ExpressionHelpers.For(
                            i,
                            lengthVar,
                            Expression.LessThan(i, Expression.Constant(objectDetails.EnumerableLength)),
                            Expression.PostIncrementAssign(i),
                            initAndSerialize
                        )
                    )
                );

                blockMembers.Add(padding);
            }

            var block = Expression.Block(blockVars, blockMembers);

            return new TypeDetails(hasFixedLength && typeDetails.HasFixedSize, hasFixedLength ? objectDetails.EnumerableLength * typeDetails.MinSize : 0, block);
        }
    }
}
