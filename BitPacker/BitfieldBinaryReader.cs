using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class BitfieldBinaryReader : BinaryReader
    {
        private readonly CountingStream stream;

        private ulong container;
        private int containerSize; // Bytes
        private int containerBitsInUse;
        private bool swapContainerEndianness;

        public int BytesRead { get { return this.stream.BytesRead; } }

        public BitfieldBinaryReader(CountingStream input)
            : base(input, Encoding.UTF8, true)
        {
            this.stream = input;
        }


        public void FlushContainer()
        {
            // Just mark it as empty...
            this.container = 0;
            this.containerSize = 0;
            this.containerBitsInUse = 0;
        }

        public ulong ReadBitfield(int containerSize, int numBits, bool swapContainerEndianness)
        {
            if (numBits > containerSize * 8)
                throw new ArgumentException("Cannot have a number of bits to write which is greater than the container size");

            // Can it read from the same container?
            if (containerSize != this.containerSize || this.containerBitsInUse < numBits)
                this.FlushContainer();

            // Do we have conflicting endianness, if there's an existing container?
            if (this.containerSize > 0 && this.swapContainerEndianness != swapContainerEndianness)
                throw new Exception("Cannot have mixed endianness among different fields in the same bitfield container");

            // Do we need to read into the container? Do it if so
            if (this.containerSize == 0)
            {
                this.containerSize = containerSize;
                this.swapContainerEndianness = swapContainerEndianness;
                this.containerBitsInUse = this.containerSize * 8;

                if (this.swapContainerEndianness)
                {
                    for (int i = this.containerSize - 1; i >= 0; i--)
                    {
                        this.container |= (ulong)((ulong)base.ReadByte() << (i * 8));
                    }
                }
                else
                {
                    for (int i = 0; i < this.containerSize; i++)
                    {
                        this.container |= (ulong)((ulong)base.ReadByte() << (i * 8));
                    }
                }
            }

            ulong mask = ~(~0UL << numBits);
            ulong value = this.container & mask;
            this.container >>= numBits;
            this.containerBitsInUse -= numBits;

            return value;
        }

        #region Overrides to call FlushContainer

        public override int Read()
        {
            this.FlushContainer();
            return base.Read();
        }

        public override int Read(byte[] buffer, int index, int count)
        {
            this.FlushContainer();
            return base.Read(buffer, index, count);
        }

        public override int Read(char[] buffer, int index, int count)
        {
            this.FlushContainer();
            return base.Read(buffer, index, count);
        }

        public override bool ReadBoolean()
        {
            this.FlushContainer();
            return base.ReadBoolean();
        }

        public override byte ReadByte()
        {
            this.FlushContainer();
            return base.ReadByte();
        }

        public override byte[] ReadBytes(int count)
        {
            this.FlushContainer();
            return base.ReadBytes(count);
        }

        public override char ReadChar()
        {
            this.FlushContainer();
            return base.ReadChar();
        }

        public override char[] ReadChars(int count)
        {
            this.FlushContainer();
            return base.ReadChars(count);
        }

        public override decimal ReadDecimal()
        {
            this.FlushContainer();
            return base.ReadDecimal();
        }

        public override double ReadDouble()
        {
            this.FlushContainer();
            return base.ReadDouble();
        }

        public override short ReadInt16()
        {
            this.FlushContainer();
            return base.ReadInt16();
        }

        public override int ReadInt32()
        {
            this.FlushContainer();
            return base.ReadInt32();
        }

        public override long ReadInt64()
        {
            this.FlushContainer();
            return base.ReadInt64();
        }

        public override sbyte ReadSByte()
        {
            this.FlushContainer();
            return base.ReadSByte();
        }

        public override float ReadSingle()
        {
            this.FlushContainer();
            return base.ReadSingle();
        }

        public override string ReadString()
        {
            this.FlushContainer();
            return base.ReadString();
        }

        public override ushort ReadUInt16()
        {
            this.FlushContainer();
            return base.ReadUInt16();
        }

        public override uint ReadUInt32()
        {
            this.FlushContainer();
            return base.ReadUInt32();
        }

        public override ulong ReadUInt64()
        {
            this.FlushContainer();
            return base.ReadUInt64();
        }

        #endregion
    }
}
