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

        [BitPackerObject]
        private class HasVariableLengthArray
        {
            [BitPackerLengthKey(LengthKey = "foo")]
            public ushort Length { get; set; }

            [BitPackerArray(LengthKey = "foo")]
            public int[] Array { get; set; }
        }

        [BitPackerObject]
        private class HasVariableLengthArrayButNoLengthField
        {
            [BitPackerArray(LengthKey = "key")]
            public int[] IntArray { get; set; }
        }

        [BitPackerObject]
        private class HasLengthFieldButNoVariableLengthArray
        {
            [BitPackerLengthKey(LengthKey = "key")]
            public int ArrayLength { get; set; }
        }

        [BitPackerObject]
        private class HasLengthFieldAndFixedLengthArray
        {
            [BitPackerLengthKey(LengthKey = "key")]
            public int Length { get; set; }

            [BitPackerArray(Length = 5, LengthKey = "key")]
            public int[] Array { get; set; }
        }

        [BitPackerObject]
        private class HasTwoLengthFieldsForOneArray
        {
            [BitPackerLengthKey(LengthKey = "key")]
            public int Length1 { get; set; }

            [BitPackerLengthKey(LengthKey = "key")]
            public int Length2 { get; set; }

            [BitPackerArray(LengthKey = "key")]
            public int[] IntArray { get; set; }
        }

        [Fact]
        public void ThrowsIfArrayNotDecoratedWithPitPackerArrayAttribute()
        {
            var e = Assert.Throws<InvalidAttributeException>(() => new BitPackerSerializer<HasArrayWithoutArrayAttribute>());
            Assert.Equal("HasArrayWithoutArrayAttribute.IntArray", e.Property);
        }

        [Fact]
        public void ThrowsIfFixedLengthArrayIsTooLong()
        {
            var cls = new HasFixedLengthArray()
            {
                IntArray = new[] {  1, 2, 3, 4, 5, 6 }
            };
            var serializer = new BitPackerSerializer<HasFixedLengthArray>();
            var e = Assert.Throws<BitPackerTranslationException>(() => serializer.Serialize(cls));
            Assert.Equal(new[] { "IntArray" }, e.MemberPath.ToArray());
        }

        [Fact]
        public void SerializesFixedLengthArrayWithPadding()
        {
            var cls = new HasFixedLengthArray()
            {
                IntArray = new[] { 1, 2, 3 }
            };
            var serializer = new BitPackerSerializer<HasFixedLengthArray>(Endianness.LittleEndian);
            var bytes = serializer.Serialize(cls);
            var expected = new byte[]
            {
                0x01, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00,
                0x03, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
            };
            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void DeserializesFixedLengthArrayWithPadding()
        {
            var bytes = new byte[]
            {
                0x00, 0x00, 0x00, 0x0A,
                0x00, 0x00, 0x00, 0x0B,
                0x00, 0x00, 0x00, 0x0C,
                0x00, 0x00, 0x00, 0x0D,
                0x00, 0x00, 0x00, 0x00,
            };
            var deserializer = new BitPackerDeserializer<HasFixedLengthArray>(Endianness.BigEndian);
            var cls = deserializer.Deserialize(bytes);
            var expected = new[] { 0x0A, 0x0B, 0x0C, 0x0D, 0x00 };
            Assert.Equal(expected, cls.IntArray);
        }

        [Fact]
        public void SerializesVariableLengthArray()
        {
            var serializer = new BitPackerSerializer<HasVariableLengthArray>();
            var bytes = serializer.Serialize(new HasVariableLengthArray() { Array = new[] { 1, 2, 3 } });
            var expected = new byte[]
            {
                0x00, 0x03,
                0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x00, 0x02,
                0x00, 0x00, 0x00, 0x03,
            };
            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void DeserializesVariableLengthArray()
        {
            var deserializer = new BitPackerDeserializer<HasVariableLengthArray>(Endianness.LittleEndian);
            var bytes = new byte[]
            {
                0x03, 0x00,
                0x01, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00,
                0x03, 0x00, 0x00, 0x00
            };
            var cls = deserializer.Deserialize(bytes);

            Assert.Equal(3, cls.Length);
            Assert.Equal(new[] { 1, 2, 3 }, cls.Array);
        }

        [Fact]
        public void IgnoresExistingLengthFieldValueWhenSerializing()
        {
            var serializer = new BitPackerSerializer<HasVariableLengthArray>();
            var bytes = serializer.Serialize(new HasVariableLengthArray() { Length = 3, Array = new int[] { 1, 2, } });
            var expected = new byte[]
            {
                0x00, 0x02,
                0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x00, 0x02,
            };
            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void SerializationOfVariableLengthArrayWithNoLengthFieldSucceeds()
        {
            // Does not throw
            new BitPackerSerializer<HasVariableLengthArrayButNoLengthField>();
        }

        [Fact]
        public void DeserializationOfVariableLengthArrayWithNoLengthFieldFails()
        {
            var e = Assert.Throws<BitPackerTranslationException>(() => new BitPackerDeserializer<HasVariableLengthArrayButNoLengthField>());
            Assert.Equal(new[] { "IntArray" }, e.MemberPath.ToArray());
            Assert.IsType<InvalidArraySetupException>(e.InnerException);
        }

        [Fact]
        public void SerializationOfObjectWithLengthFieldButNoCorrespondingArrayFails()
        {
            Assert.Throws<InvalidArraySetupException>(() => new BitPackerSerializer<HasLengthFieldButNoVariableLengthArray>());
        }

        [Fact]
        public void DeserializationOfObjectWithLengthFieldButNoCorrespondingArraySucceeds()
        {
            // Does not throw
            new BitPackerDeserializer<HasLengthFieldButNoVariableLengthArray>();
        }

        [Fact]
        public void SerializesVariableLengthArrayWithPadding()
        {
            var cls = new HasLengthFieldAndFixedLengthArray
            {
                Array = new[] { 1, 2, 3 }
            };
            var serializer = new BitPackerSerializer<HasLengthFieldAndFixedLengthArray>();
            var bytes = serializer.Serialize(cls);
            var expected = new byte[]
            {
                0x00, 0x00, 0x00, 0x03,
                0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x00, 0x02,
                0x00, 0x00, 0x00, 0x03,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
            };
            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void DeserializesVariableLengthArrayWithPadding()
        {
            var bytes = new byte[]
            {
                0x00, 0x00, 0x00, 0x03,
                0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x00, 0x02,
                0x00, 0x00, 0x00, 0x03,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
            };
            var deserializer = new BitPackerDeserializer<HasLengthFieldAndFixedLengthArray>();
            var cls = deserializer.Deserialize(bytes);

            Assert.Equal(3, cls.Length);
            Assert.Equal(new[] { 1, 2, 3 }, cls.Array);
        }

        [Fact]
        public void SerializationOfObjectWithTwoLengthFieldsForOneArrayFails()
        {
            Assert.Throws<InvalidArraySetupException>(() => new BitPackerSerializer<HasTwoLengthFieldsForOneArray>());
        }

        [Fact]
        public void DeserializationOfObjectWithTwoLengthFieldsForOneArrayFails()
        {
            Assert.Throws<InvalidArraySetupException>(() => new BitPackerDeserializer<HasTwoLengthFieldsForOneArray>());
        }
    }
}
