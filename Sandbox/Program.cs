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
                Test = (Test)512,
            });
        }
    }

    public enum Test { Foo, Bar };

    [BitPackerObject]
    public class TestClass
    {
        [BitPackerMember(EnumType=typeof(byte))]
        public Test Test { get; set; }

        //[BitPackerMember(Length=2)]
        //public TestSubClass[] ArrayField { get; set; }

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
