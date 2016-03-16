using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public class BitfieldSizeTracker
    {
        public int TotalSize { get; private set; }

        private int containerSize;
        private int containerBitsInUse;

        public void Add(int containerSize, int numBits, bool padAfter)
        {
            // Different size, or would overflow? Flush the container
            if (containerSize != this.containerSize || (this.containerSize * 8 - this.containerBitsInUse) < numBits)
            {
                this.FlushContainer(containerSize);
            }

            this.containerBitsInUse += numBits;

            // Pad after? Flush the container
            // We can end up flushing twice: once because we would have overflowed, and another because we pad after
            if (padAfter)
            {
                this.FlushContainer(containerSize);
            }
        }

        public void FlushContainer(int containerSize)
        {
            this.TotalSize += this.containerSize;
            this.containerSize = containerSize;
            this.containerBitsInUse = 0;
        }
    }
}
