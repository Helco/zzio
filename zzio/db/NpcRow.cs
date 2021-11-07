namespace zzio.db
{
    public class NpcRow : MappedRow
    {
        public NpcRow(MappedDB mappedDB, Row row) : base(ModuleType.Npc, mappedDB, row) {}

        public string Name    => foreignText(0);

        public string Script1 => row.cells[1].String;
        public string Script2 => row.cells[2].String;
        public string Script3 => row.cells[3].String;
        public string Script4 => row.cells[4].String;
        public string Script5 => row.cells[5].String;

        public string Unknown => row.cells[6].String;
    }
}
