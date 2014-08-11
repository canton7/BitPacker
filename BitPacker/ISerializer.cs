using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public interface ISerializer<T>
    {
        bool HasFixedSize { get; }
        int MinSize { get; }

        void Serialize(BinaryWriter writer, T subject);

        byte[] Serialize(T subject);
    }

    public interface ISerializer
    {
        bool HasFixedSize { get; }
        int MinSize { get; }

        void Serialize(BinaryWriter writer, object subject);

        byte[] Serialize(object subject);
    }
}
