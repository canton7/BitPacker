using BitPacker;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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
                //SubClass = new TestSubClass()
                //{
                //    FloatField = 5.0f
                //},
                ArrayField = new List<TestSubClass>()
                {
                    new TestSubClass()
                },
                //ArrayField = new List<int>() { 1, 2, 3 },
                Enum = Test.Bar,
            });

            var deserializer = new BitPackerDeserializer(typeof(TestClass));

            var deserialized = deserializer.Deserialize(new BinaryReader(new MemoryStream(buffer)));
        }
    }

    public enum Test { Foo, Bar };

    [BitPackerObject]
    public class TestClass
    {
     //   [BitPackerMember]
     //   public TestSubClass SubClass { get; set; }

        [BitPackerMember(Length=3)]
        public List<TestSubClass> ArrayField { get; set; }

        [BitPackerMember(EnumType=typeof(long))]
        public Test Enum { get; set; }
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
