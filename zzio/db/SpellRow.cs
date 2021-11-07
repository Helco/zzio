using zzio.primitives;

namespace zzio.db
{
    public class SpellRow : MappedRow
    {
        public SpellRow(MappedDB mappedDB, Row row) : base(ModuleType.Spell, mappedDB, row) {}

        public string Name       => foreignText(0);

        public int Type          => row.cells[1].Integer;

        public CardId CardId     => new CardId(row.cells[2].Integer);

        public int[] Prices      => integerRange(3, 3);
        public int PriceA        => row.cells[3].Integer;
        public int PriceB        => row.cells[4].Integer;
        public int PriceC        => row.cells[5].Integer;

        public string Info       => row.cells[6].String;

        public int Mana          => row.cells[7].Integer;

        public int Loadup        => row.cells[8].Integer;

        public int Unknown       => row.cells[9].Integer;

        public int MissileEffect => row.cells[10].Integer;

        public int ImpactEffect  => row.cells[11].Integer;

        public int Damage        => row.cells[12].Integer;

        public int Behavior     => row.cells[13].Integer;
    }
}
