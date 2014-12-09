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
        
        internal bool SerializeInternal { get; set; }
        public Type Serializer { get; set; }
        public Type Deserializer { get; set; }

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

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class BitPackerEnumAttribute : BitPackerIntegerAttribute
    {
        public Type Type { get; set; }

        public BitPackerEnumAttribute([CallerLineNumber] int order = 0)
            : base(order)
        { }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class BitPackerArrayAttribute : BitPackerMemberAttribute
    {
        public string LengthKey { get; set; }
        public int Length { get; set; }

        public BitPackerArrayAttribute([CallerLineNumber] int order = 0)
            : base(order)
        { }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class BitPackerStringAttribute : BitPackerArrayAttribute
    {
        public string Encoding { get; set; }
        public bool NullTerminated { get; set; }

        public BitPackerStringAttribute([CallerLineNumber] int order = 0)
            : base(order)
        {
            this.Encoding = "ASCII";
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class BitPackerIntegerAttribute : BitPackerMemberAttribute
    {
        internal int? NullableBitWidth;
        public int BitWidth
        {
            get { return this.NullableBitWidth.GetValueOrDefault(0); }
            set { this.NullableBitWidth = value; }
        }

        public bool PadContainerAfter { get; set; }

        public BitPackerIntegerAttribute([CallerLineNumber] int order = 0)
            : base(order)
        { }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class BitPackerBooleanAttribute : BitPackerIntegerAttribute
    {
        public Type Type { get; set; }

        public BitPackerBooleanAttribute([CallerLineNumber] int order = 0)
            : base(order)
        { }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class BitPackerArrayLengthAttribute : BitPackerIntegerAttribute
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
}
