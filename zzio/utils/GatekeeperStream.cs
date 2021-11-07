using System.IO;

namespace zzio.utils
{
    /// <summary>A Stream decorator preventing the parent from being closed</summary>
    public class GatekeeperStream : Stream
    {
        private readonly Stream parent;
        private readonly bool shouldClose;
        private bool wasClosed = false;

        /// <summary>Constructs a new RangeStream</summary>
        /// <param name="parent">The parent stream to read from/write to</param>
        /// <param name="shouldClose">Whether the parent stream should be closed by closing the GatekeeperStream</param>
        public GatekeeperStream(Stream parent, bool shouldClose = false)
        {
            this.parent = parent;
            this.shouldClose = shouldClose;
        }

        public override void Close()
        {
            if (wasClosed)
                throw new IOException("Stream was already closed");
            if (shouldClose)
                parent.Close();
            wasClosed = true;
        }

        public override bool CanRead => !wasClosed && parent.CanRead;
        public override bool CanWrite => !wasClosed && parent.CanWrite;
        public override bool CanSeek => !wasClosed && parent.CanSeek;
        public override bool CanTimeout => parent.CanTimeout;
        public override long Length => parent.Length;

        public override long Position
        {
            get
            {
                return parent.Position;
            }
            set
            {
                if (wasClosed)
                    throw new IOException("Stream was already closed");
                parent.Position = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return parent.WriteTimeout;
            }
            set
            {
                if (wasClosed)
                    throw new IOException("Stream was already closed");
                parent.WriteTimeout = value;
            }
        }

        public override void Flush()
        {
            if (wasClosed)
                throw new IOException("Stream was already closed");
            parent.Flush();
        }

        public override void SetLength(long length)
        {
            if (wasClosed)
                throw new IOException("Stream was already closed");
            parent.SetLength(length);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (wasClosed)
                throw new IOException("Stream was already closed");
            return parent.Seek(offset, origin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (wasClosed)
                throw new IOException("Stream was already closed");
            return parent.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (wasClosed)
                throw new IOException("Stream was already closed");
            parent.Write(buffer, offset, count);
        }
    }
}
