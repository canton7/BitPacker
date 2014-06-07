using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class BitPackerObjectAttribute : Attribute
    {
        public Endianness Endianness { get; set; }

        public BitPackerObjectAttribute()
        {
            this.Endianness = Endianness.BigEndian;
        }
    }
}
