using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal static class ExpressionHelpers
    {
        public static Expression ForEach(Expression collection, Type elementType, ParameterExpression loopVar, Expression loopContent)
        {
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);

            var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");
            var getEnumeratorCall = Expression.Call(collection, enumerableType.GetMethod("GetEnumerator"));
            var enumeratorAssign = Expression.Assign(enumeratorVar, getEnumeratorCall);

            // The MoveNext method's actually on IEnumerator, not IEnumerator<T>
            var moveNextCall = Expression.Call(enumeratorVar, typeof(IEnumerator).GetMethod("MoveNext"));

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
                breakLabel)
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
                breakLabel)
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
            if (objectDetails.Type.IsArray)
                return Expression.ArrayLength(collection);
            if (objectDetails.IsString)
                return Expression.Property(collection, "Length");
            return Expression.Call(typeof(Enumerable), "Count", new[] { objectDetails.ElementType }, collection);
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

        public static Expression MakeBitPackerTranslationException(List<string> memberPath, Expression innerException)
        {
            var ctor = typeof(BitPackerTranslationException).GetConstructor(new[] { typeof(List<string>), typeof(Exception) });
            return Expression.New(ctor, Expression.Constant(memberPath), innerException);
        }

        public static Expression StringFormat(string format, params Expression[] args)
        {
            var method = typeof(String).GetMethod("Format", new[] { typeof(string), typeof(string[]) });
            var objArray = Expression.NewArrayInit(typeof(object), args.Select(x => Expression.Convert(x, typeof(object))));
            return Expression.Call(method, new Expression[] { Expression.Constant(format), objArray });
        }
    }
}
