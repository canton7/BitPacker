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
            var buffer = BitPackerTranslate.Serialize(new TestClass()
            {
                //StringLength = 10,
                //StringMember = "testy",
                Test = double.NaN
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
                //Array = new[] {  new TestSubClass() }
                //SubClass = new TestSubClass()
            });

            try
            {
                var deserialized = BitPackerTranslate.Deserialize<TestClass>(buffer);
            }
            catch (Exception)
            {

            }
        }
    }

    public enum Test { Foo, Bar };

    [BitPackerObject(Endianness = Endianness.LittleEndian)]
    public class TestClass
    {
        //[BitPackerMember(CustomDeserializer = typeof(CustomDeserializer))]
        //public TestSubClass SubClass { get; set; }

        //[BitPackerArrayLength(LengthKey = "test")]
        //public int StringLength { get; set; }

        //[BitPackerString(Encoding = "ASCII", NullTerminated = true)]
        //public string StringMember { get; set; }

        [BitPackerMember]
        public double Test { get; set; }

        //[BitPackerArrayLength(LengthKey = "key")]
        //public int Length
        //{
        //    get { return 3; }
        //}

        //[BitPackerArray(LengthKey = "key")]
        //public int[] Testy { get; set; }
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
            get;
            set;
        }
    }

    [BitPackerObject]
    public class TestSubSubClass
    {
        [BitPackerMember]
        public int IntField
        {
            get;
            set;
        }
    }

    public class CustomDeserializer : IDeserializer<TestSubClass>
    {
        public bool HasFixedSize
        {
            get { return false; }
        }

        public int MinSize
        {
            get { return 3; }
        }

        public TestSubClass Deserialize(BinaryReader reader)
        {
            return new TestSubClass();
        }
    }
}
