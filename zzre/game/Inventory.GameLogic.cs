﻿using System;
using System.Linq;
using zzio;

namespace zzre.game
{
    public partial class Inventory
    {
        private const int MaxLevel = 60;
        private const int MaxXP = 15000;

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

        public void AddXP(InventoryFairy fairy, int moreXP)
        {
            fairy.xpChangeCount += (uint)moreXP;
            while (moreXP-- >= 0)
            {
                fairy.xp = Math.Min(MaxXP, fairy.xp + 1);
                var newLevel = GetLevelByXP(fairy);
                if (newLevel <= fairy.level)
                    continue;
                fairy.levelChangeCount++;
                fairy.level = newLevel;
            }
        }

        private uint GetLevelByXP(InventoryFairy fairy)
        {
            var levelUpFactor = mappedDB.GetFairy(fairy.dbUID).LevelUp * 0.001;
            return (uint)(Math.Pow(fairy.xp, levelUpFactor) * MaxLevel / Math.Pow(MaxXP, levelUpFactor));
        }

        private double GetLevelFactor(InventoryFairy fairy) => fairy.level / MaxLevel;
    }
}
