using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal abstract class ImmutableStack<T> : IEnumerable<T>, IEnumerable
    {
        public abstract ImmutableStack<T> Push(T value);
        public abstract ImmutableStack<T> Pop();
        public abstract T Peek();
        public abstract bool IsEmpty { get; }

        private sealed class EmptyStack : ImmutableStack<T>
        {
            public override bool IsEmpty
            {
                get { return true; }
            }

            public override T Peek()
            {
                throw new Exception("Empty stack");
            }

            public override ImmutableStack<T> Push(T value)
            {
                return new ImmutableStack<T>.NonEmptyStack(value, this);
            }

            public override ImmutableStack<T> Pop()
            {
                throw new Exception("Empty stack");
            }
        }

        private sealed class NonEmptyStack : ImmutableStack<T>
        {
            private readonly T head;
            private readonly ImmutableStack<T> tail;

            public override bool IsEmpty
            {
                get { return false; }
            }

            public NonEmptyStack(T head, ImmutableStack<T> tail)
            {
                this.head = head;
                this.tail = tail;
            }

            public override T Peek()
            {
                return head;
            }

            public override ImmutableStack<T> Pop()
            {
                return tail;
            }

            public override ImmutableStack<T> Push(T value)
            {
                return new ImmutableStack<T>.NonEmptyStack(value, this);
            }
        }

        private static readonly EmptyStack empty = new EmptyStack();

        public static ImmutableStack<T> Empty
        {
            get { return empty; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (ImmutableStack<T> stack = this; !stack.IsEmpty ; stack = stack.Pop())
                yield return stack.Peek();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

    internal static class ImmutableStack
    {
        public static ImmutableStack<T> Init<T>(T initialValue)
        {
            return ImmutableStack<T>.Empty.Push(initialValue);
        }

        public static ImmutableStack<T> From<T>(IEnumerable<T> initialValues)
        {
            var stack = ImmutableStack<T>.Empty;
            foreach (var initialValue in initialValues)
            {
                stack = stack.Push(initialValue);
            }
            return stack;
        }
    }
}
