using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public interface IDeserializer<T>
    {
        bool HasFixedSize { get; }
        int MinSize { get; }

        T Deserialize(BinaryReader reader);

        T Deserialize(byte[] buffer);
    }

    public interface IDeserializer
    {
        bool HasFixedSize { get; }
        int MinSize { get; }

        object Deserialize(BinaryReader reader);

        object Deserialize(byte[] buffer);
    }
}
