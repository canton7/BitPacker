using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public interface ICustomDeserializer
    {
        Type ContextType { get; }
        bool HasFixedSize { get; }
        int MinSize { get; }

        object Deserialize(BinaryReader reader, object context);
    }
}
