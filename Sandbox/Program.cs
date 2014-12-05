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
                Test = new TestSubClass()
                {
                    SubSubClass = new TestSubSubClass()
                    {
                        TheArray = new[] { 1, 2, 3 }
                    }
                },
                OtherSubClass = new TestOtherSubClass()
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
        public TestOtherSubClass OtherSubClass { get; set; }

        [BitPackerMember]
        public TestSubClass Test { get; set; }

        
            
        


        //[BitPackerArrayLength(LengthKey = "key")]
        //public int Length
        //{
        //    get { return 3; }
        //}

        
    }

    [BitPackerObject]
    public class TestSubClass
    {
        [BitPackerMember]
        public TestSubSubClass SubSubClass { get; set; }
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
