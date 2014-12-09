using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public class BitPackerTranslationException : Exception
    {
        public IReadOnlyList<string> MemberPath { get; private set; }

        public BitPackerTranslationException(string message, List<string> memberPath)
            : this(message, memberPath, null)
        { }

        public BitPackerTranslationException(List<string> memberPath, Exception innerException)
            : this("See InnerException for details", memberPath, innerException)
        { }

        public BitPackerTranslationException(string message, List<string> memberPath, Exception innerException)
            : base(String.Format("Error translating field {0}: {1}", String.Join(".", memberPath), message), innerException)
        {
            this.MemberPath = memberPath.AsReadOnly();
        }
    }
}
