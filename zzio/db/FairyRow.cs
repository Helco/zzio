using System;
using System.Linq;

namespace zzio.db
{
    public class FairyRow : MappedRow
    {
        public FairyRow(MappedDB mappedDB, Row row) : base(ModuleType.Fairy, mappedDB, row) { }

        public string Mesh => row.cells[0].String;

        public string Name => foreignText(1);

        public ZZClass Class0 => (ZZClass)row.cells[2].Integer;

        public CardId CardId => new CardId(row.cells[3].Integer);

        public int Unknown => row.cells[4].Integer;

        public FairyLevelData[] Levels => Enumerable
            .Range(5, 10)
            .Select(i => new FairyLevelData(unchecked((uint)row.cells[i].Integer)))
            .ToArray();

        public FairyLevelData Level0 => new FairyLevelData(unchecked((uint)row.cells[5].Integer));
        public FairyLevelData Level1 => new FairyLevelData(unchecked((uint)row.cells[6].Integer));
        public FairyLevelData Level2 => new FairyLevelData(unchecked((uint)row.cells[7].Integer));
        public FairyLevelData Level3 => new FairyLevelData(unchecked((uint)row.cells[8].Integer));
        public FairyLevelData Level4 => new FairyLevelData(unchecked((uint)row.cells[9].Integer));
        public FairyLevelData Level5 => new FairyLevelData(unchecked((uint)row.cells[10].Integer));
        public FairyLevelData Level6 => new FairyLevelData(unchecked((uint)row.cells[11].Integer));
        public FairyLevelData Level7 => new FairyLevelData(unchecked((uint)row.cells[12].Integer));
        public FairyLevelData Level8 => new FairyLevelData(unchecked((uint)row.cells[13].Integer));
        public FairyLevelData Level9 => new FairyLevelData(unchecked((uint)row.cells[14].Integer));

        public string Info => foreignText(15);

        public int MHP => row.cells[16].Integer;

        public int EvolCId => row.cells[17].Integer;

        public int EvolVar => row.cells[18].Integer;

        public int MovSpeed => row.cells[19].Integer;

        public int JumpPower => row.cells[20].Integer;

        public int CriticalHit => row.cells[21].Integer;

        public int Sphere => row.cells[22].Integer;

        public int Glow => row.cells[23].Integer;

        public int Unknown24 => row.cells[24].Integer;

        public int LevelUp => row.cells[25].Integer;

        public int WingSound => row.cells[26].Integer;

        public double BaseMoveSpeed => MovSpeed switch
        {
            0 => 0.8,
            1 => 0.95,
            2 => 1.1,
            3 => 1.25,
            4 => 1.35,
            _ => 0.0
        } * 0.9;

        public double BaseJumpPower => JumpPower switch
        {
            0 => 0.5,
            1 => 0.8,
            2 => 1.2,
            3 => 1.3,
            4 => 1.6,
            _ => 0.0
        } * 1.2;

        public double BaseCriticalHit => CriticalHit switch
        {
            0 => 1.0,
            1 => 1.15,
            2 => 1.3,
            3 => 1.45,
            4 => 1.6,
            _ => 0
        };
    }

    public record struct FairyLevelData(uint Raw)
    {
        public int StartLevel
        {
            get => (int)(Raw >> 16);
            set
            {
                if (value < 0 || value >= 2 << 16)
                    throw new ArgumentOutOfRangeException($"Invalid LevelData start level: {value}");
                Raw = (Raw & 0xffff) | (uint)(value << 16);
            }
        }

        public int SpellSlot
        {
            get => (int)(Raw >> 12) & 3;
            set
            {
                if (value < 0 || value > 3)
                    throw new ArgumentOutOfRangeException($"Invalid LevelData spell slot: {value}");
                Raw = (Raw & 0xffff) | (Raw & 0b1100_1111_1111_1111) | (uint)(value << 12);
            }
        }

        public ZZClass Class0
        {
            get => (ZZClass)((Raw >> 8) & 0xf);
            set
            {
                var iValue = (uint)value;
                if (iValue > 15)
                    throw new ArgumentOutOfRangeException($"Invalid LevelData class 0: {value}");
                Raw = (Raw & 0xffff_f0ff) | (iValue << 8);
            }
        }

        public ZZClass Class1
        {
            get => (ZZClass)((Raw >> 4) & 0xf);
            set
            {
                var iValue = (uint)value;
                if (iValue > 15)
                    throw new ArgumentOutOfRangeException($"Invalid LevelData class 1: {value}");
                Raw = (Raw & 0xffff_ff0f) | (iValue << 4);
            }
        }

        public ZZClass Class2
        {
            get => (ZZClass)(Raw & 0xf);
            set
            {
                var iValue = (uint)value;
                if (iValue > 15)
                    throw new ArgumentOutOfRangeException($"Invalid LevelData class 2: {value}");
                Raw = (Raw & 0xffff_fff0) | iValue;
            }
        }
    }
}
