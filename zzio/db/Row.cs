using System;
using System.IO;

namespace zzio.db
{
    [Serializable]
    public class Row
    {
        public UID uid;
        public Cell[] cells = Array.Empty<Cell>();

        public Row() { }

        public void Read(BinaryReader reader)
        {
            uid = UID.ReadNew(reader);
            cells = new Cell[reader.ReadUInt32()];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = Cell.ReadNew(reader);
        }

        public void Write(BinaryWriter writer)
        {
            uid.Write(writer);
            writer.Write(cells.Length);
            foreach (Cell cell in cells)
                cell.Write(writer);
        }
    }
}
