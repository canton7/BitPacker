using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public class InvalidAttributeException : BitPackerException
    {
        public string Property { get; private set; }
        public InvalidAttributeException(string message, string property)
            : base(String.Format("Property {0}: {1}", property, message))
        {
            this.Property = property;
        }
    }
}
