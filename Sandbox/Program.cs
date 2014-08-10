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
                Array = new[] {  new TestSubClass() }
            });

            var deserializer = new BitPackerDeserializer(typeof(TestClass));

            try
            { 
                var deserialized = deserializer.Deserialize(new BinaryReader(new MemoryStream(buffer)));
            }
            catch (Exception)
            {

            }
        }
    }

    public enum Test { Foo, Bar };

    [BitPackerObject]
    public class TestClass
    {
        [BitPackerArray(Length = 1)]
        public TestSubClass[] Array { get; set; }
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

        [BitPackerMember]
        public int IntField
        {
            get { return 0; }
            set { throw new InvalidOperationException(); }
        }
    }

    [BitPackerObject]
    public class TestSubSubClass
    {
        [BitPackerMember]
        public int IntField
        {
            get { return 0; }
            set { throw new InvalidOperationException(); }
        }
    }
}
