namespace zzio.db
{
    public class ItemRow : MappedRow
    {
        public ItemRow(MappedDB mappedDB, Row row) : base(ModuleType.Item, mappedDB, row) { }

        public string Name => foreignText(0);

        public CardId CardId => new CardId(row.cells[1].Integer);

        public string Info => foreignText(2);

        public byte Special => row.cells[3].Byte;

        public string Script => row.cells[4].String;

        public int Unknown => row.cells[5].Integer;
    }
}
