using BitPacker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BitPackerUnitTests
{
    public class IntegerTests
    {
        [BitPackerObject]
        private class HasIntegerFields
        {
            [BitPackerMember]
            public byte ByteField { get; set; }

            [BitPackerMember]
            public ushort UInt16Field { get; set; }

            [BitPackerMember]
            public short Int16Field { get; set; }

            [BitPackerMember]
            public uint UInt32Field { get; set; }

            [BitPackerMember]
            public int Int32Field { get; set; }

            [BitPackerMember]
            public ulong UInt64Field { get; set; }

            [BitPackerMember]
            public long Int64Field { get; set; }
        }

        private readonly HasIntegerFields cls;

        public IntegerTests()
        {
            this.cls = new HasIntegerFields()
            {
                ByteField = 0x01,
                UInt16Field = 0x0203,
                Int16Field = 0x0405,
                UInt32Field = 0x06070809,
                Int32Field = 0x0A0B0C0D,
                UInt64Field = 0x0E0F101112131415,
                Int64Field = 0x161718191A1B1C1D,
            };
        }

        [Fact]
        public void SerializesLittleEndianCorrectly()
        {
            var serializer = new BitPackerSerializer<HasIntegerFields>(Endianness.LittleEndian);
            var bytes = serializer.Serialize(this.cls);
            var expected = new byte[]
            {
                0x01,
                0x03, 0x02,
                0x05, 0x04,
                0x09, 0x08, 0x07, 0x06,
                0x0D, 0x0C, 0x0B, 0x0A,
                0x15, 0x14, 0x13, 0x12, 0x11, 0x10, 0x0F, 0x0E,
                0x1D, 0x1C, 0x1B, 0x1A, 0x19, 0x18, 0x17, 0x16
            };
            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void SerializesBigEndianCorrectly()
        {
            var serializer = new BitPackerSerializer<HasIntegerFields>(Endianness.BigEndian);
            var bytes = serializer.Serialize(this.cls);
            var expected = new byte[]
            {
                0x01,
                0x02, 0x03,
                0x04, 0x05,
                0x06, 0x07, 0x08, 0x09,
                0x0A, 0x0B, 0x0C, 0x0D,
                0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15,
                0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D
            };
            Assert.Equal(expected, bytes);
        }
    }
}
