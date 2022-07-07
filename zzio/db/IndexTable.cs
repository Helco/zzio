using System;
using System.IO;

namespace zzio.db
{
    [Serializable]
    public class IndexTable
    {
        public string[] columnNames = Array.Empty<string>();
        public UInt32[] columnNumbers = Array.Empty<UInt32>();

        public int ColumnCount => columnNames.Length;

        public void Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
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
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(ColumnCount);
            for (int i = 0; i < ColumnCount; i++)
            {
                writer.Write(columnNumbers[i]);
                writer.WriteZString(columnNames[i]);
            }
        }
    }
}
