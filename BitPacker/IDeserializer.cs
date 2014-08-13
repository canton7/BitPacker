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
    }

    public interface IDeserializer
    {
        bool HasFixedSize { get; }
        int MinSize { get; }

        object Deserialize(BinaryReader reader);
    }

    public static class DeserializerExtensions
    {
        public static T Deserialize<T>(this IDeserializer<T> deserializer, byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            using (var reader = new BinaryReader(ms))
            {
                return deserializer.Deserialize(reader);
            }
        }

        public static object Deserialize(this IDeserializer deserializer, byte[] buffer)
        {
            using (var ms = new MemoryStream())
            using (var reader = new BinaryReader(ms))
            {
                return deserializer.Deserialize(reader);
            }
        }
    }
}
