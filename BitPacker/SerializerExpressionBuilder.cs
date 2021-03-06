﻿using System;
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
        private static readonly MethodInfo writeBitfieldMethod = typeof(BitfieldBinaryWriter).GetMethod("WriteBitfield", new[] { typeof(ulong), typeof(int) });
        private static readonly MethodInfo beginBitfieldWriteMethod = typeof(BitfieldBinaryWriter).GetMethod("BeginBitfieldWrite", new[] { typeof(int) });
        private static readonly MethodInfo flushContainerMethod = typeof(BitfieldBinaryWriter).GetMethod("FlushContainer", new Type[0]);
        private static readonly MethodInfo getBytesMethod = typeof(Encoding).GetMethod("GetBytes", new[] { typeof(string), typeof(int), typeof(int), typeof(byte[]), typeof(int) });
        private static readonly MethodInfo serializeMethod = typeof(ICustomSerializer).GetMethod("Serialize", new[] { typeof(BinaryWriter), typeof(object), typeof(object) });

        private readonly Expression writer;
        private readonly Type objectType;
        private readonly Endianness? defaultEndianness;

        public SerializerExpressionBuilder(Expression writer, Type objectType, Endianness? defaultEndianness = null)
        {
            this.writer = writer;
            this.objectType = objectType;
            this.defaultEndianness = defaultEndianness;
        }

        public TypeDetails BuildExpression(Expression subject)
        {
            var objectDetails = new ObjectDetails(this.objectType, null, ImmutableStack.Init(new BitPackerMemberAttribute(0) { NullableEndianness = this.defaultEndianness }));
            objectDetails.Discover();

            var context = new TranslationContext(objectDetails, subject);
            return this.SerializeValue(context);
        }

        private TypeDetails SerializeValue(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;

            if (objectDetails.IsBoolean)
                return this.SerializeBoolean(context);

            if (objectDetails.IsString)
                return this.SerializeString(context);

            if (objectDetails.IsEnumerable)
                return this.SerializeEnumerable(context);

            if (objectDetails.IsPrimitiveType)
                return this.SerializePrimitive(context);

            if (objectDetails.IsEnum)
                return this.SerializeEnum(context);

            if (objectDetails.IsBitField)
                return this.SerializeBitField(context);

            if (objectDetails.IsCustomType)
                return this.SerializeCustomType(context);

            throw new Exception(String.Format("Don't know how to serialize type {0}. Is it missing a [BitPackerObject] attribute?", objectDetails.Type.Name));
        }

        private TypeDetails SerializeBitField(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;

            if (!objectDetails.IsBitField)
                return null;

            var blockMembers = new List<Expression>();
            blockMembers.Add(Expression.Call(this.writer, beginBitfieldWriteMethod, Expression.Constant(objectDetails.BitfieldWidthBytes)));

            var typeDetails = this.SerializeCustomTypeImpl(context);
            blockMembers.Add(typeDetails.OperationExpression);

            blockMembers.Add(Expression.Call(this.writer, flushContainerMethod));

            var block = Expression.Block(blockMembers);
            return new TypeDetails(true, objectDetails.BitfieldWidthBytes, block);
        }

        private TypeDetails SerializeCustomType(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;

            // If it's not marked with our attribute, we're not serializing it
            if (!objectDetails.IsCustomType)
                return null;

            if (objectDetails.CustomSerializer != null)
                return this.SerializeUsingSerializer(context);

            return this.SerializeCustomTypeImpl(context);
        }

        private TypeDetails SerializeCustomTypeImpl(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;

            // For the sake of TranslationContext.FindLengthKey, and for symmetry with DeserializationExpressionBuilder
            var localContext = context.PushIntermediateObject(objectDetails, context.Subject);

            Expression result;
            var typeDetails = objectDetails.Properties.Select(property =>
            {
                var newContext = localContext.Push(property, property.AccessExpression(context.Subject), property.PropertyInfo.Name);

                if (!property.PropertyInfo.CanRead)
                    throw new BitPackerTranslationException("The property must have a public getter", newContext.GetMemberPath());

                return this.SerializeValue(newContext);
            }).ToArray();

            var blockMembers = typeDetails.Select(x => x.OperationExpression).ToList();

            result = Expression.Block(blockMembers.Where(x => x != null).DefaultIfEmpty(Expression.Empty()));

            return new TypeDetails(typeDetails.All(x => x.HasFixedSize), typeDetails.Sum(x => x.MinSize), result);
        }

        private TypeDetails SerializeUsingSerializer(TranslationContext context)
        {
            var serializerType = context.ObjectDetails.CustomSerializer;

            try
            {
                if (!serializerType.IsClass || serializerType.IsAbstract || !typeof(ICustomSerializer).IsAssignableFrom(serializerType))
                    throw new Exception("Custom serializer must be a concrete class that implements ICustomSerializer");

                ICustomSerializer serializer = (ICustomSerializer)Activator.CreateInstance(serializerType, false);

                // Try and find them a context, if we can...
                var contextType = serializer.ContextType;
                var customContext = context.FindParentContextOfType(serializer.ContextType) ?? Expression.Constant(null);
                var wrappedInvocation = ExpressionHelpers.TryTranslate(Expression.Call(Expression.Constant(serializer), serializeMethod, this.writer, context.Subject, customContext), context.GetMemberPath());

                var positionAccess = Expression.Property(this.writer, "BytesWritten");
                var beforePositionVar = Expression.Variable(typeof(long), "beforePosition");
                var beforePositionAssign = Expression.Assign(beforePositionVar, positionAccess);

                var writtenBytes = Expression.Subtract(positionAccess, beforePositionVar);

                Expression check;
                Expression exceptionMessage;
                if (serializer.HasFixedSize)
                {
                    check = Expression.NotEqual(Expression.Convert(Expression.Constant(serializer.MinSize), typeof(long)), writtenBytes);
                    var constMessage = String.Format("Error serializing field {0} using custom serializer {1}: Serializer should have written exactly {2} bytes, but actually wrote {{0}}", String.Join(".", context.GetMemberPath()), serializerType, serializer.MinSize);
                    exceptionMessage = ExpressionHelpers.StringFormat(constMessage, writtenBytes);
                }
                else
                {
                    check = Expression.GreaterThan(Expression.Convert(Expression.Constant(serializer.MinSize), typeof(long)), writtenBytes);
                    var constMessage = String.Format("Error serializing field {0} using custom serializer {1}: Serializer should have written {2} bytes or more, but actually wrote {{0}}", String.Join(".", context.GetMemberPath()), serializerType, serializer.MinSize);
                    exceptionMessage = ExpressionHelpers.StringFormat(constMessage, writtenBytes);
                }

                var exceptionCtor = typeof(BitPackerTranslationException).GetConstructor(new[] { typeof(string), typeof(List<string>) });
                var newException = Expression.New(exceptionCtor, exceptionMessage, Expression.Constant(context.GetMemberPath()));

                var checkAndThrow = Expression.IfThen(check, Expression.Throw(newException));

                var block = Expression.Block(new[] { beforePositionVar },
                    beforePositionAssign,
                    wrappedInvocation,
                    checkAndThrow
                );

                return new TypeDetails(serializer.HasFixedSize, serializer.MinSize, block);
            }
            catch (Exception e)
            {
                if (e is BitPackerTranslationException)
                    throw;
                throw new BitPackerTranslationException("Error creating / executing custom serializer. See InnerException for details", context.GetMemberPath(), e);
            }
        }

        private TypeDetails SerializePrimitive(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;
            var value = context.Subject;

            var info = objectDetails.PrimitiveTypeInfo;

            List<Expression> blockMembers = new List<Expression>();

            if (objectDetails.IsLengthField)
            {
                var arrayAccess = context.FindVariableLengthArrayWithLengthKey(objectDetails.LengthKey);
                var length = ExpressionHelpers.LengthOfEnumerable(arrayAccess.Value, arrayAccess.ObjectDetails);
                var assign = Expression.Assign(value, Expression.Convert(length, objectDetails.Type));
                blockMembers.Add(assign);
            }

            if (objectDetails.Serialize)
            {
                if (objectDetails.IsChildOfBitField)
                {
                    var convertedValue = Expression.Convert(value, typeof(ulong));
                    var numBits = Expression.Constant(objectDetails.BitWidth.Value);
                    var writeBitfield = Expression.Call(this.writer, writeBitfieldMethod, convertedValue, numBits);

                    blockMembers.Add(writeBitfield);
                }
                // Even through EndiannessUtilities has now Swap(byte) overload, we get an AmbiguousMatchException
                // when we try and find such a method (maybe the byte is being coerced into an int or something?).
                // Therefore, handle this...
                else if (objectDetails.Endianness != EndianUtilities.HostEndianness && info.Size > 1)
                {
                    blockMembers.Add(info.SwappedSerializeExpression(this.writer, value));
                }
                else
                {
                    blockMembers.Add(info.SerializeExpression(this.writer, value));
                }
            }

            var wrappedWrite = ExpressionHelpers.TryTranslate(Expression.Block(blockMembers), context.GetMemberPath());

            return new TypeDetails(true, objectDetails.Serialize ? info.Size : 0, wrappedWrite);
        }

        private TypeDetails SerializeEnum(TranslationContext context)
        {
            var equivalentObjectDetails = context.ObjectDetails.EnumEquivalentObjectDetails;
            var newContext = context.Push(equivalentObjectDetails, Expression.Convert(context.Subject, equivalentObjectDetails.Type), null);
            return this.SerializePrimitive(newContext);
        }

        private TypeDetails SerializeBoolean(TranslationContext context)
        {
            var type = context.ObjectDetails.BooleanEquivalentObjectDetails.Type;
            var value = Expression.Condition(context.Subject, Expression.Convert(Expression.Constant(1), type), Expression.Convert(Expression.Constant(0), type));
            var newContext = context.Push(context.ObjectDetails.BooleanEquivalentObjectDetails, value, null);
            return this.SerializePrimitive(newContext);
        }

        private TypeDetails SerializeString(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;
            var encoding = Expression.Constant(objectDetails.Encoding);
            var str = context.Subject;

            var blockMembers = new List<Expression>();

            var byteArrayVar = Expression.Variable(typeof(byte[]), "bytes");
            var byteCountVar = Expression.Variable(typeof(int), "byteCount");
            var byteCountAssign = Expression.Assign(byteCountVar, ExpressionHelpers.ByteCountOfString(str, objectDetails));
            var arrayInit = Expression.NewArrayBounds(typeof(byte), byteCountVar);
            var arrayAssign = Expression.Assign(byteArrayVar, arrayInit);
            var strLength = Expression.Property(str, "Length");
            var getBytesCall = Expression.Call(encoding, getBytesMethod, str, Expression.Constant(0), strLength, byteArrayVar, Expression.Constant(0));

            // If it's fixed length, is the wrong length, and doesn't allow null-terminating, then we have to throw
            Expression lengthAssertion = Expression.Empty();
            if (objectDetails.LengthKey == null && objectDetails.EnumerableLength > 0 && !ObjectDetails.NullTerminatedEncodings.Contains(objectDetails.Encoding))
            {
                var exceptionMessage = ExpressionHelpers.StringFormat(String.Format("You specified an explicit length of {0} bytes for a string, but the actual string contains {{0}} bytes and its encoding ({1}) can't be NULL-padded.", objectDetails.EnumerableLength, objectDetails.Encoding.EncodingName), byteCountVar);
                var throwExpr = Expression.Throw(ExpressionHelpers.MakeBitPackerTranslationException(exceptionMessage, context.GetMemberPath()));
                lengthAssertion = Expression.IfThen(Expression.NotEqual(Expression.Constant(objectDetails.EnumerableLength), byteCountVar), throwExpr);
            }

            var typeDetails = this.SerializeEnumerable(context.Push(objectDetails, byteArrayVar, "[]"));

            var block = Expression.Block(new[] { byteCountVar, byteArrayVar },
                byteCountAssign,
                arrayAssign,
                lengthAssertion,
                getBytesCall,
                typeDetails.OperationExpression
            );

            return new TypeDetails(typeDetails.HasFixedSize, typeDetails.MinSize, block);
        }

        private TypeDetails SerializeEnumerable(TranslationContext context)
        {    
            var objectDetails = context.ObjectDetails;
            var enumerable = context.Subject;
            var blockMembers = new List<Expression>();
            var blockVars = new List<ParameterExpression>();

            ParameterExpression lengthVar = null;
            bool hasFixedLength = objectDetails.EnumerableLength > 0;

            // If they specified an explicit length, throw if the actual enumerable is longer
            if (hasFixedLength)
            {
                lengthVar = Expression.Variable(typeof(int), "length");
                blockVars.Add(lengthVar);

                var enumerableLength = ExpressionHelpers.LengthOfEnumerable(enumerable, objectDetails);
                blockMembers.Add(ExpressionHelpers.TryTranslate(Expression.Assign(lengthVar, enumerableLength), context.GetMemberPath()));

                var enumerableLengthExpr = Expression.Constant(objectDetails.EnumerableLength);
                var test = Expression.GreaterThan(lengthVar, enumerableLengthExpr);
                var exceptionMessage = ExpressionHelpers.StringFormat("You specified an explicit length ({0}) for an array member, but the actual member is longer ({1})", enumerableLengthExpr, lengthVar);
                var throwExpr = Expression.Throw(ExpressionHelpers.MakeBitPackerTranslationException(exceptionMessage, context.GetMemberPath()));
                blockMembers.Add(Expression.IfThen(test, throwExpr));
            }

            // If they specified a length field, we've already assigned it (yay how organised as we?!)

            var loopVar = Expression.Variable(objectDetails.ElementType, "loopVariable");
            var typeDetails = this.SerializeValue(context.Push(objectDetails.ElementObjectDetails, loopVar, "[]"));
            Expression loop;
            if (objectDetails.Type.IsArray || objectDetails.Type == typeof(string))
                loop = ExpressionHelpers.ForElementsInArray(loopVar, enumerable, typeDetails.OperationExpression);
            else
                loop = ExpressionHelpers.ForEach(enumerable, objectDetails.ElementType, loopVar, typeDetails.OperationExpression);
            blockMembers.Add(ExpressionHelpers.TryTranslate(loop, context.GetMemberPath()));

            // If it's a fixed-length array, we might need to pad it out
            // if (lengthVar < property.EnumerableLength)
            // {
            //     var emptyInstance = new SomeType(); // Or whatever
            //     for (int i = lengthVar; i < property.EnumerableLength; i++)
            //     {
            //         writer.Write(emptyInstance); // Or whatever the writing expression happens to be
            //     }
            // }
            // If SomeType doesn't have a default constructor, only throw if lengthVar < property.EnumerableLength
            if (hasFixedLength)
            {
                var emptyInstanceVar = Expression.Variable(objectDetails.ElementType, "emptyInstance");
                blockVars.Add(emptyInstanceVar);

                var elementTypeCtor = objectDetails.ElementType.GetConstructor(Type.EmptyTypes);
                // GetConstructor doesn't pick up on a struct's default parameterless constructor
                if (elementTypeCtor != null || objectDetails.ElementType.IsValueType)
                {
                    var emptyInstanceAssignment = ExpressionHelpers.TryTranslate(Expression.Assign(emptyInstanceVar, Expression.New(objectDetails.ElementType)), context.GetMemberPath());

                    var initAndSerialize = this.SerializeValue(context.Push(objectDetails.ElementObjectDetails, emptyInstanceVar, "[]")).OperationExpression;
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
                else
                {
                    var exceptionMessage = Expression.Constant(String.Format("Unable to pad array with elements of type {0}, as it does not have a parameterless constructor", objectDetails.ElementType.Description()));
                    var throwExpr = Expression.Throw(ExpressionHelpers.MakeBitPackerTranslationException(exceptionMessage, context.GetMemberPath()));
                    blockMembers.Add(Expression.IfThen(Expression.LessThan(lengthVar, Expression.Constant(objectDetails.EnumerableLength)), throwExpr));
                }
            }

            var block = Expression.Block(blockVars, blockMembers);

            return new TypeDetails(hasFixedLength && typeDetails.HasFixedSize, hasFixedLength ? objectDetails.EnumerableLength * typeDetails.MinSize : 0, block);
        }
    }
}
