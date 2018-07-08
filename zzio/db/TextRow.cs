using System;

namespace zzio.db
{
    public class TextRow : MappedRow
    {
        public TextRow(MappedDB mappedDB, Row row) : base(ModuleType.Text, mappedDB, row) {}

        public string Text   { get { return row.cells[0].String; } }

        public int Group     { get { return row.cells[1].Integer; } }
        
        public string Define { get { return row.cells[2].String; } }
    }
}
