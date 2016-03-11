using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal static class ExpressionHelpers
    {
        private static readonly MethodInfo moveNextMethod = typeof(IEnumerator).GetMethod("MoveNext", new Type[0]);
        private static readonly MethodInfo stringFormatMethod = typeof(String).GetMethod("Format", new[] { typeof(string), typeof(string[]) });
        private static readonly MethodInfo getByteCountMethod = typeof(Encoding).GetMethod("GetByteCount", new[] { typeof(string) });

        public static Expression ForEach(Expression collection, Type elementType, ParameterExpression loopVar, Expression loopContent)
        {
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);

            var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");
            var getEnumeratorCall = Expression.Call(collection, enumerableType.GetMethod("GetEnumerator"));
            var enumeratorAssign = Expression.Assign(enumeratorVar, getEnumeratorCall);

            // The MoveNext method's actually on IEnumerator, not IEnumerator<T>
            var moveNextCall = Expression.Call(enumeratorVar, moveNextMethod);

            var breakLabel = Expression.Label("LoopBreak");

            var loop = Expression.Block(new[] { enumeratorVar },
                enumeratorAssign,
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Equal(moveNextCall, Expression.Constant(true)),
                        Expression.Block(new[] { loopVar },
                            Expression.Assign(loopVar, Expression.Property(enumeratorVar, "Current")),
                            loopContent
                        ),
                        Expression.Break(breakLabel)
                    ),
                    breakLabel
                )
            );

            return loop;
        }

        public static Expression For(ParameterExpression loopVar, Expression initValue, Expression condition, Expression increment, Expression loopContent)
        {
            var initAssign = Expression.Assign(loopVar, initValue);

            var breakLabel = Expression.Label("LoopBreak");

            var loop = Expression.Block(new[] { loopVar },
                initAssign,
                Expression.Loop(
                    Expression.IfThenElse(
                        condition,
                        Expression.Block(
                            loopContent,
                            increment
                        ),
                        Expression.Break(breakLabel)
                    ),
                    breakLabel
                )
            );

            return loop;
        }

        public static Expression ForElementsInArray(ParameterExpression loopVar, Expression array, Expression loopContent)
        {
            var i = Expression.Variable(typeof(int), "i");
            var length = Expression.ArrayLength(array);

            var ourLoopContent = Expression.Block(new[] { loopVar },
                Expression.Assign(loopVar, Expression.ArrayAccess(array, i)),
                loopContent
            );

            return For(i, Expression.Constant(0), Expression.LessThan(i, length), Expression.PostIncrementAssign(i), ourLoopContent);
        }

        public static Expression LengthOfEnumerable(Expression collection, ObjectDetails objectDetails)
        {
            // This is slightly hacky. If we're called from SerializeEnumerable, from SerializeString, the ObjectDetails
            // refers to a single byte, not to a byte array. Therefore don't use the ObjectDetails when finding
            // the length of arrays.
            if (collection.Type.IsArray)
                return Expression.ArrayLength(collection);
            if (collection.Type == typeof(string))
                return ByteCountOfString(collection, objectDetails);
            return Expression.Call(typeof(Enumerable), "Count", new[] { objectDetails.ElementType }, collection);
        }

        public static Expression ByteCountOfString(Expression str, ObjectDetails objectDetails)
        {
            var encoding = Expression.Constant(objectDetails.Encoding);
            var numBytes = Expression.Call(encoding, getByteCountMethod, str);
            var paddingBytes = objectDetails.NullTerminated ? 1 : 0;
            return Expression.Add(numBytes, Expression.Constant(paddingBytes));
        }

        public static Expression TryTranslate(Expression block, List<string> memberPath)
        {
            var e = Expression.Parameter(typeof(Exception), "e");
            var exception = MakeBitPackerTranslationException(memberPath, e);

            var eToRethrow = Expression.Parameter(typeof(BitPackerTranslationException), "e");

            return Expression.TryCatch(
                block,
                Expression.Catch(
                    e,
                    Expression.Block(
                        Expression.IfThenElse(
                            Expression.TypeIs(e, typeof(BitPackerTranslationException)),
                            Expression.Throw(e),
                            Expression.Throw(exception)
                        ),
                        Expression.Default(block.Type)
                    )
                )
            );
        }

        public static Expression MakeBitPackerTranslationException(Expression message, List<string> memberPath)
        {
            var ctor = typeof(BitPackerTranslationException).GetConstructor(new[] { typeof(string), typeof(List<string>) });
            return Expression.New(ctor, message, Expression.Constant(memberPath));
        }

        public static Expression MakeBitPackerTranslationException(List<string> memberPath, Expression innerException)
        {
            var ctor = typeof(BitPackerTranslationException).GetConstructor(new[] { typeof(List<string>), typeof(Exception) });
            return Expression.New(ctor, Expression.Constant(memberPath), innerException);
        }

        public static Expression StringFormat(string format, params Expression[] args)
        {
            var objArray = Expression.NewArrayInit(typeof(object), args.Select(x => Expression.Convert(x, typeof(object))));
            return Expression.Call(stringFormatMethod, new Expression[] { Expression.Constant(format), objArray });
        }
    }
}
