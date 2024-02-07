namespace zzre.game;
using System;
using System.Linq;
using zzio.db;

public enum StdSpellId
{
    SmallQuake = 0,
    SmallWave = 10,
    SmallStone = 30
}

public static class StdSpells
{
    private static (int attack, int support) GetMaxPriceFor(uint level) => (level / 5) switch
    {
        < 2 => (1, -1),
        2   => (1, 1),
        3   => (2, 1),
        < 6 => (2, 2),
        6   => (3, 2),
        _   => (3, 3)
    };

    private static bool IsSpellCompatible(SpellRow spell, zzio.ZZClass zzClass, uint level)
    {
        var price =
            (spell.PriceA != zzio.ZZClass.None ? 1 : 0) +
            (spell.PriceB != zzio.ZZClass.None ? 1 : 0) +
            (spell.PriceC != zzio.ZZClass.None ? 1 : 0);
        var maxPrice = GetMaxPriceFor(level);
        var maxRelevantPrice = spell.Type == 0 ? maxPrice.attack : maxPrice.support;
        return spell.PriceA == zzClass && price <= maxRelevantPrice;
    }

    public static StdSpellId GetStarterSpellFor(zzio.ZZClass zzClass) => zzClass switch
    {
        zzio.ZZClass.Nature => StdSpellId.SmallQuake,
        zzio.ZZClass.Water => StdSpellId.SmallWave,
        zzio.ZZClass.Stone => StdSpellId.SmallStone,
        _ => throw new ArgumentException($"Unknown starter class: {zzClass}")
    };

    public static SpellRow GetFirstAttackSpell(this MappedDB db, zzio.ZZClass zzClass, uint level) =>
        db.Spells.OrderBy(s => s.CardId.EntityId).First(s => s.Type == 0 && IsSpellCompatible(s, zzClass, level));

    public static (SpellRow? attack0, SpellRow? support0, SpellRow? attack1, SpellRow? support1) GetRandomSpellSet(this MappedDB db, Random random, zzio.ZZClass zzClass, uint level)
    {
        var compatibleSpells = db.Spells.Where(s => IsSpellCompatible(s, zzClass, level));
        var attackSpells = compatibleSpells.Where(s => s.Type == 0).ToArray();
        var supportSpells = compatibleSpells.Where(s => s.Type != 0).ToArray();

        for (int i = 0; i < Math.Min(2, attackSpells.Length); i++)
        {
            int j = random.Next(i, attackSpells.Length);
            (attackSpells[i], attackSpells[j]) = (attackSpells[j], attackSpells[i]);
        }

        for (int i = 0; i < Math.Min(2, supportSpells.Length); i++)
        {
            int j = random.Next(i, supportSpells.Length);
            (supportSpells[i], supportSpells[j]) = (supportSpells[j], supportSpells[i]);
        }

        return (
            attackSpells.FirstOrDefault(),
            supportSpells.FirstOrDefault(),
            attackSpells.Skip(1).FirstOrDefault(),
            supportSpells.Skip(1).FirstOrDefault());
    }
}
