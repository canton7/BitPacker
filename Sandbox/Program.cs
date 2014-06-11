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
                SubClass = new TestSubClass(),
                ArrayField = new[]
                {
                    1, 2, 3
                },
                Enum = Test.Bar,
            });
        }
    }

    public enum Test { Foo, Bar };

    [BitPackerObject]
    public class TestClass
    {
        [BitPackerMember]
        public TestSubClass SubClass { get; set; }

        [BitPackerMember(LengthKey="Test")]
        public int[] ArrayField { get; set; }

        [BitPackerMember(EnumType=typeof(long))]
        public Test Enum { get; set; }
    }

    [BitPackerObject]
    public class TestSubClass
    {
        [BitPackerMember]
        public float FloatField { get; set; }

        [BitPackerMember(LengthKey="Test")]
        public int IntField { get; set; }

        [BitPackerMember]
        public int AnotherIntField { get; set; }
    }
}
