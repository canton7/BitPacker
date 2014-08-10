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

        public BitPackerTranslationException(List<string> memberPath, Exception innerException)
            : base(String.Format("Error translating field {0}", String.Join(".", memberPath)), innerException)
        {
            this.MemberPath = memberPath.AsReadOnly();
        }
    }
}
