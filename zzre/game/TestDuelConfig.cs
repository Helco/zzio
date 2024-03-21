using zzio;

namespace zzre.game;

internal sealed partial class TestDuelConfig
{
    private const int MaxFairyId = (int)StdFairyId.Lana;
    private const int MaxSpellId = 96; // TODO: Check limits in TestDuelConfig
    private const int MaxLevel = 60;
    private const string ConfigurationSection = "zanzarah.wizform";

    public messages.StartDuel ConvertToMessage(zzio.db.MappedDB mappedDb)
    {
        var ecsOverworld = new DefaultEcs.World();
        var playerInventory = new Inventory(mappedDb);
        AddWizform(playerInventory, 0, Own1, Own1Level, Own1Attack1, Own1Attack2, Own1Support1, Own1Support2);
        AddWizform(playerInventory, 1, Own2, Own2Level, Own2Attack1, Own2Attack2, Own2Support1, Own2Support2);
        AddWizform(playerInventory, 2, Own3, Own3Level, Own3Attack1, Own3Attack2, Own3Support1, Own3Support2);
        AddWizform(playerInventory, 3, Own4, Own4Level, Own4Attack1, Own4Attack2, Own4Support1, Own4Support2);
        AddWizform(playerInventory, 4, Own5, Own5Level, Own5Attack1, Own5Attack2, Own5Support1, Own5Support2);
        var playerEntity = ecsOverworld.CreateEntity();
        playerEntity.Set(playerInventory);

        var enemyInventory = new Inventory(mappedDb);
        AddWizform(enemyInventory, 0, Other1, Other1Level, Other1Attack1, Other1Attack2, Other1Support1, Other1Support2);
        AddWizform(enemyInventory, 1, Other2, Other2Level, Other2Attack1, Other2Attack2, Other2Support1, Other2Support2);
        AddWizform(enemyInventory, 2, Other3, Other3Level, Other3Attack1, Other3Attack2, Other3Support1, Other3Support2);
        AddWizform(enemyInventory, 3, Other4, Other4Level, Other4Attack1, Other4Attack2, Other4Support1, Other4Support2);
        AddWizform(enemyInventory, 4, Other5, Other5Level, Other5Attack1, Other5Attack2, Other5Support1, Other5Support2);
        var enemyEntity = ecsOverworld.CreateEntity();
        enemyEntity.Set(enemyInventory);

        var savegame = new Savegame();
        playerInventory.Save(savegame);

        return new(
            savegame,
            playerEntity,
            [enemyEntity],
            SceneId: 0,
            CanFlee: false);
    }

    private static void AddWizform(Inventory inv, int slot, StdFairyId id, int level, int attack1, int attack2, int support1, int support2)
    {
        if ((int)id < 0 || (int)id > MaxFairyId || level < 0)
            return;
        var fairy = inv.AddFairy((int)id);
        inv.SetSlot(fairy, slot);
        inv.AddXP(fairy, inv.GetXPForLevel(fairy, (uint)level));
        if (attack1 >= 0)
            inv.SetSpellSlot(fairy, inv.AddSpell(attack1), 0);
        if (attack2 >= 0)
            inv.SetSpellSlot(fairy, inv.AddSpell(attack2), 2);
        if (support1 >= 0)
            inv.SetSpellSlot(fairy, inv.AddSpell(support1), 1);
        if (support2 >= 0)
            inv.SetSpellSlot(fairy, inv.AddSpell(support2), 3);
    }

