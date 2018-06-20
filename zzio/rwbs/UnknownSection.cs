using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace zzio.rwbs
{
    [Serializable]
    public class UnknownSection : Section
    {
        private readonly SectionId _sectionId;
        public override SectionId sectionId { get { return _sectionId; } }
        public byte[] data = new byte[0];

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

        public override Section findChildById(SectionId sectionId, bool recursive)
        {
            return null;
        }
    }
}
