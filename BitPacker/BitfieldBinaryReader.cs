using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class BitfieldBinaryReader : BinaryReader
    {
        private readonly CountingStream stream;

        private BigInteger bitfieldContainer;
        private int bitfieldBitsInUse;

        public int BytesRead { get { return this.stream.BytesRead; } }

        public BitfieldBinaryReader(CountingStream input)
            : base(input, Encoding.ASCII, true)
        {
            this.stream = input;
        }

        public void FlushContainer()
        {
            // Just mark it as empty...
            this.bitfieldBitsInUse = 0;
        }

        public void BeginBitfieldRead(int bitfieldSizeBytes)
        {
            var bytes = base.ReadBytes(bitfieldSizeBytes);
            this.bitfieldContainer = new BigInteger(bytes.Reverse().ToArray());
            this.bitfieldBitsInUse = bitfieldSizeBytes * 8;
        }

        public ulong ReadBitfield(int numBits)
        {
            if (this.bitfieldBitsInUse == 0)
                throw new InvalidOperationException("Bitfield read not currently in progress");

            if (numBits > this.bitfieldBitsInUse)
                throw new ArgumentException("Cannot read that many bits, as the conatiner doesn't contain that many", "numBits");

            // Read from the bottom up to the top
            ulong mask = ~(~0UL << numBits);
            var output = this.bitfieldContainer & new BigInteger(mask);
            this.bitfieldContainer >>= numBits;
            this.bitfieldBitsInUse -= numBits;
            return (ulong)output;
        }

        private void EnsureBitfieldReadNotInProgress()
        {
            if (this.bitfieldBitsInUse > 0)
                throw new InvalidOperationException("Bitfield read is currently in progress");
        }

        #region Overrides to call EnsureBitfieldReadNotInProgress

        public override int Read()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.Read();
        }

        public override int Read(byte[] buffer, int index, int count)
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.Read(buffer, index, count);
        }

        public override int Read(char[] buffer, int index, int count)
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.Read(buffer, index, count);
        }

        public override bool ReadBoolean()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadBoolean();
        }

        public override byte ReadByte()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadByte();
        }

        public override byte[] ReadBytes(int count)
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadBytes(count);
        }

        public override char ReadChar()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadChar();
        }

        public override char[] ReadChars(int count)
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadChars(count);
        }

        public override decimal ReadDecimal()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadDecimal();
        }

        public override double ReadDouble()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadDouble();
        }

        public override short ReadInt16()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadInt16();
        }

        public override int ReadInt32()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadInt32();
        }

        public override long ReadInt64()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadInt64();
        }

        public override sbyte ReadSByte()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadSByte();
        }

        public override float ReadSingle()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadSingle();
        }

        public override string ReadString()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadString();
        }

        public override ushort ReadUInt16()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadUInt16();
        }

        public override uint ReadUInt32()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadUInt32();
        }

        public override ulong ReadUInt64()
        {
            this.EnsureBitfieldReadNotInProgress();
            return base.ReadUInt64();
        }

        #endregion
    }
}
