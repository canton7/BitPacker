﻿using BitPacker;
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
    }
}
