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

        int Serialize(Stream stream, T subject);
    }

    public interface ISerializer
    {
        bool HasFixedSize { get; }
        int MinSize { get; }

        int Serialize(Stream stream, object subject);
    }

    public static class SerializerExtensions
    {
        public static byte[] Serialize<T>(this ISerializer<T> serializer, T subject)
        {
            using (var ms = new MemoryStream())
            {
                serializer.Serialize(ms, subject);
                return ms.ToArray();
            }
        }

        public static int Serialize<T>(this ISerializer<T> serializer, T subject, byte[] buffer, int index, int maxCount)
        {
            if (serializer.MinSize > maxCount)
                throw new ArgumentException(String.Format("Must be less than the Serializer's MinSize ({0})", serializer.MinSize), "maxCount");

            using (var ms = new MemoryStream(buffer, index, maxCount))
            {
                return serializer.Serialize(ms, subject);
            }
        }

        public static byte[] Serialize(this ISerializer serializer, object subject)
        {
            using (var ms = new MemoryStream())
            {
                serializer.Serialize(ms, subject);
                return ms.ToArray();
            }
        }

        public static int Serialize(this ISerializer serializer, object subject, byte[] buffer, int index, int maxCount)
        {
            if (serializer.MinSize > maxCount)
                throw new ArgumentException(String.Format("Must be less than the Serializer's MinSize ({0})", serializer.MinSize), "maxCount");

            using (var ms = new MemoryStream(buffer, index, maxCount))
            {
                return serializer.Serialize(ms, subject);
            }
        }
    }
}
