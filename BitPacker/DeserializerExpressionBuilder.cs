using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class DeserializerExpressionBuilder
    {
        private readonly ParameterExpression reader;
        private readonly Type objectType;
        private Dictionary<Type, object> deserializerCache = new Dictionary<Type, object>();

        public DeserializerExpressionBuilder(ParameterExpression reader, Type objectType)
        {
            this.reader = reader;
            this.objectType = objectType;
        }

        public TypeDetails Deserialize()
        {
            var subject = Expression.Parameter(this.objectType, "rootSubject");

            var objectDetails = new ObjectDetails(this.objectType, new BitPackerMemberAttribute(0));
            objectDetails.Discover();

            // First, we need to make sure it's fully constructed
            var blockMembers = new List<Expression>();

            var context = new TranslationContext(objectDetails);
            var deserialized = this.DeserializeAndAssignValue(subject, context);
            blockMembers.Add(deserialized.OperationExpression);
            // Set return value
            blockMembers.Add(subject);

            var block = Expression.Block(new[] { subject }, blockMembers.Where(x => x != null));

            return new TypeDetails(deserialized.HasFixedSize, deserialized.MinSize, block);
        }

        //private Expression EnsureFullyConstructed(ObjectDetails objectDetails)
        //{
        //    var properties = objectDetails.RecursiveFlatProperties().Where(x => x.IsCustomType);
        //    var blockMembers = properties.Select(property =>
        //    {
        //        return Expression.IfThen(
        //            Expression.Equal(property.Value, Expression.Constant(null, property.Type)),
        //            Expression.Assign(property.Value, Expression.New(property.Type))
        //        );
        //    });

        //    return blockMembers.Any() ? Expression.Block(blockMembers) : null;
        //}

        private TypeDetails DeserializeAndAssignValue(Expression subject, TranslationContext context)
        {
            var typeDetails = this.DeserializeValue(context);
            var wrappedAssignment = ExpressionHelpers.TryTranslate(Expression.Assign(subject, typeDetails.OperationExpression), context.GetMemberPath());
            return new TypeDetails(typeDetails.HasFixedSize, typeDetails.MinSize, wrappedAssignment);
        }

        private TypeDetails DeserializeValue(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;
            if (objectDetails.IsString)
                return this.DeserializeString(context);

            if (objectDetails.Type == typeof(bool))
                return this.DeserializeBoolean(objectDetails);

            if (objectDetails.IsEnumerable)
                return this.DeserializeEnumerable(context);

            if (PrimitiveTypes.Types.ContainsKey(objectDetails.Type))
                return this.DeserializePrimitive(objectDetails);

            if (objectDetails.IsEnum)
                return this.DeserializeEnum(objectDetails);

            if (objectDetails.IsCustomType)
                return this.DeserializeCustomType(context);

            throw new Exception(String.Format("Don't know how to deserialize type {0}", objectDetails.Type.Name));
        }

        public TypeDetails DeserializeCustomType(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;

            // If it's not marked with our attribute, we're not deserializing it
            if (!objectDetails.IsCustomType)
                return null;

            var blockMembers = new List<Expression>();
            var subject = Expression.Variable(objectDetails.Type, objectDetails.Type.Name);

            // TODO Raise a different exception here, to show that it's the constructing that's failed
            var createAndAssign = Expression.Assign(subject, Expression.New(objectDetails.Type));
            var wrappedCreateAndAssign = ExpressionHelpers.TryTranslate(createAndAssign, context.GetMemberPath());
            blockMembers.Add(wrappedCreateAndAssign);

            var typeDetails = objectDetails.Properties.Select(property =>
            {
                var newContext = context.Push(property, subject, property.PropertyInfo.Name);

                // Does it have a custom deserializer?
                if (property.CustomDeserializer != null)
                {
                    return this.CreateAndAssignFromDeserializer(property.CustomDeserializer, property.AccessExpression(subject), newContext);
                }
                else
                {
                    return this.DeserializeAndAssignValue(property.AccessExpression(subject), newContext);
                }
            }).ToArray();

            blockMembers.AddRange(typeDetails.Select(x => x.OperationExpression));
            

            blockMembers.Add(subject); // Last value in block is the return value
            var result = Expression.Block(new[] { subject }, blockMembers.Where(x => x != null));
            return new TypeDetails(typeDetails.All(x => x.HasFixedSize), typeDetails.Sum(x => x.MinSize), result);
        }

        private TypeDetails DeserializePrimitive(ObjectDetails objectDetails)
        {
            // Even through EndiannessUtilities has now Swap(byte) overload, we get an AmbiguousMatchException
            // when we try and find such a method (maybe the byte is being coerced into an int or something?).
            // Therefore, handle this..

            var info = PrimitiveTypes.Types[objectDetails.Type];
            Expression readExpression;

            if (info.IsIntegral && objectDetails.BitWidth.HasValue)
            {
                var readMethod = typeof(BitfieldBinaryReader).GetMethod("ReadBitfield", new[] { typeof(int), typeof(int), typeof(bool) });
                var containerSize = Expression.Constant(info.Size);
                var numBits = Expression.Constant(objectDetails.BitWidth.Value);
                var swapEndianness = Expression.Constant(objectDetails.Endianness != EndianUtilities.HostEndianness);
                var readValue = Expression.Call(this.reader, readMethod, containerSize, numBits, swapEndianness);
                readExpression = Expression.Convert(readValue, objectDetails.Type);
            }
            else if (objectDetails.Endianness != EndianUtilities.HostEndianness && info.Size > 1)
            {
                // If EndianUtilities has a Swap method for this type, then we can convert it
                var swapMethod = typeof(EndianUtilities).GetMethod("Swap", new[] { objectDetails.Type });
                readExpression = info.DeserializeExpression(this.reader);
                if (swapMethod != null)
                    readExpression = Expression.Call(swapMethod, readExpression);
            }
            else
            {
                readExpression = info.DeserializeExpression(this.reader);
            }

            return new TypeDetails(true, info.Size, readExpression);
        }

        private TypeDetails DeserializeEnum(ObjectDetails objectDetails)
        {
            var typeDetails = this.DeserializePrimitive(objectDetails.EnumEquivalentObjectDetails);
            var value = Expression.Convert(typeDetails.OperationExpression, objectDetails.Type);
            return new TypeDetails(typeDetails.HasFixedSize, typeDetails.MinSize, value);
        }

        private TypeDetails DeserializeBoolean(ObjectDetails objectDetails)
        {
            var typeDetails = this.DeserializePrimitive(objectDetails.BooleanEquivalentObjectDetails);
            var value = Expression.NotEqual(typeDetails.OperationExpression, Expression.Convert(Expression.Constant(0), typeDetails.OperationExpression.Type));
            return new TypeDetails(typeDetails.HasFixedSize, typeDetails.MinSize, value);
        }

        private Expression GetArrayLength(TranslationContext context, out Expression arrayPaddingLength)
        {
            var objectDetails = context.ObjectDetails;
            Expression arrayLength;
            bool hasFixedLength = objectDetails.EnumerableLength > 0;
            arrayPaddingLength = null;

            if (objectDetails.LengthKey != null)
            {
                if (!context.TryFindLengthKey(objectDetails.LengthKey, out arrayLength))
                    throw new Exception(String.Format("Could not find integer field with Length Key {0}", objectDetails.LengthKey));

                // If it has both fixed and variable-length attributes, then there's padding at the end of it
                if (hasFixedLength)
                    arrayPaddingLength = Expression.Subtract(Expression.Constant(objectDetails.EnumerableLength), arrayLength);
            }
            else if (hasFixedLength)
            {
                arrayLength = Expression.Constant(objectDetails.EnumerableLength);
            }
            else
            {
                throw new BitPackerTranslationException(context.GetMemberPath(), new Exception("Unknown length for array"));
            }

            return arrayLength;
        }

        public TypeDetails DeserializeString(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;
            var encoding = Expression.Constant(objectDetails.Encoding);
            bool hasFixedLength = objectDetails.EnumerableLength > 0;
            var getStringMethod = typeof(Encoding).GetMethod("GetString", new[] { typeof(byte[]) });
            BlockExpression block;

            // Riiight... So. It's either got a length, or it's an ASCII null-terminated string
            // The length option is easy...
            if (hasFixedLength || objectDetails.LengthKey != null)
            {
                Expression arrayPaddingLength;
                var arrayLength = this.GetArrayLength(context, out arrayPaddingLength);
                var blockMembers = new List<Expression>();

                var readBytesMethod = typeof(BitfieldBinaryReader).GetMethod("ReadBytes", new[] { typeof(int) });
                var bytesArrayVar = Expression.Variable(typeof(byte[]), "bytes");
                blockMembers.Add(Expression.Assign(bytesArrayVar, Expression.Call(this.reader, readBytesMethod, arrayLength)));
                if (arrayPaddingLength != null)
                    blockMembers.Add(Expression.Call(this.reader, readBytesMethod, arrayPaddingLength));

                var stringRead = Expression.Call(encoding, getStringMethod, bytesArrayVar);

                // If it's ASCII, trim the NULLs
                if (objectDetails.Encoding == Encoding.ASCII)
                {
                    var trimMethod = typeof(string).GetMethod("TrimEnd", new[] { typeof(char[]) });
                    blockMembers.Add(Expression.Call(stringRead, trimMethod, Expression.NewArrayInit(typeof(char), Expression.Constant('\0'))));
                }
                else
                {
                    blockMembers.Add(stringRead);
                }

                block = Expression.Block(new[] { bytesArrayVar }, blockMembers);
            }
            else
            {
                Trace.Assert(objectDetails.Encoding == Encoding.ASCII); // ObjectDetails should have ensured this for us
                // We've no choice but to walk the thing. Thankfully we know it's ASCII
                // Once we find a null byte, that null's the last character of the string - but we don't know how long it's going to be....

                var listCtor = typeof(List<byte>).GetConstructor(new Type[0]);
                var listVar = Expression.Variable(typeof(List<byte>), "bytes");
                var listAssign = Expression.Assign(listVar, Expression.New(listCtor));

                var breakLabel = Expression.Label("LoopBreak");
                var byteVar = Expression.Variable(typeof(byte), "byte");
                var readByteMethod = typeof(BitfieldBinaryReader).GetMethod("ReadByte");
                var listAddMethod = typeof(List<byte>).GetMethod("Add", new[] { typeof(byte) });

                var loopContents = Expression.Block(new[] { byteVar },
                    Expression.Assign(byteVar, Expression.Call(this.reader, readByteMethod)),
                    Expression.IfThenElse(
                        Expression.Equal(byteVar, Expression.Constant((byte)0)),
                        Expression.Break(breakLabel),
                        Expression.Call(listVar, listAddMethod, byteVar)
                    )
                );

                var loop = Expression.Loop(loopContents, breakLabel);

                var toArrayMethod = typeof(List<byte>).GetMethod("ToArray", new Type[0]);
                var toArrayCall = Expression.Call(listVar, toArrayMethod);
                var stringRead = Expression.Call(encoding, getStringMethod, toArrayCall);

                block = Expression.Block(new[] { listVar },
                    listAssign,
                    loop,
                    stringRead
                );
            }

            return new TypeDetails(hasFixedLength, hasFixedLength ? objectDetails.EnumerableLength : 0, block);
        }

        private TypeDetails DeserializeEnumerable(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;

            bool hasFixedLength = objectDetails.EnumerableLength > 0;

            var subject = Expression.Parameter(objectDetails.Type, String.Format("enumerableOf{0}", objectDetails.ElementType.Name));
            Expression arrayInit;
            Expression arrayPaddingLength;
            var arrayLength = this.GetArrayLength(context, out arrayPaddingLength);

            arrayInit = Expression.Assign(
                subject,
                this.CreateListOrArray(objectDetails, arrayLength)
            );

            var typeDetails = this.DeserializeValue(context.Push(objectDetails.ElementObjectDetails, subject, "[]"));

            var loopVar = Expression.Variable(typeof(int), "loopVar");
            var forLoop = ExpressionHelpers.For(
                loopVar,
                Expression.Constant(0),
                Expression.LessThan(loopVar, arrayLength),
                Expression.PostIncrementAssign(loopVar),
                objectDetails.ElementObjectDetails.AssignExpression(subject, loopVar, typeDetails.OperationExpression)
            );

            Expression paddingLoop = Expression.Empty();
            if (arrayPaddingLength != null)
            {
                // If it's got a fixed size, we can just call ReadBytes once for the whole lot
                if (typeDetails.HasFixedSize)
                {
                    var readBytesMethod = typeof(BitfieldBinaryReader).GetMethod("ReadBytes", new[] { typeof(int) });
                    var readLength = Expression.Multiply(Expression.Constant(typeDetails.MinSize), arrayPaddingLength);
                    paddingLoop = Expression.Call(this.reader, readBytesMethod, readLength);
                }
                else
                {
                    var paddingLoopVar = Expression.Variable(typeof(int), "loopVar");
                    paddingLoop = ExpressionHelpers.For(
                        paddingLoopVar,
                        Expression.Constant(0),
                        Expression.LessThan(paddingLoopVar, arrayPaddingLength),
                        Expression.PostIncrementAssign(paddingLoopVar),
                        typeDetails.OperationExpression
                    );
                }
            }

            var block = Expression.Block(new[] { subject },
                arrayInit,
                forLoop,
                paddingLoop,
                subject
            );

            return new TypeDetails(hasFixedLength && typeDetails.HasFixedSize, hasFixedLength ? objectDetails.EnumerableLength * typeDetails.MinSize : 0, block);
        }

        private Expression CreateListOrArray(ObjectDetails objectDetails, Expression length)
        {
            if (objectDetails.Type.IsArray)
            {
                return Expression.NewArrayBounds(objectDetails.ElementType, length);
            }
            else
            {
                var listType = typeof(List<>).MakeGenericType(objectDetails.ElementType);
                var ctor = listType.GetConstructor(new[] { typeof(int) });
                return Expression.New(ctor, length);
            }
        }

        private TypeDetails CreateAndAssignFromDeserializer(Type deserializerType, Expression subject, TranslationContext context)
        {
            if (!deserializerType.IsClass || deserializerType.IsAbstract)
                throw new Exception("Custom deserializer must be a concrete class");

            Type interfaceType;

            interfaceType = deserializerType.GetInterfaces().FirstOrDefault(x => (x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDeserializer<>)) || x == typeof(IDeserializer));
            if (interfaceType == null)
            {
                throw new Exception("Custom deserializer must implement IDeserializer or IDeserializer<T>");
            }

            object deserializerObject;

            // Is it in the cache?
            if (!this.deserializerCache.TryGetValue(deserializerType, out deserializerObject))
            {
                deserializerObject = Activator.CreateInstance(deserializerType);
                this.deserializerCache.Add(deserializerType, deserializerObject);
            }

            var deserializer = Expression.Convert(Expression.Constant(deserializerObject), deserializerType);

            var minSize = (int)deserializerType.GetProperty("MinSize").GetValue(deserializerObject);
            var hasFixedSize = (bool)deserializerType.GetProperty("HasFixedSize").GetValue(deserializerObject);

            var deserializeMethod = deserializerType.GetMethod("Deserialize");
            var deserialization = Expression.Convert(Expression.Call(deserializer, deserializeMethod, this.reader), context.ObjectDetails.Type);

            var positionAccess = Expression.Property(Expression.Property(this.reader, "BaseStream"), "Position");

            var startingPositionVar = Expression.Variable(typeof(long), "beforePosition");
            var startingPositionAssignment = Expression.Assign(startingPositionVar, positionAccess);

            var wrappedAssignment = ExpressionHelpers.TryTranslate(Expression.Assign(subject, deserialization), context.GetMemberPath());

            var readBytesVar = Expression.Variable(typeof(long), "readBytes");
            var readBytesAssignment = Expression.Assign(readBytesVar, Expression.Subtract(positionAccess, startingPositionVar));

            Expression check;
            Expression exceptionMessage;
            if (hasFixedSize)
            {
                check = Expression.NotEqual(Expression.Convert(Expression.Constant(minSize), typeof(long)), readBytesVar);
                var constMessage = String.Format("Error deserializing field {0} using custom deserializer: Deserializer should have read {1} bytes, but actually read {{0}}", String.Join(".", context.GetMemberPath()), minSize);
                exceptionMessage = ExpressionHelpers.StringFormat(constMessage, readBytesVar);
            }
            else
            {
                check = Expression.GreaterThan(Expression.Convert(Expression.Constant(minSize), typeof(long)), readBytesVar);
                var constMessage = String.Format("Error deserializing field {0} using custom deserializer: Deserializer should have read {1} bytes or more, but actually read {{0}}", String.Join(".", context.GetMemberPath()), minSize);
                exceptionMessage = ExpressionHelpers.StringFormat(constMessage, readBytesVar);
            }

            var exceptionCtor = typeof(BitPackerTranslationException).GetConstructor(new[] { typeof(string), typeof(List<string>) });
            var newException = Expression.New(exceptionCtor, exceptionMessage, Expression.Constant(context.GetMemberPath()));

            var checkAndThrow = Expression.IfThen(check, Expression.Throw(newException));

            var block = Expression.Block(new[] { startingPositionVar, readBytesVar },
                startingPositionAssignment,
                wrappedAssignment,
                readBytesAssignment,
                checkAndThrow
            );

            return new TypeDetails(hasFixedSize, minSize, block);
        }
    }
}
