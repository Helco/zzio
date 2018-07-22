using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    [Serializable]
    public class RWString : Section
    {
        public override SectionId sectionId => SectionId.String;
        public string value = "";

        protected override void readBody(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            byte[] buffer = reader.ReadBytes((int)stream.Length);
            value = Encoding.UTF8.GetString(buffer).Replace("\u0000", "");
        }

        protected override void writeBody(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            byte[] buffer = Encoding.UTF8.GetBytes(value);
            writer.Write(buffer);
        }

        public override Section FindChildById(SectionId sectionId, bool recursive)
        {
            return null;
        }
    }
}
