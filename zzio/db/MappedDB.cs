using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace zzio.db
{
    public class MappedDB
    {
        private readonly Dictionary<ModuleType, Dictionary<UID, Row>> modules =
            new();

        private readonly ResettableLazy<IReadOnlyDictionary<int, FairyRow>> fairiesByIndex;
        private readonly ResettableLazy<IReadOnlyDictionary<int, ItemRow>> itemsByIndex;
        private readonly ResettableLazy<IReadOnlyDictionary<int, SpellRow>> spellsByIndex;

        public MappedDB()
        {
            foreach (var moduleType in Enum.GetValues(typeof(ModuleType)).Cast<ModuleType>())
                modules.Add(moduleType, new Dictionary<UID, Row>());
            fairiesByIndex = new ResettableLazy<IReadOnlyDictionary<int, FairyRow>>(
                () => Fairies.ToDictionary(f => f.CardId.EntityId, f => f));
            itemsByIndex = new ResettableLazy<IReadOnlyDictionary<int, ItemRow>>(
                () => Items.ToDictionary(i => i.CardId.EntityId, i => i));
            spellsByIndex = new ResettableLazy<IReadOnlyDictionary<int, SpellRow>>(
                () => Spells.ToDictionary(s => s.CardId.EntityId, s => s));
        }

        public void AddTable(Stream stream)
        {
            Table table = new();
            table.Read(stream);
            AddTable(table);
        }

        public void AddTable(Table table)
        {
            if (table.rows.Count <= 0)
                return;

            int moduleNumber = table.rows.First().Key.Module;
            ModuleType module = EnumUtils.intToEnum<ModuleType>(moduleNumber);
            if (module == ModuleType.Unknown)
                throw new InvalidDataException("Unknown database module");

            // Merge by overwriting previous defined rows
            modules[module] = modules[module].Concat(table.rows)
                .GroupBy(pair => pair.Key)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().Value
                );

            switch (module)
            {
                case ModuleType.Fairy: fairiesByIndex.Reset(); break;
                case ModuleType.Item: itemsByIndex.Reset(); break;
                case ModuleType.Spell: spellsByIndex.Reset(); break;
            }
        }

        public IEnumerable<FairyRow> Fairies => modules[ModuleType.Fairy].Values.Select(row => new FairyRow(this, row));
        public FairyRow GetFairy(UID uid) => new(this, modules[ModuleType.Fairy][uid]);
        public FairyRow GetFairy(int idx) => fairiesByIndex.Value[idx];

        public IEnumerable<TextRow> Texts => modules[ModuleType.Text].Values.Select(row => new TextRow(this, row));
        public TextRow GetText(UID uid) => new(this, modules[ModuleType.Text][uid]);

        public IEnumerable<SpellRow> Spells => modules[ModuleType.Spell].Values
                    .Select(row => new SpellRow(this, row));
        public SpellRow GetSpell(UID uid) => new(this, modules[ModuleType.Spell][uid]);
        public SpellRow GetSpell(int idx) => spellsByIndex.Value[idx];

        public IEnumerable<ItemRow> Items => modules[ModuleType.Item].Values
                    .Select(row => new ItemRow(this, row));
        public ItemRow GetItem(UID uid) => new(this, modules[ModuleType.Item][uid]);
        public ItemRow GetItem(int idx) => itemsByIndex.Value[idx];

        public IEnumerable<NpcRow> Npcs => modules[ModuleType.Npc].Values
                    .Select(row => new NpcRow(this, row));
        public NpcRow GetNpc(UID uid) => new(this, modules[ModuleType.Npc][uid]);
        public bool TryGetNpc(UID uid, [NotNullWhen(true)] out NpcRow? npc)
        {
            npc = modules[ModuleType.Npc].TryGetValue(uid, out var row)
                ? new NpcRow(this, row)
                : null;
            return npc != null;
        }

        public IEnumerable<DialogRow> Dialogs => modules[ModuleType.Dialog].Values
                    .Select(row => new DialogRow(this, row));
        public DialogRow GetDialog(UID uid) => new(this, modules[ModuleType.Dialog][uid]);
    }
}
