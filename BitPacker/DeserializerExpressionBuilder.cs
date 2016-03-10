using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class DeserializerExpressionBuilder
    {
        private static readonly MethodInfo readBitfieldMethod = typeof(BitfieldBinaryReader).GetMethod("ReadBitfield", new[] { typeof(int), typeof(int), typeof(bool) });
        private static readonly MethodInfo flushContainerMethod = typeof(BitfieldBinaryReader).GetMethod("FlushContainer", new Type[0]);
        private static readonly MethodInfo getStringMethod = typeof(Encoding).GetMethod("GetString", new[] { typeof(byte[]) });
        private static readonly MethodInfo readBytesMethod = typeof(BitfieldBinaryReader).GetMethod("ReadBytes", new[] { typeof(int) });
        private static readonly MethodInfo TrimEndMethod = typeof(string).GetMethod("TrimEnd", new[] { typeof(char[]) });
        private static readonly MethodInfo readByteMethod = typeof(BitfieldBinaryReader).GetMethod("ReadByte");
        private static readonly MethodInfo byteListAddMethod = typeof(List<byte>).GetMethod("Add", new[] { typeof(byte) });
        private static readonly MethodInfo byteListToArrayMethod = typeof(List<byte>).GetMethod("ToArray", new Type[0]);
        private static readonly MethodInfo deserializeMethod = typeof(ICustomDeserializer).GetMethod("Deserialize", new[] { typeof(BinaryReader), typeof(object) });

        private readonly ParameterExpression reader;
        private readonly Type objectType;
        private readonly Endianness? defaultEndianness;

        public DeserializerExpressionBuilder(ParameterExpression reader, Type objectType, Endianness? defaultEndianness = null)
        {
            this.reader = reader;
            this.objectType = objectType;
            this.defaultEndianness = defaultEndianness;
        }

        public TypeDetails BuildExpression()
        {
            var subject = Expression.Parameter(this.objectType, "rootSubject");

            var objectDetails = new ObjectDetails(this.objectType, new BitPackerMemberAttribute(0) { NullableEndianness = this.defaultEndianness });
            objectDetails.Discover();

            // First, we need to make sure it's fully constructed
            var blockMembers = new List<Expression>();

            var context = new TranslationContext(objectDetails, subject);
            var deserialized = this.DeserializeAndAssignValue(context);
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

        private TypeDetails DeserializeAndAssignValue(TranslationContext context)
        {
            try
            {
                var typeDetails = this.DeserializeValue(context);
                var wrappedAssignment = ExpressionHelpers.TryTranslate(Expression.Assign(context.Subject, typeDetails.OperationExpression), context.GetMemberPath());
                return new TypeDetails(typeDetails.HasFixedSize, typeDetails.MinSize, wrappedAssignment);
            }
            catch (Exception e)
            {
                if (e is BitPackerTranslationException)
                    throw e;
                throw new BitPackerTranslationException(context.GetMemberPath(), e);
            }
        }

        private TypeDetails DeserializeValue(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;
            if (objectDetails.IsString)
                return this.DeserializeString(context);

            if (objectDetails.IsBoolean)
                return this.DeserializeBoolean(objectDetails);

            if (objectDetails.IsEnumerable)
                return this.DeserializeEnumerable(context);

            if (objectDetails.IsPrimitiveType)
                return this.DeserializePrimitive(objectDetails);

            if (objectDetails.IsEnum)
                return this.DeserializeEnum(objectDetails);

            if (objectDetails.IsCustomType)
                return this.DeserializeCustomType(context);

            throw new Exception(String.Format("Don't know how to deserialize type {0}", objectDetails.Type.Name));
        }

        public TypeDetails DeserializeCustomType(TranslationContext context)
        {
            try
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

                var localContext = context.PushIntermediateObject(objectDetails, subject);

                var typeDetails = objectDetails.Properties.Select(property =>
                {
                    var newContext = localContext.Push(property, property.AccessExpression(subject), property.PropertyInfo.Name);

                    if (!property.PropertyInfo.CanWrite)
                        throw new BitPackerTranslationException("The property must have a public setter", newContext.GetMemberPath());

                    // Does it have a custom deserializer?
                    if (property.CustomDeserializer != null)
                        return this.CreateAndAssignFromDeserializer(property.CustomDeserializer, property.AccessExpression(subject), newContext);
                    else
                        return this.DeserializeAndAssignValue(newContext);
                }).ToArray();

                blockMembers.AddRange(typeDetails.Select(x => x.OperationExpression));


                blockMembers.Add(subject); // Last value in block is the return value
                var result = Expression.Block(new[] { subject }, blockMembers.Where(x => x != null));
                return new TypeDetails(typeDetails.All(x => x.HasFixedSize), typeDetails.Sum(x => x.MinSize), result);
            }
            catch (Exception e)
            {
                if (e is BitPackerTranslationException)
                    throw e;
                throw new BitPackerTranslationException(context.GetMemberPath(), e);
            }
        }

        private TypeDetails DeserializePrimitive(ObjectDetails objectDetails)
        {
            // Even through EndiannessUtilities has now Swap(byte) overload, we get an AmbiguousMatchException
            // when we try and find such a method (maybe the byte is being coerced into an int or something?).
            // Therefore, handle this..

            var info = objectDetails.PrimitiveTypeInfo;
            Expression readExpression;

            if (info.IsIntegral && objectDetails.BitWidth.HasValue)
            {
                var containerSize = Expression.Constant(info.Size);
                var numBits = Expression.Constant(objectDetails.BitWidth.Value);
                var swapEndianness = Expression.Constant(objectDetails.Endianness != EndianUtilities.HostEndianness);
                var readValue = Expression.Call(this.reader, readBitfieldMethod, containerSize, numBits, swapEndianness);
                var converted = Expression.Convert(readValue, objectDetails.Type);
                if (objectDetails.PadContainerAfter)
                {
                    var valueVar = Expression.Variable(objectDetails.Type, "value");
                    readExpression = Expression.Block(new[] { valueVar },
                        Expression.Assign(valueVar, converted),
                        Expression.Call(this.reader, flushContainerMethod),
                        valueVar
                    );
                }
                else
                {
                    readExpression = converted;
                }
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
                arrayLength = context.FindLengthKey(objectDetails.LengthKey).Value;

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
                throw new BitPackerTranslationException(context.GetMemberPath(), new InvalidArraySetupException("Unknown length for array. Arrays must either have a Length or LengthKey for deserialziation"));
            }

            return arrayLength;
        }

        public TypeDetails DeserializeString(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;
            var encoding = Expression.Constant(objectDetails.Encoding);
            bool hasFixedLength = objectDetails.EnumerableLength > 0;
            BlockExpression block;

            // Riiight... So. It's either got a length, or it's an ASCII null-terminated string
            // The length option is easy...
            if (hasFixedLength || objectDetails.LengthKey != null)
            {
                Expression arrayPaddingLength;
                var arrayLength = this.GetArrayLength(context, out arrayPaddingLength);
                var blockMembers = new List<Expression>();

                var bytesArrayVar = Expression.Variable(typeof(byte[]), "bytes");
                blockMembers.Add(Expression.Assign(bytesArrayVar, Expression.Call(this.reader, readBytesMethod, Expression.Convert(arrayLength, typeof(int)))));
                if (arrayPaddingLength != null)
                    blockMembers.Add(Expression.Call(this.reader, readBytesMethod, arrayPaddingLength));

                var stringRead = Expression.Call(encoding, getStringMethod, bytesArrayVar);

                // If it's ASCII, trim the NULLs
                if (objectDetails.Encoding == Encoding.ASCII)
                    blockMembers.Add(Expression.Call(stringRead, TrimEndMethod, Expression.NewArrayInit(typeof(char), Expression.Constant('\0'))));
                else
                    blockMembers.Add(stringRead);

                block = Expression.Block(new[] { bytesArrayVar }, blockMembers);
            }
            else if (objectDetails.NullTerminated && ObjectDetails.NullTerminatedEncodings.Contains(objectDetails.Encoding))
            {
                // We've no choice but to walk the thing. Thankfully we know it's ASCII or UTF-8
                // Once we find a null byte, that null's the last character of the string - but we don't know how long it's going to be....

                var listCtor = typeof(List<byte>).GetConstructor(new Type[0]);
                var listVar = Expression.Variable(typeof(List<byte>), "bytes");
                var listAssign = Expression.Assign(listVar, Expression.New(listCtor));

                var breakLabel = Expression.Label("LoopBreak");
                var byteVar = Expression.Variable(typeof(byte), "byte");

                var loopContents = Expression.Block(new[] { byteVar },
                    Expression.Assign(byteVar, Expression.Call(this.reader, readByteMethod)),
                    Expression.IfThenElse(
                        Expression.Equal(byteVar, Expression.Constant((byte)0)),
                        Expression.Break(breakLabel),
                        Expression.Call(listVar, byteListAddMethod, byteVar)
                    )
                );

                var loop = Expression.Loop(loopContents, breakLabel);

                var toArrayCall = Expression.Call(listVar, byteListToArrayMethod);
                var stringRead = Expression.Call(encoding, getStringMethod, toArrayCall);

                block = Expression.Block(new[] { listVar },
                    listAssign,
                    loop,
                    stringRead
                );
            }
            else
            {
                Exception e;
                if (ObjectDetails.NullTerminatedEncodings.Contains(objectDetails.Encoding))
                    e = new InvalidStringSetupException(String.Format("{0} strings must either be null-terminated, or have a Length or Length Key (or both)", objectDetails.Encoding));
                else
                    e = new InvalidStringSetupException(String.Format("{0} strings must either have a Length or LengthKey (or both)", objectDetails.Encoding));
                throw new BitPackerTranslationException(context.GetMemberPath(), e);
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

            var loopVar = Expression.Variable(arrayLength.Type, "loopVar");
            var forLoop = ExpressionHelpers.For(
                loopVar,
                Expression.Convert(Expression.Constant(0), arrayLength.Type),
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
            try
            {
                if (!deserializerType.IsClass || deserializerType.IsAbstract || !typeof(ICustomDeserializer).IsAssignableFrom(deserializerType))
                    throw new Exception("Custom deserializer must be a concrete class");

                ICustomDeserializer deserializer = (ICustomDeserializer)Activator.CreateInstance(deserializerType, false);

                // Try and find them a context, if we can...
                var contextType = deserializer.ContextType;
                var customContext = context.FindParentContextOfType(deserializer.ContextType) ?? Expression.Constant(null);
                var wrappedInvocation = Expression.Call(Expression.Constant(deserializer), deserializeMethod, this.reader, customContext);

                var positionAccess = Expression.Property(this.reader, "BytesRead");

                var startingPositionVar = Expression.Variable(typeof(long), "beforePosition");
                var startingPositionAssignment = Expression.Assign(startingPositionVar, positionAccess);

                var wrappedAssignment = ExpressionHelpers.TryTranslate(Expression.Assign(subject, Expression.Convert(wrappedInvocation, context.ObjectDetails.Type)), context.GetMemberPath());

                var readBytesVar = Expression.Variable(typeof(long), "readBytes");
                var readBytesAssignment = Expression.Assign(readBytesVar, Expression.Subtract(positionAccess, startingPositionVar));

                Expression check;
                Expression exceptionMessage;
                if (deserializer.HasFixedSize)
                {
                    check = Expression.NotEqual(Expression.Convert(Expression.Constant(deserializer.MinSize), typeof(long)), readBytesVar);
                    var constMessage = String.Format("Error deserializing field {0} using custom deserializer {1}: Deserializer should have read {2} bytes, but actually read {{0}}", String.Join(".", context.GetMemberPath()), deserializerType, deserializer.MinSize);
                    exceptionMessage = ExpressionHelpers.StringFormat(constMessage, readBytesVar);
                }
                else
                {
                    check = Expression.GreaterThan(Expression.Convert(Expression.Constant(deserializer.MinSize), typeof(long)), readBytesVar);
                    var constMessage = String.Format("Error deserializing field {0} using custom deserializer {1}: Deserializer should have read {2} bytes or more, but actually read {{0}}", String.Join(".", context.GetMemberPath()), deserializerType, deserializer.MinSize);
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

                return new TypeDetails(deserializer.HasFixedSize, deserializer.MinSize, block);
            }
            catch (Exception e)
            {
                if (e is BitPackerTranslationException)
                    throw;
                throw new BitPackerTranslationException("Error creating / executing custom deserializer. See InnerException for details", context.GetMemberPath(), e);
            }
        }
    }
}
