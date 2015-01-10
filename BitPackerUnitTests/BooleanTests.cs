using BitPacker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BitPackerUnitTests
{
    public class BooleanTests
    {
        [BitPackerObject]
        private class HasBooleanFields
        {
            [BitPackerMember]
            public bool BooleanField { get; set; }

            [BitPackerBoolean]
            public bool ExplicitBooleanField { get; set; }

            [BitPackerBoolean(Type = typeof(byte))]
            public bool ByteField { get; set; }

            [BitPackerBoolean(Type = typeof(ushort))]
            public bool UInt16Field { get; set; }

            [BitPackerBoolean(Type = typeof(short))]
            public bool Int16Field { get; set; }

            [BitPackerBoolean(Type = typeof(uint))]
            public bool UInt32Field { get; set; }

            [BitPackerBoolean(Type = typeof(int))]
            public bool Int32Field { get; set; }

            [BitPackerBoolean(Type = typeof(ulong))]
            public bool UInt64Field { get; set; }

            [BitPackerBoolean(Type = typeof(long))]
            public bool Int64Field { get; set; }
        }

        [BitPackerObject]
        private class HasInvalidType
        {
            [BitPackerBoolean(Type = typeof(object))]
            public bool Field { get; set; }
        }

        [BitPackerObject]
        private class HasBooleanAttributeOnNonBooleanType
        {
            [BitPackerBoolean]
            public int Field { get; set; }
        }

        [Fact]
        public void BooleanWithInvalidTypeFails()
        {
            var e1 = Assert.Throws<InvalidEquivalentTypeException>(() => new BitPackerSerializer<HasInvalidType>());
            Assert.Equal("HasInvalidType.Field", e1.Property);

            var e2 = Assert.Throws<InvalidEquivalentTypeException>(() => new BitPackerDeserializer<HasInvalidType>());
            Assert.Equal("HasInvalidType.Field", e2.Property);
        }

        [Fact]
        public void BooleanAttributeOnNonBooleanFieldFails()
        {
            var e1 = Assert.Throws<InvalidAttributeException>(() => new BitPackerSerializer<HasBooleanAttributeOnNonBooleanType>());
            Assert.Equal("HasBooleanAttributeOnNonBooleanType.Field", e1.Property);

            var e2 = Assert.Throws<InvalidAttributeException>(() => new BitPackerDeserializer<HasBooleanAttributeOnNonBooleanType>());
            Assert.Equal("HasBooleanAttributeOnNonBooleanType.Field", e2.Property);
        }

        [Fact]
        public void SerializationOfTrueBooleansBigEndianSucceeds()
        {
            var cls = new HasBooleanFields()
            {
                BooleanField = true,
                ExplicitBooleanField = true,
                ByteField = true,
                UInt16Field = true,
                Int16Field = true,
                UInt32Field = true,
                Int32Field = true,
                UInt64Field = true,
                Int64Field = true
            };

            var serializer = new BitPackerSerializer<HasBooleanFields>(Endianness.BigEndian);
            var result = serializer.Serialize(cls);

            var expectedResult = new byte[]
            {
                0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x00, 0x01,
                0x01,
                0x00, 0x01,
                0x00, 0x01,
                0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            };

            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void DeserializationOfTrueBooleansLittleEndianSucceeds()
        {
            var bytes = new byte[]
            {
                0x00, 0x00, 0x00, 0x01,
                0xFF, 0xFF, 0xFF, 0xFF,
                0x05,
                0x01, 0x00,
                0x80, 0x00,
                0x01, 0x02, 0x03, 0x04,
                0xFF, 0xFF, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
            };

            var deserializer = new BitPackerDeserializer<HasBooleanFields>(Endianness.BigEndian);
            var deserialized = deserializer.Deserialize(bytes);

            Assert.True(deserialized.BooleanField);
            Assert.True(deserialized.ExplicitBooleanField);
            Assert.True(deserialized.ByteField);
            Assert.True(deserialized.UInt16Field);
            Assert.True(deserialized.Int16Field);
            Assert.True(deserialized.UInt32Field);
            Assert.True(deserialized.Int32Field);
            Assert.True(deserialized.UInt64Field);
            Assert.True(deserialized.Int64Field);
        }
    }
}
