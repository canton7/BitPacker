using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class BitPackerMemberAttribute : Attribute
    {
        public int Order { get; set; }
        public Type EnumType { get; set; }
        internal bool SerializeInternal { get; set; }
        public Type CustomSerializer { get; set; }
        public Type CustomDeserializer { get; set; }

        internal Endianness? NullableEndianness;
        public Endianness Endianness
        {
            get { return this.NullableEndianness.GetValueOrDefault(Endianness.LittleEndian); }
            set { this.NullableEndianness = value; }
        }

        public BitPackerMemberAttribute([CallerLineNumber] int order = 0)
        {
            this.Order = order;
            this.SerializeInternal = true;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    public class BitPackerArrayAttribute : BitPackerMemberAttribute
    {
        public string LengthKey { get; set; }
        public int Length { get; set; }

        public BitPackerArrayAttribute([CallerLineNumber] int order = 0)
            : base(order)
        { }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    public sealed class BitPackerArrayLengthAttribute : BitPackerMemberAttribute
    {
        public string LengthKey { get; set; }
        public bool Serialize
        {
            get { return this.SerializeInternal; }
            set { this.SerializeInternal = value; }
        }

        public BitPackerArrayLengthAttribute([CallerLineNumber] int order = 0)
            : base(order)
        { }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple =false)]
    public sealed class BitPackerStringAttribute : BitPackerArrayAttribute
    {
        public string Encoding { get; set; }
        public bool NullTerminated { get; set; }

        public BitPackerStringAttribute([CallerLineNumber] int order = 0)
            : base(order)
        { }
    }
}
