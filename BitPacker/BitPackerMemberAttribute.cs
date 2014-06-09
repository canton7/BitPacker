using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class BitPackerMemberAttribute : Attribute
    {
        public int Order { get; set; }
        public int Length { get; set; }
        public string LengthField { get; set; }
        public Type EnumType { get; set; }

        internal Endianness? NullableEndianness;
        public Endianness Endianness
        {
            get { return this.NullableEndianness.GetValueOrDefault(Endianness.LittleEndian); }
            set { this.NullableEndianness = value; }
        }

        public BitPackerMemberAttribute([CallerLineNumber] int order = 0)
        {
            this.Order = order;
        }
    }
}
