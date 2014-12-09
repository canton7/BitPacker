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

        T Deserialize(Stream stream);
    }

    public interface IDeserializer
    {
        bool HasFixedSize { get; }
        int MinSize { get; }

        object Deserialize(Stream stream);
    }

    public static class DeserializerExtensions
    {
        public static T Deserialize<T>(this IDeserializer<T> deserializer, byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            {
                return deserializer.Deserialize(ms);
            }
        }

        public static object Deserialize(this IDeserializer deserializer, byte[] buffer)
        {
            using (var ms = new MemoryStream())
            {
                return deserializer.Deserialize(ms);
            }
        }
    }
}
