namespace zzio.db
{
    public class NpcRow : MappedRow
    {
        public NpcRow(MappedDB mappedDB, Row row) : base(ModuleType.Npc, mappedDB, row) { }

        public string Name => foreignText(0);

        public string TriggerScript => row.cells[1].String;
        public string InitScript => row.cells[2].String;
        public string UpdateScript => row.cells[3].String;
        public string DefeatedScript => row.cells[4].String;
        public string VictoriousScript => row.cells[5].String;

        public string InternalName => row.cells[6].String;
    }
}