    [Configuration(Key = "OWN_WIZFORM_1_ID", IsInteger = true, Min = -1, Max = MaxFairyId)]
    public StdFairyId Own1 = StdFairyId.Manox;
    [Configuration(Key = "OWN_WIZFORM_1_LEVEL", IsInteger = true, Min = 0, Max = MaxLevel)]
    public int Own1Level = 50;
    [Configuration(Key = "OWN_WIZFORM_1_FIRST_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own1Attack1 = 41;
    [Configuration(Key = "OWN_WIZFORM_1_SECOND_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own1Attack2 = 1;
    [Configuration(Key = "OWN_WIZFORM_1_FIRST_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own1Support1 = 60;
    [Configuration(Key = "OWN_WIZFORM_1_SECOND_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own1Support2 = 61;

    [Configuration(Key = "OWN_WIZFORM_2_ID", IsInteger = true, Min = -1, Max = MaxFairyId)]
    public StdFairyId Own2 = StdFairyId.Turnox;
    [Configuration(Key = "OWN_WIZFORM_2_LEVEL", IsInteger = true, Min = 0, Max = MaxLevel)]
    public int Own2Level = 50;
    [Configuration(Key = "OWN_WIZFORM_2_FIRST_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own2Attack1 = 5;
    [Configuration(Key = "OWN_WIZFORM_2_SECOND_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own2Attack2 = 6;
    [Configuration(Key = "OWN_WIZFORM_2_FIRST_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own2Support1 = 65;
    [Configuration(Key = "OWN_WIZFORM_2_SECOND_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own2Support2 = 60;

    [Configuration(Key = "OWN_WIZFORM_3_ID", IsInteger = true, Min = -1, Max = MaxFairyId)]
    public StdFairyId Own3 = (StdFairyId)71;
    [Configuration(Key = "OWN_WIZFORM_3_LEVEL", IsInteger = true, Min = 0, Max = MaxLevel)]
    public int Own3Level = 50;
    [Configuration(Key = "OWN_WIZFORM_3_FIRST_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own3Attack1 = 30;
    [Configuration(Key = "OWN_WIZFORM_3_SECOND_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own3Attack2 = 31;
    [Configuration(Key = "OWN_WIZFORM_3_FIRST_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own3Support1 = 90;
    [Configuration(Key = "OWN_WIZFORM_3_SECOND_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own3Support2 = 91;

    [Configuration(Key = "OWN_WIZFORM_4_ID", IsInteger = true, Min = -1, Max = MaxFairyId)]
    public StdFairyId Own4 = StdFairyId.Lighane;
    [Configuration(Key = "OWN_WIZFORM_4_LEVEL", IsInteger = true, Min = 0, Max = MaxLevel)]
    public int Own4Level = 50;
    [Configuration(Key = "OWN_WIZFORM_4_FIRST_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own4Attack1 = 25;
    [Configuration(Key = "OWN_WIZFORM_4_SECOND_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own4Attack2 = 85;
    [Configuration(Key = "OWN_WIZFORM_4_FIRST_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own4Support1 = 26;
    [Configuration(Key = "OWN_WIZFORM_4_SECOND_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own4Support2 = 96;

    [Configuration(Key = "OWN_WIZFORM_5_ID", IsInteger = true, Min = -1, Max = MaxFairyId)]
    public StdFairyId Own5 = StdFairyId.Driane;
    [Configuration(Key = "OWN_WIZFORM_5_LEVEL", IsInteger = true, Min = 0, Max = MaxLevel)]
    public int Own5Level = 50;
    [Configuration(Key = "OWN_WIZFORM_5_FIRST_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own5Attack1 = 10;
    [Configuration(Key = "OWN_WIZFORM_5_SECOND_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own5Attack2 = 11;
    [Configuration(Key = "OWN_WIZFORM_5_FIRST_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own5Support1 = 70;
    [Configuration(Key = "OWN_WIZFORM_5_SECOND_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Own5Support2 = 71;

    [Configuration(Key = "OTHER_WIZFORM_1_ID", IsInteger = true, Min = -1, Max = MaxFairyId)]
    public StdFairyId Other1 = StdFairyId.Beltaur;
    [Configuration(Key = "OTHER_WIZFORM_1_LEVEL", IsInteger = true, Min = 0, Max = MaxLevel)]
    public int Other1Level = 59;
    [Configuration(Key = "OTHER_WIZFORM_1_FIRST_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other1Attack1 = 20;
    [Configuration(Key = "OTHER_WIZFORM_1_SECOND_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other1Attack2 = -1;
    [Configuration(Key = "OTHER_WIZFORM_1_FIRST_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other1Support1 = 61;
    [Configuration(Key = "OTHER_WIZFORM_1_SECOND_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other1Support2 = -1;

    [Configuration(Key = "OTHER_WIZFORM_2_ID", IsInteger = true, Min = -1, Max = MaxFairyId)]
    public StdFairyId Other2 = StdFairyId.Mentaur;
    [Configuration(Key = "OTHER_WIZFORM_2_LEVEL", IsInteger = true, Min = 0, Max = MaxLevel)]
    public int Other2Level = 59;
    [Configuration(Key = "OTHER_WIZFORM_2_FIRST_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other2Attack1 = 36;
    [Configuration(Key = "OTHER_WIZFORM_2_SECOND_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other2Attack2 = -1;
    [Configuration(Key = "OTHER_WIZFORM_2_FIRST_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other2Support1 = 66;
    [Configuration(Key = "OTHER_WIZFORM_2_SECOND_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other2Support2 = -1;

    [Configuration(Key = "OTHER_WIZFORM_3_ID", IsInteger = true, Min = -1, Max = MaxFairyId)]
    public StdFairyId Other3 = StdFairyId.Clum;
    [Configuration(Key = "OTHER_WIZFORM_3_LEVEL", IsInteger = true, Min = 0, Max = MaxLevel)]
    public int Other3Level = 0;
    [Configuration(Key = "OTHER_WIZFORM_3_FIRST_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other3Attack1 = 39;
    [Configuration(Key = "OTHER_WIZFORM_3_SECOND_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other3Attack2 = -1;
    [Configuration(Key = "OTHER_WIZFORM_3_FIRST_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other3Support1 = 84;
    [Configuration(Key = "OTHER_WIZFORM_3_SECOND_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other3Support2 = -1;

    [Configuration(Key = "OTHER_WIZFORM_4_ID", IsInteger = true, Min = -1, Max = MaxFairyId)]
    public StdFairyId Other4 = StdFairyId.Clumaur;
    [Configuration(Key = "OTHER_WIZFORM_4_LEVEL", IsInteger = true, Min = 0, Max = MaxLevel)]
    public int Other4Level = 0;
    [Configuration(Key = "OTHER_WIZFORM_4_FIRST_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other4Attack1 = 36;
    [Configuration(Key = "OTHER_WIZFORM_4_SECOND_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other4Attack2 = 85;
    [Configuration(Key = "OTHER_WIZFORM_4_FIRST_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other4Support1 = -1;
    [Configuration(Key = "OTHER_WIZFORM_4_SECOND_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other4Support2 = -1;

    [Configuration(Key = "OTHER_WIZFORM_5_ID", IsInteger = true, Min = -1, Max = MaxFairyId)]
    public StdFairyId Other5 = (StdFairyId)60;
    [Configuration(Key = "OTHER_WIZFORM_5_LEVEL", IsInteger = true, Min = 0, Max = MaxLevel)]
    public int Other5Level = 0;
    [Configuration(Key = "OTHER_WIZFORM_5_FIRST_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other5Attack1 = 24;
    [Configuration(Key = "OTHER_WIZFORM_5_SECOND_ATTACK_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other5Attack2 = -1;
    [Configuration(Key = "OTHER_WIZFORM_5_FIRST_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other5Support1 = 70;
    [Configuration(Key = "OTHER_WIZFORM_5_SECOND_SUPPORT_SPELL", IsInteger = true, Min = -1, Max = MaxSpellId)]
    public int Other5Support2 = -1;
}
