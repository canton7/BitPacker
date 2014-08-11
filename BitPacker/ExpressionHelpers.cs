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

        public static Expression LengthOfEnumerable(Expression collection, Type elementType)
        {
            //var method = typeof(Enumerable).GetMethods().Single(x => x.Name == "Count" && x.GetParameters().Length == 1);
            return Expression.Call(typeof(Enumerable), "Count", new[] { elementType }, collection);
        }

        public static Expression TryTranslate(Expression block, List<string> memberPath)
        {
            var e = Expression.Parameter(typeof(Exception), "e");
            var ctor = typeof(BitPackerTranslationException).GetConstructors()[0];
            var exception = Expression.New(ctor, Expression.Constant(memberPath), e);

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
    }
}
