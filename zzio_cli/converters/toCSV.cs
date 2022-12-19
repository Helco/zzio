using System;
using System.Text;
using System.IO;
using System.Linq;
using zzio.db;

namespace zzio.cli.converters
{
    public class FBStoCSV : IConverter
    {
        public FileType TypeFrom { get { return FileType.FBS_Data; } }
        public FileType TypeTo { get { return FileType.CSV; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            var table = new Table();
            table.Read(from);
            var mappedDb = new MappedDB();
            mappedDb.AddTable(table);

            StreamWriter writer = new(to,
                Encoding.UTF8, 1024, true);
            switch ((ModuleType)table.rows.First().Key.Module)
            {
                default:
                case ModuleType.Unknown: writeUnknownTable(writer, table); break;
                case ModuleType.Dialog: writeDialogTable(writer, mappedDb); break;
                case ModuleType.Fairy: writeFairyTable(writer, mappedDb); break;
                case ModuleType.Item: writeItemTable(writer, mappedDb); break;
                case ModuleType.Npc: writeNpcTable(writer, mappedDb); break;
                case ModuleType.Spell: writeSpellTable(writer, mappedDb); break;
                case ModuleType.Text: writeTextTable(writer, mappedDb); break;
            }
        }

        private string escapeString(string str)
        {
            return "\"" + str.Replace("\"", "\"\"") + "\"";
        }

        private void writeCells(StreamWriter writer, params object[] args)
        {
            writer.WriteLine(string.Join(",", args.Select(arg =>
            {
                if (arg is string)
                    return escapeString((string)arg);
                return arg.ToString();
            })));
        }

        private void writeTextTable(StreamWriter writer, MappedDB mappedDb)
        {
            writer.WriteLine("# UID, Text, Group, Define");
            foreach (TextRow text in mappedDb.Texts)
                writeCells(writer,
                    text.Uid,
                    text.Text,
                    text.Group,
                    text.Define);
        }

        private void writeSpellTable(StreamWriter writer, MappedDB mappedDb)
        {
            writer.WriteLine(
                "# UID, Name, Type, CardId, PriceA, PriceB, PriceC, " +
                "Info, Mana, Loadup, Unknown, MissileEffect, ImpactEffect" +
                "Damage, Behavior");
            foreach (SpellRow spell in mappedDb.Spells)
                writeCells(writer,
                    spell.Uid,
                    spell.Name,
                    spell.Type,
                    spell.CardId,
                    spell.PriceA,
                    spell.PriceB,
                    spell.PriceC,
                    spell.Info,
                    spell.Mana,
                    spell.Loadup,
                    spell.Unknown,
                    spell.MissileEffect,
                    spell.ImpactEffect,
                    spell.Damage,
                    spell.Behavior);
        }

        private void writeNpcTable(StreamWriter writer, MappedDB mappedDb)
        {
            writer.WriteLine(
                "# UID, Name, Script1, Script2, Script3, Script4, Script5, Unknown");
            foreach (NpcRow npc in mappedDb.Npcs)
                writeCells(writer,
                    npc.Uid,
                    npc.Name,
                    npc.TriggerScript,
                    npc.InitScript,
                    npc.UpdateScript,
                    npc.DefeatedScript,
                    npc.VictoriousScript,
                    npc.InternalName);
        }

        private void writeItemTable(StreamWriter writer, MappedDB mappedDb)
        {
            writer.WriteLine(
                "# UID, Name, CardId, Info, Special, Script, Unknown");
            foreach (ItemRow item in mappedDb.Items)
                writeCells(writer,
                    item.Uid,
                    item.Name,
                    item.CardId,
                    item.Info,
                    item.Special,
                    item.Script,
                    item.Unknown);
        }

        private void writeFairyTable(StreamWriter writer, MappedDB mappedDb)
        {
            writer.WriteLine(
                "# UID, Mesh, Name, Class0, CardId, Unknown, Level0, Level1, " +
                "Level2, Level3, Level4, Level5, Level6, Level7, Level8, " +
                "Level9, Info, MHP, EvolCId, EvolVar, MovSpeed, JumpPower, " +
                "CriticalHit, Sphere, Glow, LevelUp, Voice, Class1");
            foreach (FairyRow fairy in mappedDb.Fairies)
                writeCells(writer,
                    fairy.Uid,
                    fairy.Mesh,
                    fairy.Name,
                    fairy.Class0,
                    fairy.CardId,
                    fairy.Unknown,
                    fairy.Level0,
                    fairy.Level1,
                    fairy.Level2,
                    fairy.Level3,
                    fairy.Level4,
                    fairy.Level5,
                    fairy.Level6,
                    fairy.Level7,
                    fairy.Level8,
                    fairy.Level9,
                    fairy.Info,
                    fairy.MHP,
                    fairy.EvolCId,
                    fairy.EvolVar,
                    fairy.MovSpeed,
                    fairy.JumpPower,
                    fairy.CriticalHit,
                    fairy.Sphere,
                    fairy.Glow,
                    fairy.LevelUp,
                    fairy.Unknown24,
                    fairy.WingSound);
        }

        private void writeDialogTable(StreamWriter writer, MappedDB mappedDb)
        {
            writer.WriteLine("# UID, Text, Npc, Voice");
            foreach (DialogRow dialog in mappedDb.Dialogs)
                writeCells(writer,
                    dialog.Uid,
                    dialog.Text,
                    dialog.Npc,
                    dialog.Voice);
        }

        private void writeUnknownTable(StreamWriter writer, Table table)
        {
            foreach (Row row in table.rows.Values)
            {
                var cellStrings = row.cells.Select(cell =>
                {
                    switch (cell.Type)
                    {
                        case CellDataType.Byte: return cell.Byte.ToString();
                        case CellDataType.Integer: return cell.Integer.ToString();
                        case CellDataType.String: return escapeString(cell.String);
                        case CellDataType.ForeignKey: return cell.ForeignKey.ToString();
                        case CellDataType.Buffer:
                        case CellDataType.Unknown:
                        default:
                            return BitConverter.ToString(cell.Buffer).Replace("-", "");
                    }
                });
                writer.WriteLine(row.uid.ToString() + "," + string.Join(',', cellStrings));
            }
        }
    }
}
