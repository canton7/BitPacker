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
                //ArrayField = new List<TestSubClass>()
                //{
                //    new TestSubClass()
                //},
                ////ArrayField = new List<int>() { 1, 2, 3 },
                //Enum = Test.Bar,
                SubClass = new TestSubClass()
                {
                    Array = new[] { new TestSubSubClass() { IntField = 4 }, new TestSubSubClass() { IntField = 5} }
                },
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
        [BitPackerArrayLength(LengthKey = "key")]
        public int Length { get; set; }

        [BitPackerMember]
        public TestSubClass SubClass { get; set; }

        //[BitPackerMember(Length=3)]
        //public List<TestSubClass> ArrayField { get; set; }

        [BitPackerMember(EnumType = typeof(int))]
        public Test Enum { get; set; }
    }

    [BitPackerObject]
    public class TestSubClass
    {
        //[BitPackerMember]
        //public float FloatField { get; set; }

        //[BitPackerMember]
        //public int IntField { get; set; }

        //[BitPackerMember]
        //public int AnotherIntField { get; set; }

        [BitPackerArray(LengthKey = "key", Length = 3)]
        public TestSubSubClass[] Array { get; set; }
    }

    [BitPackerObject]
    public class TestSubSubClass
    {
        [BitPackerMember]
        public int IntField { get; set; }
    }
}
