using System;
using System.IO;
using System.Collections.Generic;
using zzio.utils;

namespace zzio.db
{
    [Serializable]
    public class IndexTable
    {
        public string[] columnNames = new string[0];
        public UInt32[] columnNumbers = new UInt32[0];

        public int ColumnCount { get { return columnNames.Length; } }

        public void Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            UInt32 columnCount = reader.ReadUInt32();
            columnNumbers = new UInt32[columnCount];
            columnNames = new string[columnCount];
            for (UInt32 i = 0; i < columnCount; i++)
            {
                columnNumbers[i] = reader.ReadUInt32();
                columnNames[i] = reader.ReadZString();
            }
        }

        public void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(ColumnCount);
            for (int i = 0; i < ColumnCount; i++)
            {
                writer.Write(columnNumbers[i]);
                writer.WriteZString(columnNames[i]);
            }
        }
    }
}
