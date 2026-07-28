namespace zzio.db;

public class SpellRow : MappedRow
{
    public SpellRow(MappedDB mappedDB, Row row) : base(ModuleType.Spell, mappedDB, row) { }

    public string Name => foreignText(0);

    public int Type => row.cells[1].Integer;

    public CardId CardId => new(row.cells[2].Integer);

    public ZZClass PriceA => (ZZClass)row.cells[3].Byte;
    public ZZClass PriceB => (ZZClass)row.cells[4].Byte;
    public ZZClass PriceC => (ZZClass)row.cells[5].Byte;

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

    /// <summary>Damage this spell deals before any modifiers</summary>
    public int BaseDamage => BaseDamageOf(Damage);

    /// <summary>How fast this spell charges up while its button is held</summary>
    public double BaseLoadupRate => BaseLoadupRateOf(Loadup);

    /// <summary>Base damage for a damage step, as the original looks it up</summary>
    /// <remarks>FUN_0044b4d6. Support spells always carry step 0.</remarks>
    public static int BaseDamageOf(int damage) => damage switch
    {
        1 => 65,
        2 => 70,
        3 => 95,
        4 => 110,
        _ => 50
    };

    /// <summary>Charge rate for a loadup step, as the original looks it up</summary>
    /// <remarks>FUN_0043addc. A higher step charges <i>faster</i>.</remarks>
    public static double BaseLoadupRateOf(int loadup) => loadup switch
    {
        1 => 0.7,
        2 => 1.0,
        3 => 1.3,
        4 => 1.7,
        _ => 0.4
    };
}
