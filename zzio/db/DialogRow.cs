namespace zzio.db
{
    public class DialogRow : MappedRow
    {
        public DialogRow(MappedDB mappedDB, Row row) : base(ModuleType.Dialog, mappedDB, row) { }

        public string Text => row.cells[0].String;

        public int Npc => row.cells[1].Integer;

        public string Voice => row.cells[2].String;
    }
}
