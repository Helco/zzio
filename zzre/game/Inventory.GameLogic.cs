using System;
using System.Linq;
using zzio;

namespace zzre.game;

public partial class Inventory
{
    public const int FairySlotCount = 5;
    private const int MaxLevel = 60;
    private const int MaxXP = 15000;
    private const double XPExponentFactor = 0.001;

    public void SetLevel(InventoryFairy fairy, int level)
    {
        fairy.levelChangeCount++;
        fairy.level = (uint)level;
        Update(fairy);
        fairy.currentMHP = fairy.maxMHP;
        FillManaAfterLevelup(fairy);
    }

    private void Update(InventoryFairy fairy)
    {
        UpdateMaxMHP(fairy);
        UpdateMoveSpeed(fairy);
        UpdateJumpPower(fairy);
        UpdateCriticalHit(fairy);
    }

    private void UpdateMaxMHP(InventoryFairy fairy)
    {
        var levelFactor = GetLevelFactor(fairy);
        var reallyMaxMHP = mappedDB.GetFairy(fairy.dbUID).MHP;
        var oneTenth = reallyMaxMHP / 10.0;
        fairy.maxMHP = (uint)(levelFactor * (reallyMaxMHP - oneTenth) + oneTenth);
    }

    private void UpdateMoveSpeed(InventoryFairy fairy)
    {
        var baseMoveSpeed = mappedDB.GetFairy(fairy.dbUID).BaseMoveSpeed;
        var levelFactor = Math.Max(0.001, GetLevelFactor(fairy));
        fairy.moveSpeed = (float)(
            baseMoveSpeed / 2 * levelFactor +
            baseMoveSpeed / 2);
        if (fairy.status == ZZPermSpellStatus.Burned)
            fairy.moveSpeed *= 0.7f;
    }

    private void UpdateJumpPower(InventoryFairy fairy)
    {
        var baseJumpPower = mappedDB.GetFairy(fairy.dbUID).BaseJumpPower;
        var levelFactor = Math.Max(0.001, GetLevelFactor(fairy));
        fairy.jumpPower = (float)(
            baseJumpPower * 0.8 * levelFactor +
            baseJumpPower * 0.2);
        if (fairy.status == ZZPermSpellStatus.Burned)
            fairy.jumpPower *= 0.7f;
    }

    private void UpdateCriticalHit(InventoryFairy fairy)
    {
        var baseCrit = mappedDB.GetFairy(fairy.dbUID).BaseCriticalHit;
        var levelFactor = Math.Max(0.001, GetLevelFactor(fairy));
        fairy.criticalHit = (float)(baseCrit * levelFactor);
        if (fairy.status == ZZPermSpellStatus.Burned)
            fairy.criticalHit *= 0.5f;
    }

    private void FillManaAfterLevelup(InventoryFairy fairy)
    {
        foreach (int spellIndex in fairy.spellIndices.Where(i => i >= 0))
        {
            var spell = (InventorySpell)cards[spellIndex]!;
            var maxMana = mappedDB.GetSpell(spell.dbUID).MaxMana;
            AddMana(spell, (int)(maxMana - spell.mana) / 2);
        }
    }

    public void FillMana()
    {
        foreach (var fairy in fairySlots.NotNull())
            FillMana(fairy);
    }

    public void FillMana(InventoryFairy fairy)
    {
        foreach (int spellIndex in fairy.spellIndices.Where(i => i >= 0))
            FillMana((InventorySpell)cards[spellIndex]!);
    }

    public void FillMana(InventorySpell spell)
    {
        spell.mana = mappedDB.GetSpell(spell.dbUID).MaxMana;
    }

    public void AddMana(InventorySpell spell, int delta)
    {
        var dbRow = mappedDB.GetSpell(spell.dbUID);
        if (dbRow.Mana == 5)
            return;
        spell.usageCounter++;
        spell.mana = (uint)Math.Clamp((int)spell.mana + delta, 0, dbRow.MaxMana);
    }

    public void AddXP(InventoryFairy fairy, uint moreXP)
    {
        fairy.xpChangeCount += moreXP;
        for (uint i = 0; i < moreXP; i++)
        {
            // TODO: Refactor original XP increase method
            fairy.xp = Math.Min(MaxXP, fairy.xp + 1);
            var newLevel = GetLevelByXP(fairy);
            if (newLevel <= fairy.level)
                continue;
            // TODO: Handle fairy attribute change upon level change
            fairy.levelChangeCount++;
            fairy.level = newLevel;
        }
        Update(fairy);
    }

    private uint GetLevelByXP(InventoryFairy fairy)
    {
        var levelUpFactor = mappedDB.GetFairy(fairy.dbUID).LevelUp * 0.001;
        return (uint)(Math.Pow(fairy.xp, levelUpFactor) * MaxLevel / Math.Pow(MaxXP, levelUpFactor));
    }

    private static double GetLevelFactor(InventoryFairy fairy) => fairy.level / (double)MaxLevel;

    public void SetSlot(InventoryFairy fairy, int newSlotI)
    {
        if (fairy.slotIndex >= 0)
            fairySlots[fairy.slotIndex] = null;

        var swappedFairy = newSlotI < 0 ? null : fairySlots[newSlotI];
        if (swappedFairy != null)
            SetSlotRaw(swappedFairy, fairy.slotIndex);
        SetSlotRaw(fairy, newSlotI);

        void SetSlotRaw(InventoryFairy fairy, int newSlotI)
        {
            fairy.slotIndex = newSlotI;
            fairy.isInUse = newSlotI >= 0;
            if (newSlotI >= 0)
                fairySlots[newSlotI] = fairy;
        }
    }

    /// <returns>Index of slot or -1 if no slot was free</returns>
    public int SetFirstFreeSlot(InventoryFairy fairy)
    {
        var slotI = fairySlots.IndexOf(null as InventoryFairy);
        if (slotI >= 0)
            SetSlot(fairy, slotI);
        return slotI;
    }

    public void SetSpellSlot(InventoryFairy fairy, InventorySpell spell, int spellSlotI)
    {
        var spellIndex = IndexOf(spell);
        if (spellIndex < 0)
            throw new ArgumentException($"Cannot set spell slot as spell {spell} is not present in inventory");
        fairy.spellIndices[spellSlotI] = spellIndex;
        spell.isInUse = true;
    }

    public void ClearDeck()
    {
        for (int i = 0; i < fairySlots.Length; i++)
        {
            if (fairySlots[i] != null)
            {
                fairySlots[i]!.isInUse = false;
                fairySlots[i]!.slotIndex = -1;
            }
            fairySlots[i] = null;
        }
    }

    public uint GetXPForLevel(InventoryFairy fairy, uint level)
    {
        var dbFairy = mappedDB.GetFairy(fairy.dbUID);
        var baseXP = Math.Pow(MaxXP, dbFairy.LevelUp * XPExponentFactor);
        var exp = 1 / (dbFairy.LevelUp * XPExponentFactor);
        return (uint)Math.Pow(baseXP * level / MaxLevel, exp);
    }

    public uint? GetLevelupXP(InventoryFairy fairy) => fairy.level >= MaxLevel - 1
        ? null
        : GetXPForLevel(fairy, fairy.level + 1);
}
