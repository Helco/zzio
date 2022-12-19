using System;
using System.IO;
using System.Text;

namespace zzio.rwbs
{
    [Serializable]
    public class RWString : Section
    {
        public override SectionId sectionId => SectionId.String;
        public string value = "";

        protected override void readBody(Stream stream)
        {
            using BinaryReader reader = new(stream);
            byte[] buffer = reader.ReadBytes((int)stream.Length);
            value = Encoding.UTF8.GetString(buffer);
            int terminator = value.IndexOf('\0');
            if (terminator >= 0)
                value = value[..terminator];
        }

        protected override void writeBody(Stream stream)
        {
            using BinaryWriter writer = new(stream);
            byte[] buffer = Encoding.UTF8.GetBytes(value);
            writer.Write(buffer);
        }
    }
}
