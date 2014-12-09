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
        private static readonly MethodInfo writeBitfieldMethod = typeof(BitfieldBinaryWriter).GetMethod("WriteBitfield", new[] { typeof(ulong), typeof(int), typeof(int), typeof(bool) });
        private static readonly MethodInfo flushContainerMethod = typeof(BitfieldBinaryWriter).GetMethod("FlushContainer", new Type[0]);
        private static readonly MethodInfo getByteCountMethod = typeof(Encoding).GetMethod("GetByteCount", new[] { typeof(string) });
        private static readonly MethodInfo getBytesMethod = typeof(Encoding).GetMethod("GetBytes", new[] { typeof(string), typeof(int), typeof(int), typeof(byte[]), typeof(int) });
        private static readonly MethodInfo serializeMethod = typeof(ICustomSerializer).GetMethod("Serialize", new[] { typeof(BinaryWriter), typeof(object), typeof(object) });

        private readonly Expression writer;
        private readonly Type objectType;

        public SerializerExpressionBuilder(Expression writer, Type objectType)
        {
            this.writer = writer;
            this.objectType = objectType;
        }

        public TypeDetails Serialize(Expression subject)
        {
            var objectDetails = new ObjectDetails(this.objectType, new BitPackerMemberAttribute());
            objectDetails.Discover();

            var blockMembers = new List<Expression>();

            blockMembers.Add(this.HandleVariableLengthArrays(subject, objectDetails));

            var context = new TranslationContext(objectDetails, subject);
            var serialized = this.SerializeCustomType(context);
            blockMembers.Add(serialized.OperationExpression);

            return new TypeDetails(serialized.HasFixedSize, serialized.MinSize, Expression.Block(blockMembers.Where(x => x != null)));
        }

        private Expression HandleVariableLengthArrays(Expression subject, ObjectDetails objectDetails)
        {
            var arrays = objectDetails.RecursiveFlatPropertyAccess(subject).Where(x => x.ObjectDetails.IsEnumerable && x.ObjectDetails.LengthKey != null);
            var lengthFields = objectDetails.LengthFields
                .Select(x => new PropertyObjectDetailsWithAccess(x.Value, x.Value.AccessExpression(subject)))
                .Concat(objectDetails.RecursiveFlatPropertyAccess(subject)
                    .Where(x => x.ObjectDetails.IsCustomType)
                    .SelectMany(x => x.ObjectDetails.LengthFields.Select(y => new PropertyObjectDetailsWithAccess(y.Value, y.Value.AccessExpression(x.Value)))));

            var allKeys = arrays.Select(x => x.ObjectDetails.LengthKey).Concat(lengthFields.Select(x => x.ObjectDetails.LengthKey)).Distinct();

            var groups = allKeys.Select(x => new
            {
                Key = x,
                Arrays = arrays.Where(y => y.ObjectDetails.LengthKey == x).ToArray(),
                LengthFields =  lengthFields.Where(y => y.ObjectDetails.LengthKey == x).ToArray(),
            });

            // For each, synthesize an assign to the integral field, assigning the length of the array field
            var blockMembers = groups.Select(group =>
            {
                if (group.Arrays.Length != 1)
                    throw new Exception(String.Format("Found zero, or more than one arrays fields for Length Key {0}", group.Key));

                if (group.LengthFields.Length != 1)
                    throw new Exception(String.Format("Found zero, or more than one integral fields for Length Key {0}", group.Key));

                if (!group.LengthFields[0].ObjectDetails.Serialize)
                    return null;

                // If it's not writable, then that's fine
                if (!group.LengthFields[0].ObjectDetails.PropertyInfo.CanWrite)
                    return null;

                return Expression.Assign(
                    group.LengthFields[0].Value,
                    ExpressionHelpers.LengthOfEnumerable(
                        group.Arrays[0].Value,
                        group.Arrays[0].ObjectDetails
                    )
                );
            });

            blockMembers = blockMembers.Where(x => x != null);

            return blockMembers.Any() ? Expression.Block(blockMembers) : null;
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

            if (objectDetails.IsCustomType)
                return this.SerializeCustomType(context);

            throw new Exception(String.Format("Don't know how to serialize type {0}. Is it missing a [BitPackerObject] attribute?", objectDetails.Type.Name));
        }

        private TypeDetails SerializeCustomType(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;

            // If it's not marked with our attribute, we're not serializing it
            if (!objectDetails.IsCustomType)
                return null;

            if (objectDetails.CustomSerializer != null)
                return this.SerializeUsingSerializer(context);

            Expression result;
            var typeDetails = objectDetails.Properties.Select(property =>
            {
                return this.SerializeValue(context.Push(property, property.AccessExpression(context.Subject), property.PropertyInfo.Name));
            }).ToArray();

            var blockMembers = typeDetails.Select(x => x.OperationExpression);

            result = Expression.Block(blockMembers.Where(x => x != null).DefaultIfEmpty(Expression.Empty()));

            return new TypeDetails(typeDetails.All(x => x.HasFixedSize), typeDetails.Sum(x => x.MinSize), result);
        }

        private TypeDetails SerializeUsingSerializer(TranslationContext context)
        {
            ICustomSerializer serializer = (ICustomSerializer)Activator.CreateInstance(context.ObjectDetails.CustomSerializer, false);

            // Try and find them a context, if we can...
            var contextType = serializer.ContextType;
            var customContext = context.FindParentContextOfType(serializer.ContextType) ?? Expression.Constant(null);
            var invocation = Expression.Call(Expression.Constant(serializer), serializeMethod, this.writer, context.Subject, customContext);
            return new TypeDetails(serializer.HasFixedSize, serializer.MinSize, ExpressionHelpers.TryTranslate(invocation, context.GetMemberPath()));
        }

        private TypeDetails SerializePrimitive(TranslationContext context)
        {
            var objectDetails = context.ObjectDetails;
            var value = context.Subject;

            var info = objectDetails.PrimitiveTypeInfo;
            Expression writeExpression;

            if (info.IsIntegral && objectDetails.BitWidth.HasValue)
            {
                
                var convertedValue = Expression.Convert(value, typeof(ulong));
                var containerSize = Expression.Constant(info.Size);
                var numBits = Expression.Constant(objectDetails.BitWidth.Value);
                var swapEndianness = Expression.Constant(objectDetails.Endianness != EndianUtilities.HostEndianness);
                var writeBitfield = Expression.Call(this.writer, writeBitfieldMethod, convertedValue, containerSize, numBits, swapEndianness);
                if (objectDetails.PadContainerAfter)
                    writeExpression = Expression.Block(writeBitfield, Expression.Call(this.writer, flushContainerMethod));
                else
                    writeExpression = writeBitfield;
            }
            // Even through EndiannessUtilities has now Swap(byte) overload, we get an AmbiguousMatchException
            // when we try and find such a method (maybe the byte is being coerced into an int or something?).
            // Therefore, handle this...
            else if (objectDetails.Endianness != EndianUtilities.HostEndianness && info.Size > 1)
            {
                // If EndianUtilities has a Swap method for this type, then we can convert it
                var swapMethod = typeof(EndianUtilities).GetMethod("Swap", new[] { objectDetails.Type } );
                if (swapMethod != null)
                    value = Expression.Call(swapMethod, value);
                writeExpression = info.SerializeExpression(this.writer, value);
            }
            else
            {
                writeExpression = info.SerializeExpression(this.writer, value);
            }

            var wrappedWrite = ExpressionHelpers.TryTranslate(writeExpression, context.GetMemberPath());

            return new TypeDetails(true, info.Size, wrappedWrite);
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
            var encoding = Expression.Constant(context.ObjectDetails.Encoding);
            var str = context.Subject;

            var blockMembers = new List<Expression>();

            var numBytes = Expression.Call(encoding, getByteCountMethod, str);
            var byteArrayVar = Expression.Variable(typeof(byte[]), "bytes");
            var paddingBytes = context.ObjectDetails.NullTerminated ? 1 : 0;
            var arrayInit = Expression.NewArrayBounds(typeof(byte), Expression.Add(numBytes, Expression.Constant(paddingBytes)));
            var arrayAssign = Expression.Assign(byteArrayVar, arrayInit);
            var strLength = Expression.Property(str, "Length");
            var getBytesCall = Expression.Call(encoding, getBytesMethod, str, Expression.Constant(0), strLength, byteArrayVar, Expression.Constant(0));

            var typeDetails = this.SerializeEnumerable(context.Push(context.ObjectDetails, byteArrayVar, "[]"));

            var block = Expression.Block(new[] { byteArrayVar },
                arrayAssign,
                getBytesCall,
                typeDetails.OperationExpression
            );

            return new TypeDetails(typeDetails.HasFixedSize, typeDetails.MinSize, block);

            //var  this.SerializeEnumerable(context.Push(context.ObjectDetails, call, "bytes"));
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

                var test = Expression.GreaterThan(lengthVar, Expression.Constant(objectDetails.EnumerableLength));
                var throwExpr = Expression.Throw(ExpressionHelpers.MakeBitPackerTranslationException(context.GetMemberPath(), Expression.Constant(new Exception("You specified an explicit length for an array member, but the actual member is longer"))));
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
            if (hasFixedLength)
            {
                var emptyInstanceVar = Expression.Variable(objectDetails.ElementType, "emptyInstance");
                blockVars.Add(emptyInstanceVar);
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

            var block = Expression.Block(blockVars, blockMembers);

            return new TypeDetails(hasFixedLength && typeDetails.HasFixedSize, hasFixedLength ? objectDetails.EnumerableLength * typeDetails.MinSize : 0, block);
        }
    }
}
