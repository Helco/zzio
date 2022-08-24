namespace zzre.game;
using System;
using System.Linq;
using static zzre.game.StdFairyId;

public enum StdFairyId
{
    Sillia = 0,
    Viteria = 1,
    Boneria = 3,
    Gremor = 4,
    Gremrock = 5,
    Tadana = 6,
    Worgot = 9,
    Airia = 12,
    Luria = 13,
    Corgot = 10,
    Rasrow = 15,
    Maulrow = 16,
    Psyrow = 17,
    Abery = 18,
    Abnobery = 19,
    Vesbat = 20,
    Stobat = 21,
    Cera = 23,
    Amnis = 24,
    Ceramnis = 25,
    Blumella = 26,
    Mencre = 27,
    Mensec = 28,
    Violectra = 29,
    Pix = 31,
    Ferix = 32,
    Feez = 34,
    Greez = 35,
    Greezloc = 36,
    Skelbo = 37,
    Sirael = 40,
    Gorael = 41,
    Fathrael = 42,
    Darbue = 43,
    Beltaur = 46,
    Mentaur = 47,
    Clum = 48,
    Clumaur = 49,
    Pfoe = 50,
    Glacess = 52,
    Akritar = 53,
    Sirella = 54,
    Dracwin = 55,
    Tinefol = 57,
    Tinezard = 59,
    Fygaery = 61,
    Jumjum = 62,
    Jumrock = 63,
    Goop = 64,
    Minari = 65,
    Megari = 66,
    Manox = 69,
    Turnox = 70,
    Lighane = 72,
    Driane = 73,
    Lana = 75,
}

public static class StdFairyGroups
{
    private readonly struct Group
    {
        public readonly StdFairyId[] Fairies;
        public readonly int Extra; // either level range or deck size

        public Group(int extra, params StdFairyId[] fairies) => (Extra, Fairies) = (extra, fairies);
    }

    private static readonly Group[] AttackGroups =
    {
        new(5, Worgot, Blumella),
        new(13, Corgot, Lana),
        new(36, Tinefol, Viteria, Sillia),
        new(24, Pfoe, Tinefol),
        new(13, Abery, Corgot),
        new(24, Worgot, Abnobery),
        new(24, Abery, Sillia),
        new(13, Cera),
        new(24, Goop, Cera),
        new(36, Goop, Tadana),
        new(42, Tadana, Tadana, Amnis, Ceramnis),
        new(5, Vesbat, Vesbat),
        new(13, Jumjum, Vesbat),
        new(24, Gremor, Stobat),
        new(42, Boneria, Jumjum),
        new(13, Mencre, Mensec),
        new(24, Mencre, Mentaur),
        new(36, Mencre, Mentaur),
        new(42, Beltaur, Beltaur, Mencre),
        new(36, Airia, Luria),
        new(36, Gorael, Sirael),
        new(42, Sirella, Sirael),
        new(42, Dracwin, Dracwin),
        new(65, Pix, Pix, Ferix),
        new(36, Feez, Greez),
        new(42, Greezloc, Glacess),
        new(24, Rasrow, Skelbo),
        new(36, Rasrow, Skelbo),
        new(42, Rasrow, Akritar, Manox),
        new(54, Manox, Turnox, Violectra),
        new(36, Minari),
        new(42, Minari),
        new(54, Minari, Megari, Violectra),
        new(54, Lighane, Lighane, Driane, Darbue)
    };

    private static readonly Group[] DeckGroups =
    {
        new(3, Rasrow, Maulrow, Psyrow, Vesbat, Feez, Pix, Akritar, Boneria),
        new(4, Greez, Stobat, Psyrow, Clum, Ferix, Sirael, Gremor),
        new(5, Fygaery, Jumrock, Psyrow, Turnox, Clumaur, Tinezard, Goop, Greezloc, Fathrael, Gremrock)
    };

    public static int GetLevel(Random random, int levelRange)
    {
        int ampl = levelRange / 4;
        int baseLevel = levelRange - ampl - 1;
        return baseLevel + random.Next(ampl);
    }

    public static (StdFairyId fairy, int level) GetFromAttackGroup(Random random, int groupI)
    {
        var group = AttackGroups[groupI];
        return (random.NextOf(group.Fairies), GetLevel(random, group.Extra));
    }

    public static (StdFairyId fairy, int level)[] GetFromDeck(Random random, int groupI, int levelRange)
    {
        var group = DeckGroups[groupI];
        return Enumerable
            .Repeat(0, group.Extra)
            .Select(_ => (random.NextOf(group.Fairies), GetLevel(random, levelRange)))
            .ToArray();
    }
}
