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
            var serializer = new BitPackerSerializer<TestClass>();

            var buffer = serializer.Serialize(new TestClass()
            {
                Test = true,
                ArrayField = new[]
                {
                    new TestSubClass()
                    {
                        FloatField = 1.0f,
                        IntField = 3,
                    },
                    new TestSubClass()
                    {
                        FloatField = 2.0f,
                        IntField = 4,
                    }
                }
            });
        }
    }

    [BitPackerObject(Endianness=Endianness.LittleEndian)]
    public class TestClass
    {
        [BitPackerMember]
        public bool Test { get; set; }

        [BitPackerMember(Length=3)]
        public TestSubClass[] ArrayField { get; set; }

        //[BitPackerMember]
        //public TestSubClass SubClass { get; set; }
    }

    [BitPackerObject]
    public class TestSubClass
    {
        [BitPackerMember]
        public float FloatField { get; set; }

        [BitPackerMember]
        public int IntField { get; set; }

        [BitPackerMember]
        public int AnotherIntField { get; set; }
    }
}
