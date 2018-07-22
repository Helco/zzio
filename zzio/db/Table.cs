using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using zzio.primitives;
using zzio.utils;

namespace zzio.db
{
    [Serializable]
    public class Table
    {
        public Dictionary<UID, Row> rows = new Dictionary<UID, Row>();

        public void Read(Stream stream)
        {
            rows.Clear();
            BinaryReader reader = new BinaryReader(stream);
            UInt32 rowCount = reader.ReadUInt32();
            for (UInt32 i = 0; i < rowCount; i++)
            {
                Row row = new Row();
                row.Read(reader);
                rows.Add(row.uid, row);
            }
        }

        public void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(rows.Count);
            foreach (Row row in rows.Values)
                row.Write(writer);
        }
    }
}
