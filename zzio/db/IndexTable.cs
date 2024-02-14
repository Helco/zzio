using System;
using System.IO;

namespace zzio.db;

[Serializable]
public class IndexTable
{
    public string[] columnNames = [];
    public uint[] columnNumbers = [];

    public int ColumnCount => columnNames.Length;

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        uint columnCount = reader.ReadUInt32();
        columnNumbers = new uint[columnCount];
        columnNames = new string[columnCount];
        for (uint i = 0; i < columnCount; i++)
        {
            columnNumbers[i] = reader.ReadUInt32();
            columnNames[i] = reader.ReadZString();
        }
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(ColumnCount);
        for (int i = 0; i < ColumnCount; i++)
        {
            writer.Write(columnNumbers[i]);
            writer.WriteZString(columnNames[i]);
        }
    }
}
