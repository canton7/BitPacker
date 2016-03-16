using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class BitfieldBinaryWriter : BinaryWriter
    {
        private readonly CountingStream stream;

        private int bitfieldSizeBytes;
        private BigInteger bitfieldContainer;
        private int bitfieldBitsInUse;

        public int BytesWritten { get { return this.stream.BytesWritten; } }

        public BitfieldBinaryWriter(CountingStream output)
            : base(output, Encoding.ASCII, true)
        {
            this.stream = output;
            this.bitfieldContainer = new BigInteger(0);
        }

        public void FlushContainer()
        {
            if (this.bitfieldSizeBytes == 0)
                return;

            var array = this.bitfieldContainer.ToByteArray();

            // Pad...
            if (array.Length < this.bitfieldSizeBytes)
            {
                for (var i = 0; i < (this.bitfieldSizeBytes - array.Length); i++)
                {
                    base.Write((byte)0);
                }
            }

            base.Write(array);

            this.bitfieldContainer = new BigInteger(0);
            this.bitfieldSizeBytes = 0;
            this.bitfieldBitsInUse = 0;
        }

        public void BeginBitfieldWrite(int bitfieldSizeBytes)
        {
            this.FlushContainer();
            this.bitfieldSizeBytes = bitfieldSizeBytes;
        }

        public void WriteBitfield(ulong value, int numBits)
        {
            if (this.bitfieldSizeBytes == 0)
                throw new InvalidOperationException("Bitfield write is not currently in progress");

            if (numBits <= 0)
                throw new ArgumentException("numBits must be > 0", "numBits");

            if (numBits > this.bitfieldSizeBytes * 8)
                throw new ArgumentException("Cannot have a number of bits to write which is greater than the container size");

            ulong mask = ~0UL << numBits;
            if ((mask & value) > 0)
                throw new ArgumentException("Value contains bits set above those permitted by numBits");

            // Can we write it to the same container?
            if ((this.bitfieldSizeBytes * 8 - this.bitfieldBitsInUse) < numBits)
                throw new InvalidOperationException("Tried to write too many bits to bitfield");

            this.bitfieldContainer = this.bitfieldContainer | (new BigInteger(value) << this.bitfieldBitsInUse);
            this.bitfieldBitsInUse += numBits;
        }

        private void EnsureBitfieldWriteNotInProgress()
        {
            if (this.bitfieldSizeBytes > 0)
                throw new InvalidOperationException("Bitfield write is currently in progress");
        }

        #region Overrides to call EnsureBitfieldWriteNotInProgress

        public override void Flush()
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Dispose(disposing);
        }

        public override void Write(bool value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(byte value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(char ch)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(ch);
        }

        public override void Write(sbyte value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(double value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(decimal value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(short value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(ushort value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(int value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(uint value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(long value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(ulong value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(float value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        public override void Write(byte[] buffer)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(buffer);
        }

        public override void Write(char[] chars)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(chars);
        }

        public override void Write(byte[] buffer, int index, int count)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(buffer, index, count);
        }

        public override void Write(char[] chars, int index, int count)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(chars, index, count);
        }

        public override void Write(string value)
        {
            this.EnsureBitfieldWriteNotInProgress();
            base.Write(value);
        }

        #endregion
    }
}
