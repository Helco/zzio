using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using zzio.utils;
using zzio.primitives;

namespace zzio.db
{
    public class MappedDB
    {
        private Dictionary<ModuleType, Dictionary<UID, Row>> modules =
            new Dictionary<ModuleType, Dictionary<UID, Row>>();

        public MappedDB()
        {
            foreach (ModuleType moduleType in Enum.GetValues(typeof(ModuleType)))
                modules.Add(moduleType, new Dictionary<UID, Row>());
        }

        public void AddTable(Stream stream)
        {
            Table table = new Table();
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
        }

        public IEnumerable<FairyRow> Fairies
        {
            get
            {
                return modules[ModuleType.Fairy].Values
                    .Select(row => new FairyRow(this, row));
            }
        }

        public FairyRow GetFairy(UID uid)
        {
            return new FairyRow(this, modules[ModuleType.Fairy][uid]);
        }

        public IEnumerable<TextRow> Texts
        {
            get
            {
                return modules[ModuleType.Text].Values
                    .Select(row => new TextRow(this, row));
            }
        }

        public TextRow GetText(UID uid)
        {
            return new TextRow(this, modules[ModuleType.Text][uid]);
        }

        public IEnumerable<SpellRow> Spell
        {
            get
            {
                return modules[ModuleType.Spell].Values
                    .Select(row => new SpellRow(this, row));
            }
        }

        public SpellRow GetSpell(UID uid)
        {
            return new SpellRow(this, modules[ModuleType.Spell][uid]);
        }

        public IEnumerable<ItemRow> Items
        {
            get
            {
                return modules[ModuleType.Item].Values
                    .Select(row => new ItemRow(this, row));
            }
        }

        public ItemRow GetItem(UID uid)
        {
            return new ItemRow(this, modules[ModuleType.Item][uid]);
        }

        public IEnumerable<NpcRow> Npcs
        {
            get
            {
                return modules[ModuleType.Npc].Values
                    .Select(row => new NpcRow(this, row));
            }
        }

        public NpcRow GetNpc(UID uid)
        {
            return new NpcRow(this, modules[ModuleType.Npc][uid]);
        }

        public IEnumerable<DialogRow> Dialogs
        {
            get
            {
                return modules[ModuleType.Dialog].Values
                    .Select(row => new DialogRow(this, row));
            }
        }

        public DialogRow GetDialog(UID uid)
        {
            return new DialogRow(this, modules[ModuleType.Dialog][uid]);
        }
    }
}
