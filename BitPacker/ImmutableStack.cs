using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal interface IImmutableStack<T> : IEnumerable<T>
    {
        IImmutableStack<T> Push(T value);
        IImmutableStack<T> Pop();
        T Peek();
        bool IsEmpty { get; }
    }

    internal class ImmutableStack<T> : IImmutableStack<T>
    {
        private sealed class EmptyStack : IImmutableStack<T>
        {
            public bool IsEmpty
            {
                get { return true; }
            }

            public T Peek()
            {
                throw new Exception("Empty stack");
            }

            public IImmutableStack<T> Push(T value)
            {
                return new ImmutableStack<T>(value, this);
            }

            public IImmutableStack<T> Pop()
            {
                throw new Exception("Empty stack");
            }

            public IEnumerator<T> GetEnumerator()
            {
                yield break;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        private static readonly EmptyStack empty = new EmptyStack();
        private readonly T head;
        private readonly IImmutableStack<T> tail;

        public static IImmutableStack<T> Empty
        {
            get { return empty; }
        }

        private ImmutableStack(T head, IImmutableStack<T> tail)
        {
            this.head = head;
            this.tail = tail;
        }

        public ImmutableStack(T head)
        {
            this.head = head;
            this.tail = ImmutableStack<T>.Empty;
        }

        public bool IsEmpty
        {
            get { return false; }
        }

        public T Peek()
        {
            return head;
        }

        public IImmutableStack<T> Pop()
        {
            return tail;
        }

        public IImmutableStack<T> Push(T value)
        {
            return new ImmutableStack<T>(value, this);
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (IImmutableStack<T> stack = this; !stack.IsEmpty ; stack = stack.Pop())
                yield return stack.Peek();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
