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

        void Serialize(BitfieldBinaryWriter writer, T subject);
    }

    public interface ISerializer
    {
        bool HasFixedSize { get; }
        int MinSize { get; }

        void Serialize(BinaryWriter writer, object subject);
    }

    public static class SerializerExtensions
    {
        public static byte[] Serialize<T>(this ISerializer<T> serializer, T subject)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BitfieldBinaryWriter(ms))
            {
                serializer.Serialize(writer, subject);
                writer.Flush();
                return ms.ToArray();
            }
        }

        public static byte[] Serialize(this ISerializer serializer, object subject)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                serializer.Serialize(writer, subject);
                writer.Flush();
                return ms.ToArray();
            }
        }
    }
}
