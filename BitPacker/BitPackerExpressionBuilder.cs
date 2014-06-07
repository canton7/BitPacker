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
        private static readonly HashSet<Type> primitiveTypes = new HashSet<Type>()
        {
            typeof(byte), typeof(sbyte), typeof(char), typeof(double), typeof(decimal), typeof(short),
            typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float)
        };

        public BitPackerExpressionBuilder(Expression writer)
        {
            this.writer = writer;
        }

        private IEnumerable<PropertyDetails> DiscoverProperties(Type objectType)
        {
            var properties = from property in objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             let attribute = property.GetCustomAttribute<BitPackerMemberAttribute>(false)
                             where attribute != null
                             orderby attribute.Order
                             select new PropertyDetails(property, attribute);
            return properties;
        }

        private Expression SerializeProperty(Expression subject, PropertyDetails property, Endianness defaultEndianness)
        {
            Expression member = Expression.MakeMemberAccess(subject, property.PropertyInfo);
            return this.SerializeValue(member, property.PropertyType, property.Attribute.NullableEndianness ?? defaultEndianness);
        }

        private Expression SerializeValue(Expression value, Type type, Endianness endianness)
        {
            if (primitiveTypes.Contains(type))
            {
                return this.SerializePrimitive(value, type, endianness);
            }
            
            if (type.IsArray || typeof(IEnumerable<>).IsAssignableFrom(type))
            {
                var elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                // TODO: Array length
                return this.SerializeEnumerable(value, elementType, 0, endianness);
            }

            var attribute = type.GetCustomAttribute<BitPackerObjectAttribute>(false);
            if (attribute != null)
            {
                return this.SerializeCustomType(value, type);
            }

            return Expression.Empty();
        }

        public Expression SerializeCustomType(Expression value, Type type)
        {
            // If it's not marked with our attribute, we're not serializing it
            var attribute = type.GetCustomAttribute<BitPackerObjectAttribute>(false);
            if (attribute == null)
                return Expression.Empty();

            var blockMembers = this.DiscoverProperties(type).Select(property =>
            {
                return this.SerializeProperty(value, property, attribute.Endianness);
            });

            if (!blockMembers.Any())
                return Expression.Empty();

            var block = Expression.Block(blockMembers);
            var nullCheck = Expression.IfThen(Expression.NotEqual(value, Expression.Constant(null, type)), block);

            return nullCheck;
        }

        private Expression SerializePrimitive(Expression value, Type type, Endianness endianness)
        {
            if (endianness != EndianUtilities.HostEndianness)
            {
                // If EndianUtilities has a Swap method for this type, then we can convert it
                var swapMethod = typeof(EndianUtilities).GetMethod("Swap", new[] { type });
                // If swapMethod doesn't exist, and they're explictely stated that they want an endianness for this property, then that's an error
                if (swapMethod != null)
                {
                    value = Expression.Call(swapMethod, value);
                }
            }

            var method = typeof(BinaryWriter).GetMethod("Write", new[] { type });
            Debug.Assert(method != null);

            return Expression.Call(this.writer, method, value);
        }

        private Expression SerializeEnumerable(Expression enumerable, Type elementType, int length, Endianness endianness)
        {
            var blockMembers = new List<Expression>();

            var lengthVar = Expression.Variable(typeof(int), "length");
            var enumerableLength = ExpressionHelpers.LengthOfEnumerable(enumerable, elementType);
            blockMembers.Add(Expression.Assign(lengthVar, enumerableLength));

            // If they specified an explicit length, throw if the actual enumerable is longer
            if (length > 0)
            {
                var test = Expression.GreaterThan(lengthVar, Expression.Constant(length));
                var throwExpr = Expression.Throw(Expression.Constant(new Exception("You specified an explicit length for an array member, but the actual member is longer")));
                blockMembers.Add(Expression.IfThen(test, throwExpr));
            }

            var loopVar = Expression.Variable(elementType, "loopVariable");
            blockMembers.Add(ExpressionHelpers.ForEach(enumerable, elementType, loopVar, this.SerializeValue(loopVar, elementType, endianness)));

            var block = Expression.Block(new[] { lengthVar }, blockMembers);

            return block;
        }
    }
}
