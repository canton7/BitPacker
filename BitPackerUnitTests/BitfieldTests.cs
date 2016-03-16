using BitPacker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BitPackerUnitTests
{
    public class BitfieldTests
    {
        [BitPackerBitField(WidthBytes = 2)]
        private class HasSimpleBitfield
        {
            [BitPackerInteger(BitWidth = 2)]
            public byte Int { get; set; }

            [BitPackerBoolean(BitWidth = 1)]
            public bool Bool { get; set; }
        }

        [Fact]
        public void SerializesSimpleBitfield()
        {
            var serializer = new BitPackerSerializer<HasSimpleBitfield>();
            var bytes = serializer.Serialize(new HasSimpleBitfield() { Int = 2, Bool = true });
            var expected = new byte[]
            {
                0x00, 0x06, // <padding>, 10, 1
            };
            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void DeserializesSimpleBitfield()
        {
            var deserializer = new BitPackerDeserializer<HasSimpleBitfield>();
            var bytes = new byte[]
            {
                0x00, 0x06,
            };
            var cls = deserializer.Deserialize(bytes);

            Assert.Equal(2, cls.Int);
            Assert.True(cls.Bool);
        }
    }
}
