using System;

namespace zzio.db
{
    public class SpellRow : MappedRow
    {
        public SpellRow(MappedDB mappedDB, Row row) : base(ModuleType.Spell, mappedDB, row) {}

        public string Name       { get { return foreignText(0); } }
        
        public int Type          { get { return row.cells[1].Integer; } }

        public int CardId        { get { return row.cells[2].Integer; } }
        
        public int[] Prices      { get { return integerRange(3, 3); } }
        public int PriceA        { get { return row.cells[3].Integer; } }
        public int PriceB        { get { return row.cells[4].Integer; } }
        public int PriceC        { get { return row.cells[5].Integer; } }

        public string Info       { get { return row.cells[6].String; } }

        public int Mana          { get { return row.cells[7].Integer; } }

        public int Loadup        { get { return row.cells[8].Integer; } }

        public int Unknown       { get { return row.cells[9].Integer; } }

        public int MissileEffect { get { return row.cells[10].Integer; } }

        public int ImpactEffect  { get { return row.cells[11].Integer; } }

        public int Damage        { get { return row.cells[12].Integer; } }

        public int Behaviour     { get { return row.cells[13].Integer; } }
    }
}
