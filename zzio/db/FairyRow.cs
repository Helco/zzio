using System;

namespace zzio.db
{
    public class FairyRow : MappedRow
    {
        public FairyRow(MappedDB mappedDB, Row row) : base(ModuleType.Fairy, mappedDB, row) {}

        public string Mesh     { get { return row.cells[0].String; } }

        public string Name     { get { return foreignText(1); } }

        public int Class0      { get { return row.cells[2].Integer; } }

        public int CardId      { get { return row.cells[3].Integer; } }

        public int Unknown     { get { return row.cells[4].Integer; } }

        public int[] Levels    { get { return integerRange(5, 10); } }
        public int Level0      { get { return row.cells[5].Integer; } }
        public int Level1      { get { return row.cells[6].Integer; } }
        public int Level2      { get { return row.cells[7].Integer; } }
        public int Level3      { get { return row.cells[8].Integer; } }
        public int Level4      { get { return row.cells[9].Integer; } }
        public int Level5      { get { return row.cells[10].Integer; } }
        public int Level6      { get { return row.cells[11].Integer; } }
        public int Level7      { get { return row.cells[12].Integer; } }
        public int Level8      { get { return row.cells[13].Integer; } }
        public int Level9      { get { return row.cells[14].Integer; } }

        public string Info     { get { return foreignText(15); } }

        public int MHP         { get { return row.cells[16].Integer; } }

        public int EvolCId     { get { return row.cells[17].Integer; } }

        public int EvolVar     { get { return row.cells[18].Integer; } }

        public int MovSpeed    { get { return row.cells[19].Integer; } }
        
        public int JumpPower   { get { return row.cells[20].Integer; } }

        public int CriticalHit { get { return row.cells[21].Integer; } }

        public int Sphere      { get { return row.cells[22].Integer; } }

        public int Glow        { get { return row.cells[23].Integer; } }

        public int LevelUp     { get { return row.cells[24].Integer; } }

        public int Voice       { get { return row.cells[25].Integer; } }
        
        public int Class1      { get { return row.cells[26].Integer; } }
    }
}
