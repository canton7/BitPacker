using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitPacker;
using Xunit;

namespace BitPackerUnitTests
{
    public class StringTests
    {
        [BitPackerObject]
        private class HasNullTerminatedString
        {
            [BitPackerString(NullTerminated = true)]
            public string String { get; set; }
        }

        [BitPackerObject]
        private class HasNoLengthAndNoLengthKey
        {
            [BitPackerString(Encoding = "ASCII")]
            public string String { get; set; }
        }

        [BitPackerObject]
        private class HasNullTerminatedUtf16String
        {
            [BitPackerString(Encoding = "UTF-16", NullTerminated = true)]
            public string String { get; set; }
        }

        [BitPackerObject]
        private class HasVariableLengthUtf16String
        {
            [BitPackerLengthKey(LengthKey = "length")]
            public ushort Length { get; set; }

            [BitPackerString(Encoding = "UTF-16", LengthKey = "length")]
            public string String { get; set; }
        }

        [BitPackerObject]
        private class HasFixedLengthUtf8String
        {
            [BitPackerString(Encoding = "UTF-8", Length = 5)]
            public string String { get; set; }
        }

        [BitPackerObject]
        private class HasFixedLengthUtf16String
        {
            [BitPackerString(Encoding = "UTF-16", Length = 6)]
            public string String { get; set; }
        }

        [Fact]
        public void SerializesNullTerminatedString()
        {
            var serializer = new BitPackerSerializer<HasNullTerminatedString>();
            var bytes = serializer.Serialize(new HasNullTerminatedString() { String = "bar" });
            Assert.Equal(new byte[] { 98, 97, 114, 0 }, bytes);
        }

        [Fact]
        public void DeserializesNullTerminatedString()
        {
            var deserializer = new BitPackerDeserializer<HasNullTerminatedString>();
            var cls = deserializer.Deserialize(new byte[] { 98, 97, 114, 0 });
            Assert.Equal("bar", cls.String);
        }

        [Fact]
        public void FailsToDeserializeNullTerminatedStringWithoutNull()
        {
            var deserializer = new BitPackerDeserializer<HasNullTerminatedString>();
            var e = Assert.Throws<BitPackerTranslationException>(() => deserializer.Deserialize(new byte[] { 98, 97, 114 }));
            Assert.IsType<EndOfStreamException>(e.InnerException);
        }

        [Fact]
        public void SerializesStringWithNoLengthAndNoLengthField()
        {
            var serializer = new BitPackerSerializer<HasNoLengthAndNoLengthKey>();
            var bytes = serializer.Serialize(new HasNoLengthAndNoLengthKey() { String = "foo" });
            Assert.Equal(new byte[] { 102, 111, 111 }, bytes);
        }

        [Fact]
        public void ThrowsIfDeserializingStringWithNoLengthandNoLengthField()
        {
            var e = Assert.Throws<BitPackerTranslationException>(() => new BitPackerDeserializer<HasNoLengthAndNoLengthKey>());
            Assert.Equal(new[] { "String" }, e.MemberPath.ToArray());
            Assert.IsType<InvalidStringSetupException>(e.InnerException);
        }

        [Fact]
        public void FailsToSerializeNullTerminatedUtf16String()
        {
            Assert.Throws<InvalidAttributeException>(() => new BitPackerSerializer<HasNullTerminatedUtf16String>());
        }

        [Fact]
        public void SerializesUf16WithLengthField()
        {
            var serializer = new BitPackerSerializer<HasVariableLengthUtf16String>();
            var bytes = serializer.Serialize(new HasVariableLengthUtf16String() { String = "foo" });

            var expected = new byte[]
            {
                0x00, 0x06, // Number of bytes
                0x66, 0x00,
                0x6f, 0x00,
                0x6f, 0x00,
            };

            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void DeserializesUtf16WithLengthField()
        {
            var deserializer = new BitPackerDeserializer<HasVariableLengthUtf16String>();
            var bytes = new byte[]
            {
                0x06, 0x00,
                0x66, 0x00,
                0x6f, 0x00,
                0x6f, 0x00,
            };
            var cls = deserializer.Deserialize(bytes);

            Assert.Equal("foo", cls.String);
        }

        [Fact]
        public void FixedLengthStringsPadCorrectly()
        {
            var serializer = new BitPackerSerializer<HasFixedLengthUtf8String>();
            // £ is a 2-byte character in UTF-8
            var bytes = serializer.Serialize(new HasFixedLengthUtf8String() { String = "f£" });
            var expected = new byte[]
            {
                0x66, 0xc2, 0xa3, 0x00, 0x00, // 5 bytes in total, not 5 chars
            };

            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void DeserializesFixedLengthStringCorrectly()
        {
            var deserializer = new BitPackerDeserializer<HasFixedLengthUtf8String>();
            var bytes = new byte[]
            {
                0x66, 0xc2, 0xa3, 0x00, 0x00,
            };
            var cls = deserializer.Deserialize(bytes);
            Assert.Equal("f£", cls.String);
        }

        [Fact]
        public void ThrowsIfSerializingFixedLengthUtf16StringOfWrongSize()
        {
            var serializer = new BitPackerSerializer<HasFixedLengthUtf16String>();
            Assert.Throws<BitPackerTranslationException>(() => serializer.Serialize(new HasFixedLengthUtf16String() { String = "ab" }));
        }
    }
}
