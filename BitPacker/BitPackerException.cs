using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public class BitPackerException : Exception
    {
        public BitPackerException(string message)
            : base(message)
        { }
    }
}
