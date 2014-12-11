using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class CountingStream : Stream
    {
        private readonly Stream baseStream;

        public int ReadBytes { get; private set; }
        public int WrittenBytes { get; private set; }

        public CountingStream(Stream baseStream)
        {
            this.baseStream = baseStream;
        }

        public override bool CanRead
        {
            get { return this.baseStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return this.baseStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return this.baseStream.CanWrite; }
        }

        public override void Flush()
        {
            this.baseStream.Flush();
        }

        public override long Length
        {
            get { return this.baseStream.Length; }
        }

        public override long Position
        {
            get { return this.baseStream.Position; }
            set { this.baseStream.Position = value; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            this.ReadBytes += count;
            return this.baseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.baseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.WrittenBytes += count;
            this.baseStream.Write(buffer, offset, count);
        }

        public override void Close()
        {
            this.baseStream.Close();
        }
    }
}
