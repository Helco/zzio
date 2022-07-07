using System;
using System.IO;

namespace zzio.rwbs
{
    [Serializable]
    public class UnknownSection : Section
    {
        private readonly SectionId _sectionId;
        public override SectionId sectionId => _sectionId;
        public byte[] data = Array.Empty<byte>();

        public UnknownSection() : this(SectionId.Unknown) { }

        public UnknownSection(SectionId id)
        {
            _sectionId = id;
        }

        protected override void readBody(Stream stream)
        {
            int dataLen = (int)(stream.Length - stream.Position);
            data = new byte[dataLen];
            if (stream.Read(data, 0, dataLen) != dataLen)
                throw new IOException("Could not read all of section");
        }

        protected override void writeBody(Stream stream)
        {
            stream.Write(data, 0, data.Length);
        }
    }
}
