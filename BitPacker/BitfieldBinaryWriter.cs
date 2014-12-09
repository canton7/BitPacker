using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class BitfieldBinaryWriter : BinaryWriter
    {
        private ulong container;
        private int containerSize; // In bytes
        private int containerBitsInUse;
        private bool swapContainerEndianness;

        public BitfieldBinaryWriter(Stream output) : base(output, Encoding.UTF8, true)
        { }

        private void FlushContainer()
        {
            if (this.containerBitsInUse > 0)
            {
                if (this.swapContainerEndianness)
                {
                    for (int i = containerSize - 1; i >= 0; i--)
                    {
                        base.Write((byte)(this.container >> (i * 8)));
                    }
                }
                else
                {
                    for (int i = 0; i < this.containerSize; i++)
                    {
                        base.Write((byte)(this.container >> (i * 8)));
                    }
                }
            }

            this.container = 0;
            this.containerBitsInUse = 0;
            this.containerSize = 0;
        }

        public void WriteBitfield(ulong value, int containerSize, int numBits, bool swapContainerEndianness)
        {
            // Special-case
            if (numBits == 0)
            {
                this.FlushContainer();
                return;
            }

            if (numBits > containerSize * 8)
                throw new ArgumentException("Cannot have a number of bits to write which is greater than the container size");

            ulong mask = ~0UL << numBits;
            if ((mask & value) > 0)
                throw new ArgumentException("Value contains bits set above those permitted by numBits");

            // Can we write it to the same container?
            if (containerSize != this.containerSize || (this.containerSize * 8 - this.containerBitsInUse) < numBits)
                this.FlushContainer();

            // Do we have conflicting endianness, if there's an existing container?
            if (this.containerSize > 0 && this.swapContainerEndianness != swapContainerEndianness)
                throw new Exception("Cannot have mixed endianness among different fields in the same bitfield container");

            // Either this is a no-op, or we're setting up the container for its first write
            this.containerSize = containerSize;

            // Again, either no-op or first-time setup
            this.swapContainerEndianness = swapContainerEndianness;

            ulong valueToWrite = value << this.containerBitsInUse;
            this.container |= valueToWrite;

            this.containerBitsInUse += numBits;
        }

        #region Overrides to call FlushContainer

        public override void Flush()
        {
            this.FlushContainer();
            base.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            this.FlushContainer();
            base.Dispose(disposing);
        }

        public override void Write(bool value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(byte value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(char ch)
        {
            this.FlushContainer();
            base.Write(ch);
        }

        public override void Write(sbyte value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(double value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(decimal value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(short value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(ushort value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(int value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(uint value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(long value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(ulong value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(float value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        public override void Write(byte[] buffer)
        {
            this.FlushContainer();
            base.Write(buffer);
        }

        public override void Write(char[] chars)
        {
            this.FlushContainer();
            base.Write(chars);
        }

        public override void Write(byte[] buffer, int index, int count)
        {
            this.FlushContainer();
            base.Write(buffer, index, count);
        }

        public override void Write(char[] chars, int index, int count)
        {
            this.FlushContainer();
            base.Write(chars, index, count);
        }

        public override void Write(string value)
        {
            this.FlushContainer();
            base.Write(value);
        }

        #endregion
    }
}
