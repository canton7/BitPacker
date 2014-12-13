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

        int Deserialize(Stream stream, out T subject);
    }

    public interface IDeserializer
    {
        bool HasFixedSize { get; }
        int MinSize { get; }

        int Deserialize(Stream stream, out object subject);
    }

    public static class DeserializerExtensions
    {
        public static T Deserialize<T>(this IDeserializer<T> deserializer, Stream stream)
        {
            T subject;
            deserializer.Deserialize(stream, out subject);
            return subject;
        }

        public static T Deserialize<T>(this IDeserializer<T> deserializer, byte[] buffer, int index)
        {
            if (buffer.Length - index < deserializer.MinSize)
                throw new ArgumentException(String.Format("Buffer length must be >= deserializer's MinSize ({0})", deserializer.MinSize), "buffer");

            T subject;
            using (var ms = new MemoryStream(buffer, index, buffer.Length - index))
            {
                deserializer.Deserialize(ms, out subject);
            }
            return subject;
        }

        public static int Deserialize<T>(this IDeserializer<T> deserializer, byte[] buffer, int index, out T subject)
        {
            if (buffer.Length - index < deserializer.MinSize)
                throw new ArgumentException(String.Format("Buffer length must be >= deserializer's MinSize ({0})", deserializer.MinSize), "buffer");

            using (var ms = new MemoryStream(buffer, index, buffer.Length - index))
            {
                return deserializer.Deserialize(ms, out subject);
            }
        }

        public static T Deserialize<T>(this IDeserializer<T> deserializer, byte[] buffer)
        {
            return Deserialize(deserializer, buffer, 0);
        }

        public static int Deserialize<T>(this IDeserializer<T> deserializer, byte[] buffer, out T subject)
        {
            return Deserialize(deserializer, buffer, 0, out subject);
        }

        public static object Deserialize(this IDeserializer deserializer, Stream stream)
        {
            object subject;
            deserializer.Deserialize(stream, out subject);
            return subject;
        }

        public static object Deserialize(this IDeserializer deserializer, byte[] buffer, int index)
        {
            if (buffer.Length - index < deserializer.MinSize)
                throw new ArgumentException(String.Format("Buffer length must be >= deserializer's MinSize ({0})", deserializer.MinSize), "buffer");

            using (var ms = new MemoryStream(buffer, index, buffer.Length - index))
            {
                return deserializer.Deserialize(ms);
            }
        }

        public static int Deserialize(this IDeserializer deserializer, byte[] buffer, int index, out object subject)
        {
            if (buffer.Length - index < deserializer.MinSize)
                throw new ArgumentException(String.Format("Buffer length must be >= deserializer's MinSize ({0})", deserializer.MinSize), "buffer");

            using (var ms = new MemoryStream(buffer, index, buffer.Length - index))
            {
                return deserializer.Deserialize(ms, out subject);
            }
        }

        public static object Deserialize(this IDeserializer deserializer, byte[] buffer)
        {
            return Deserialize(deserializer, buffer, 0);
        }

        public static int Deserialize(this IDeserializer deserializer, byte[] buffer, out object subject)
        {
            return Deserialize(deserializer, buffer, 0, out subject);
        }
    }
}
