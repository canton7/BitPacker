using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public interface ICustomSerializer
    {
        Type ContextType { get; }
        bool HasFixedSize { get; }
        int MinSize { get; }

        void Serialize(BinaryWriter writer, object subject, object context);
    }
}
