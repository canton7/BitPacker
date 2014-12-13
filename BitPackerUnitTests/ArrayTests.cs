using BitPacker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BitPackerUnitTests
{
    public class ArrayTests
    {
        [BitPackerObject]
        private class HasArrayWithoutArrayAttribute
        {
            [BitPackerMember]
            public int[] IntArray { get; set; }
        }

        [BitPackerObject]
        private class HasFixedLengthArray
        {
            [BitPackerArray(Length = 5)]
            public int[] IntArray { get; set; }
        }

        [Fact]
        public void ThrowsIfArrayNotDecoratedWithPitPackerArrayAttribute()
        {
            Assert.Throws<InvalidAttributeException>(() => new BitPackerSerializer<HasArrayWithoutArrayAttribute>());
        }
    }
}
