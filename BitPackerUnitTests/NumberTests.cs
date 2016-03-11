using BitPacker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BitPackerUnitTests
{
    public class NumberTests
    {
        [BitPackerObject]
        private class HasNumericFields
        {
            [BitPackerMember]
            public byte ByteField { get; set; }

            [BitPackerMember]
            public char CharField { get; set; }

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

            [BitPackerMember]
            public float FloatField { get; set; }

            [BitPackerMember]
            public double DoubleField { get; set; }

            [BitPackerMember]
            public decimal DecimalField { get; set; }
        }

        private readonly HasNumericFields cls;

        public NumberTests()
        {
            unchecked
            {
                this.cls = new HasNumericFields()
                {
                    ByteField = 0x01,
                    CharField = 'a',
                    UInt16Field = 0xF203,
                    Int16Field = (short)0xF405U,
                    UInt32Field = 0xF6070809,
                    Int32Field = (int)0xFA0B0C0DU,
                    UInt64Field = 0xFE0F101112131415,
                    Int64Field = (long)0xF61718191A1B1C1DU,
                    FloatField = 1.234f,
                    DoubleField = 5.678,
                    DecimalField = 123.456m,
                };
            }
        }

        [Fact]
        public void SerializesLittleEndianCorrectly()
        {
            var serializer = new BitPackerSerializer<HasNumericFields>(Endianness.LittleEndian);
            var bytes = serializer.Serialize(this.cls);
            var expected = new byte[]
            {
                0x01,
                0x61,
                0x03, 0xF2,
                0x05, 0xF4,
                0x09, 0x08, 0x07, 0xF6,
                0x0D, 0x0C, 0x0B, 0xFA,
                0x15, 0x14, 0x13, 0x12, 0x11, 0x10, 0x0F, 0xFE,
                0x1D, 0x1C, 0x1B, 0x1A, 0x19, 0x18, 0x17, 0xF6,
                0xB6, 0xF3, 0x9D, 0x3F,
                0x83, 0xC0, 0xCA, 0xA1, 0x45, 0xB6, 0x16, 0x40,
                0x40, 0xE2, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00,

            };
            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void SerializesBigEndianCorrectly()
        {
            var serializer = new BitPackerSerializer<HasNumericFields>(Endianness.BigEndian);
            var bytes = serializer.Serialize(this.cls);
            var expected = new byte[]
            {
                0x01,
                0x61,
                0xF2, 0x03,
                0xF4, 0x05,
                0xF6, 0x07, 0x08, 0x09,
                0xFA, 0x0B, 0x0C, 0x0D,
                0xFE, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15,
                0xF6, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D,
                0x3F, 0x9D, 0xF3, 0xB6,
                0x40, 0x16, 0xB6, 0x45, 0xA1, 0xCA, 0xC0, 0x83,
                0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0xE2, 0x40,
            };
            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void DeserializesLittleEndianCorrectly()
        {
            var deserializer = new BitPackerDeserializer<HasNumericFields>(Endianness.LittleEndian);
            var bytes = new byte[]
            {
                0x01,
                0x61,
                0x03, 0x02,
                0x05, 0x04,
                0x09, 0x08, 0x07, 0x06,
                0x0D, 0x0C, 0x0B, 0x0A,
                0x15, 0x14, 0x13, 0x12, 0x11, 0x10, 0x0F, 0x0E,
                0x1D, 0x1C, 0x1B, 0x1A, 0x19, 0x18, 0x17, 0x16,
                0xB6, 0xF3, 0x9D, 0x3F,
                0x83, 0xC0, 0xCA, 0xA1, 0x45, 0xB6, 0x16, 0x40,
                0x40, 0xE2, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00,
            };

            var cls = deserializer.Deserialize(bytes);

            Assert.Equal(0x01, cls.ByteField);
            Assert.Equal('a', cls.CharField);
            Assert.Equal(0x0203, cls.UInt16Field);
            Assert.Equal(0x0405, cls.Int16Field);
            Assert.Equal(0x06070809U, cls.UInt32Field);
            Assert.Equal(0x0A0B0C0D, cls.Int32Field);
            Assert.Equal(0x0E0F101112131415U, cls.UInt64Field);
            Assert.Equal(0x161718191A1B1C1D, cls.Int64Field);
            Assert.Equal(1.234f, cls.FloatField);
            Assert.Equal(5.678, cls.DoubleField);
            Assert.Equal(123.456m, cls.DecimalField);
        }

        [Fact]
        public void DeserializesBigEndianCorrectly()
        {
            var deserializer = new BitPackerDeserializer<HasNumericFields>(Endianness.BigEndian);
            var bytes = new byte[]
            {
                0x01,
                0x61,
                0x02, 0x03,
                0x04, 0x05,
                0x06, 0x07, 0x08, 0x09,
                0x0A, 0x0B, 0x0C, 0x0D,
                0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15,
                0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D,
                0x3F, 0x9D, 0xF3, 0xB6,
                0x40, 0x16, 0xB6, 0x45, 0xA1, 0xCA, 0xC0, 0x83,
                0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0xE2, 0x40,
            };

            var cls = deserializer.Deserialize(bytes);

            Assert.Equal(0x01, cls.ByteField);
            Assert.Equal('a', cls.CharField);
            Assert.Equal(0x0203, cls.UInt16Field);
            Assert.Equal(0x0405, cls.Int16Field);
            Assert.Equal(0x06070809U, cls.UInt32Field);
            Assert.Equal(0x0A0B0C0D, cls.Int32Field);
            Assert.Equal(0x0E0F101112131415U, cls.UInt64Field);
            Assert.Equal(0x161718191A1B1C1D, cls.Int64Field);
            Assert.Equal(1.234f, cls.FloatField);
            Assert.Equal(5.678, cls.DoubleField);
            Assert.Equal(123.456m, cls.DecimalField);
        }
    }
}
