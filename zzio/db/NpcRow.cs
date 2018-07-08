using System;

namespace zzio.db
{
    public class NpcRow : MappedRow
    {
        public NpcRow(MappedDB mappedDB, Row row) : base(ModuleType.Npc, mappedDB, row) {}

        public string Name    { get { return foreignText(0); } }

        public string Script1 { get { return row.cells[1].String; } }
        public string Script2 { get { return row.cells[2].String; } }
        public string Script3 { get { return row.cells[3].String; } }
        public string Script4 { get { return row.cells[4].String; } }
        public string Script5 { get { return row.cells[5].String; } }

        public string Unknown { get { return row.cells[6].String; } }
    }
}
