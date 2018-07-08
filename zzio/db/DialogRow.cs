using System;

namespace zzio.db
{
    public class DialogRow : MappedRow
    {
        public DialogRow(MappedDB mappedDB, Row row): base(ModuleType.Dialog, mappedDB, row) {}

        public string Text  { get { return row.cells[0].String; } }

        public int Npc      { get { return row.cells[1].Integer; } }

        public string Voice { get { return row.cells[2].String; } }
    }
}
