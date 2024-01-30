namespace zzio.db;

public class SpellRow : MappedRow
{
    public SpellRow(MappedDB mappedDB, Row row) : base(ModuleType.Spell, mappedDB, row) { }

    public string Name => foreignText(0);

    public int Type => row.cells[1].Integer;

    public CardId CardId => new(row.cells[2].Integer);

    public byte PriceA => row.cells[3].Byte;
    public byte PriceB => row.cells[4].Byte;
    public byte PriceC => row.cells[5].Byte;

    public string Info => foreignText(6);

    public int Mana => row.cells[7].Integer;

    public int Loadup => row.cells[8].Integer;

    public int Unknown => row.cells[9].Integer;

    public int MissileEffect => row.cells[10].Integer;

    public int ImpactEffect => row.cells[11].Integer;

    public int Damage => row.cells[12].Integer;

    public int Behavior => row.cells[13].Integer;

    public uint MaxMana => Mana switch
    {
        0 => 5,
        1 => 15,
        2 => 30,
        3 => 40,
        4 => 55,
        _ => 1000
    };
}
