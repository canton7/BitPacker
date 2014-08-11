using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public static class BitPackerTranslate
    {
        private static ConcurrentDictionary<Type, ISerializer> nongenericSerializerCache = new ConcurrentDictionary<Type, ISerializer>();
        private static ConcurrentDictionary<Type, IDeserializer> nongenericDeserializerCache = new ConcurrentDictionary<Type, IDeserializer>();

        public static ISerializer<T> GetSerializer<T>()
        {
            return BitPackerSerializer<T>.Instance;
        }

        public static ISerializer GetSerializer(Type type)
        {
            return nongenericSerializerCache.GetOrAdd(type, t => new BitPackerSerializer(t));
        }

        public static void Serialize<T>(BinaryWriter writer, T subject)
        {
            BitPackerSerializer<T>.Instance.Serialize(writer, subject);
        }

        public static byte[] Serialize<T>(T subject)
        {
            return BitPackerSerializer<T>.Instance.Serialize(subject);
        }

        public static IDeserializer<T> GetDeserializer<T>()
        {
            return BitPackerDeserializer<T>.Instance;
        }

        public static IDeserializer GetDeserializer(Type type)
        {
            return nongenericDeserializerCache.GetOrAdd(type, t => new BitPackerDeserializer(t));
        }

        public static T Deserialize<T>(BinaryReader reader)
        {
            return BitPackerDeserializer<T>.Instance.Deserialize(reader);
        }

        public static T Deserialize<T>(byte[] buffer)
        {
            return BitPackerDeserializer<T>.Instance.Deserialize(buffer);
        }
    }
}
