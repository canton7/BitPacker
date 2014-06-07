using BitPacker;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            var serializer = new BitPackerSerializer(typeof(TestClass));

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    serializer.Serialize(bw, new TestClass()
                    {
                        IntField = new int[]{1, 2, 3},
                    });
                    var buffer = ms.GetBuffer();
                }
            }
        }
    }

    [BitPackerObject(Endianness=Endianness.BigEndian)]
    public class TestClass
    {
        [BitPackerMember(Length=1)]
        public int[] IntField { get; set; }

        [BitPackerMember]
        public TestSubClass SubClass { get; set; }
    }

    [BitPackerObject]
    public class TestSubClass
    {
        [BitPackerMember]
        public float FloatField { get; set; }
    }
}
