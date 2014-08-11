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
}
