using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class BitPackerBitFieldAttribute : Attribute
    {
        internal int? NullableWidthBytes { get; private set; }

        public int WidthBytes
        {
            get { return this.NullableWidthBytes ?? 0; }
            set { this.NullableWidthBytes = value; }
        }
    }
}
