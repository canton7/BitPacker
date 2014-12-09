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
                //IntField = 3,
                SubClass = new TestSubClass(),
                //Enum = Test.Bar,
                //TestBool = true,
                //AnotherTestBool = true,
                //Enum = Test.Bar,
                //StringLength = 10,
                //StringMember = "testy",
                //Test = new TestSubClass()
                //{
                //    SubSubClass = new TestSubSubClass()
                //    {
                //        TheArray = new[] { 1, 2, 3 }
                //    }
                //},
                //OtherSubClass = new TestOtherSubClass()
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

    public enum Test : byte { Foo, Bar = 1 };

    [BitPackerObject(Endianness=Endianness.BigEndian)]
    public class TestClass
    {
        //[BitPackerMember(CustomDeserializer = typeof(CustomDeserializer))]
        //public TestSubClass SubClass { get; set; }

        //[BitPackerArrayLength(LengthKey = "test")]
        //public int StringLength { get; set; }

        //[BitPackerString(NullTerminated = true, Length = 5)]
        //public string StringMember { get; set; }

        //[BitPackerMember]
        //public TestOtherSubClass OtherSubClass { get; set; }

        //[BitPackerMember]
        //public TestSubClass Test { get; set; }

        //[BitPackerMember]
        //public Test Enum { get; set; }

        [BitPackerMember]
        public int IntField { set { throw new Exception("BOOM"); } }

        [BitPackerMember]
        public TestSubClass SubClass { get; set; }

        //[BitPackerInteger(BitWidth = 0, PadContainerAfter = true)]
        //public short SomeInt { get; set; }

        //[BitPackerInteger(BitWidth = 8)]
        //public short SomeOtherInt { get; set; }

        //[BitPackerInteger(BitWidth = 8)]
        //public short SomeOtherOtherInt { get; set; }

        //[BitPackerMember]
        //public short SomeOtherOtherOtherint { get; set; }

        //[BitPackerBoolean(Type = typeof(byte), BitWidth=1)]
        //public bool TestBool { get; set; }

        //[BitPackerBoolean(Type = typeof(byte), BitWidth = 1)]
        //public bool AnotherTestBool { get; set; }

        //[BitPackerArrayLength(LengthKey = "key")]
        //public int Length
        //{
        //    get { return 3; }
        //}

        
    }

    [BitPackerObject]
    public class TestSubClass
    {
        public int Foo { get; set; }
        //[BitPackerMember]
        //public TestSubSubClass SubSubClass { get; set; }
    }

    [BitPackerObject]
    public class TestSubSubClass
    {
        [BitPackerArray(LengthKey = "key")]
        public int[] TheArray { get; set; }
    }

    [BitPackerObject]
    public class TestOtherSubClass
    {
        [BitPackerArrayLength(LengthKey = "key")]
        public int Length { get; set; }
    }
}
