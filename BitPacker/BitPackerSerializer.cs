﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public class BitPackerSerializer : ISerializer
    {
        internal Action<BitfieldBinaryWriter, object> serializer;
        internal Type subjectType;

        public bool HasFixedSize { get; private set; }
        public int MinSize { get; private set; }

        public BitPackerSerializer(Type subjectType)
            : this(subjectType, null)
        { }

        public BitPackerSerializer(Type subjectType, Endianness defaultEndianness)
            : this(subjectType, (Endianness?)defaultEndianness)
        { }

        private BitPackerSerializer(Type subjectType, Endianness? defaultEndianness)
        {
            this.subjectType = subjectType;

            var writer = Expression.Parameter(typeof(BitfieldBinaryWriter), "writer");
            var subject = Expression.Parameter(typeof(object), "subject");

            var subjectVar = Expression.Variable(subjectType, "typedSubject");
            var assignment = Expression.Assign(subjectVar, Expression.Convert(subject, subjectType));

            var builder = new SerializerExpressionBuilder(writer, subjectType, defaultEndianness);
            var typeDetails = builder.BuildExpression(subjectVar);

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            var block = Expression.Block(new[] { subjectVar }, assignment, typeDetails.OperationExpression);
            this.serializer = Expression.Lambda<Action<BitfieldBinaryWriter, object>>(block, writer, subject).Compile();
        }

        private void CheckType(object subject)
        {
            if (!this.subjectType.IsAssignableFrom(subject.GetType()))
                throw new Exception(String.Format("Serializer for type {0} call with subject of type {1}", this.subjectType, subject.GetType()));
        }

        public int Serialize(Stream stream, object subject)
        {
            this.CheckType(subject);
            var countingStream = new CountingStream(stream);
            using (var writer = new BitfieldBinaryWriter(countingStream))
            {
                this.serializer(writer, subject);
            }
            return countingStream.BytesWritten;
        }
    }

    public class BitPackerSerializer<T> : ISerializer<T>
    {
        private static readonly Lazy<BitPackerSerializer<T>> lazy = new Lazy<BitPackerSerializer<T>>(() => new BitPackerSerializer<T>());
        internal static BitPackerSerializer<T> Instance 
        {
            get { return lazy.Value; }
        }

        public bool HasFixedSize { get; private set; }
        public int MinSize { get; private set; }

        private Action<BitfieldBinaryWriter, T> serializer;

        public BitPackerSerializer()
            : this(null)
        { }

        public BitPackerSerializer(Endianness defaultEndianness)
            : this((Endianness?)defaultEndianness)
        { }

        private BitPackerSerializer(Endianness? defaultEndianness)
	    {
            var subjectType = typeof(T);
            var writer = Expression.Parameter(typeof(BitfieldBinaryWriter), "writer");
            var subject = Expression.Parameter(subjectType, "subject");

            var builder = new SerializerExpressionBuilder(writer, subjectType, defaultEndianness);
            var typeDetails = builder.BuildExpression(subject);

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            this.serializer = Expression.Lambda<Action<BitfieldBinaryWriter, T>>(typeDetails.OperationExpression, writer, subject).Compile();
	    }

        public int Serialize(Stream stream, T subject)
        {
            var countingStream = new CountingStream(stream);
            using (var writer = new BitfieldBinaryWriter(countingStream))
            {
                this.serializer(writer, subject);
            }
            return countingStream.BytesWritten;
        }
    }
}
