using System;
using zzio.primitives;

namespace zzio.db
{
    public class FairyRow : MappedRow
    {
        public FairyRow(MappedDB mappedDB, Row row) : base(ModuleType.Fairy, mappedDB, row) {}

        public string Mesh     => row.cells[0].String;

        public string Name     => foreignText(1);

        public int Class0      => row.cells[2].Integer;

        public CardId CardId   => new CardId(row.cells[3].Integer);

        public int Unknown     => row.cells[4].Integer;

        public int[] Levels    => integerRange(5, 10);
        public int Level0      => row.cells[5].Integer;
        public int Level1      => row.cells[6].Integer;
        public int Level2      => row.cells[7].Integer;
        public int Level3      => row.cells[8].Integer;
        public int Level4      => row.cells[9].Integer;
        public int Level5      => row.cells[10].Integer;
        public int Level6      => row.cells[11].Integer;
        public int Level7      => row.cells[12].Integer;
        public int Level8      => row.cells[13].Integer;
        public int Level9      => row.cells[14].Integer;

        public string Info     => foreignText(15);

        public int MHP         => row.cells[16].Integer;

        public int EvolCId     => row.cells[17].Integer;

        public int EvolVar     => row.cells[18].Integer;

        public int MovSpeed    => row.cells[19].Integer;
        
        public int JumpPower   => row.cells[20].Integer;

        public int CriticalHit => row.cells[21].Integer;

        public int Sphere      => row.cells[22].Integer;

        public int Glow        => row.cells[23].Integer;

        public int LevelUp     => row.cells[24].Integer;

        public int Voice       => row.cells[25].Integer;
        
        public int Class1      => row.cells[26].Integer;
    }
}
