using System;

namespace zzio.db
{
    public class ItemRow : MappedRow
    {
        public ItemRow(MappedDB mappedDB, Row row): base(ModuleType.Item, mappedDB, row) {}

        public string Name   { get { return foreignText(0); } }

        public int CardId    { get { return row.cells[1].Integer; } }

        public string Info   { get { return foreignText(2); } }

        public int Special   { get { return row.cells[3].Integer; } }

        public string Script { get { return row.cells[4].String; } }

        public int Unknown   { get { return row.cells[5].Integer; } }
    }
}
