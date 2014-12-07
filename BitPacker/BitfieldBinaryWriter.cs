using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public class BitfieldBinaryWriter : BinaryWriter
    {
        private int scratchBitsInUse;
        private byte bitScratchpad;

        public BitfieldBinaryWriter(Stream output) : base(output)
        { }

        private void FlushBitfield()
        {
            if (this.scratchBitsInUse == 0)
                return;

            base.Write(this.bitScratchpad);

            this.scratchBitsInUse = 0;
            this.bitScratchpad = 0;
        }

        public void WriteBitfield(ulong value, int numBits)
        {
            ulong mask = ~0UL << numBits;
            if ((mask & value) > 0)
                throw new ArgumentException("Value contains bits set above those permitted by numBits");
            
            int numBitsLeftToWrite = numBits;
            int offsetInValueToWrite = 0;


            while (numBitsLeftToWrite > 0)
            {
                int bitsToWrite = Math.Min(8 - this.scratchBitsInUse, numBitsLeftToWrite);

                // We've already guarenteed that they don't have any bits set above numBits
                byte valueToWrite = (byte)((byte)(value >> offsetInValueToWrite) << this.scratchBitsInUse);
                this.bitScratchpad |= valueToWrite;

                this.scratchBitsInUse += bitsToWrite;
                if (this.scratchBitsInUse == 8)
                    this.FlushBitfield();

                numBitsLeftToWrite -= bitsToWrite;
                offsetInValueToWrite += bitsToWrite;
            }
        }

        #region Overrides to call FlushBitfield

        public override void Flush()
        {
            this.FlushBitfield();
            base.Flush();
        }

        public override void Write(bool value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(byte value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(char ch)
        {
            this.FlushBitfield();
            base.Write(ch);
        }

        public override void Write(sbyte value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(double value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(decimal value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(short value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(ushort value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(int value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(uint value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(long value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(ulong value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(float value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        public override void Write(byte[] buffer)
        {
            this.FlushBitfield();
            base.Write(buffer);
        }

        public override void Write(char[] chars)
        {
            this.FlushBitfield();
            base.Write(chars);
        }

        public override void Write(byte[] buffer, int index, int count)
        {
            this.FlushBitfield();
            base.Write(buffer, index, count);
        }

        public override void Write(char[] chars, int index, int count)
        {
            this.FlushBitfield();
            base.Write(chars, index, count);
        }

        public override void Write(string value)
        {
            this.FlushBitfield();
            base.Write(value);
        }

        #endregion
    }
}
