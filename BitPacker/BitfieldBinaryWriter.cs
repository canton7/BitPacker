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
        private int scratchBitsInUse;
        private byte bitScratchpad;

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
            int numBitsLeftToWrite = numBits;
            int offsetInValueToWrite = 0;

            while (numBitsLeftToWrite > 0)
            {
                int bitsToWrite = Math.Min(sizeof(byte) - this.scratchBitsInUse, numBitsLeftToWrite);

                ulong mask = ~((~0UL) << bitsToWrite);
                // Bits to write, down in the least significant position
                byte valueToWrite = (byte)((value >> offsetInValueToWrite) & mask);
                byte shiftedValueToWrite = (byte)(valueToWrite << this.scratchBitsInUse);
                this.bitScratchpad |= shiftedValueToWrite;

                this.scratchBitsInUse += bitsToWrite;
                if (this.scratchBitsInUse == sizeof(byte))
                    this.FlushBitfield();

                numBitsLeftToWrite -= bitsToWrite;
                offsetInValueToWrite += bitsToWrite;
            }

        }

        public override void Flush()
        {
            this.FlushBitfield();
            base.Flush();
        }

        #region Write Overloads

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
