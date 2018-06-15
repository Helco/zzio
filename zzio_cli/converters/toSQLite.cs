using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using SQLite;

namespace zzio.cli.converters
{
    public static class SQLiteConvHelper
    {
        public static string[][] mapping = new string[][]
        {
            new string[] {
                "Mesh", "Name", "Class0", "CardId", "unused1", "Level0", "Level1", "Level2", "Level3", "Level4", "Level5",
                "Level6", "Level7", "Level8", "Level9", "Info", "MHP", "EvolCId", "EvolVar", "MovSpeed", "JumpPower",
                "CriticalHit", "unknown", "Glow", "unused2", "Levelup", "WingSound"
            },
            new string[] { "Text", "Group", "Define" },
            new string[] {
                "Name", "Type", "CardId", "PriceA", "PriceB", "PriceC", "Info", "Mana", "Loadup", "Trajectory",
                "MissileEffect", "ImpactEffect", "Damage", "Behaviour"
            },
            new string[] {
                "Name", "CardId", "Info", "Special", "Script", "Group"
            },
            new string[] {
                "Name", "Script1", "Script2", "Script3", "Script4", "Script5", "Define"
            },
            new string[] {
                "Text", "Npc", "Voice", "Define"
            }
        };

        //resolve Name FK, 
        public static string[] sqlCreateHR = new string[]
        {
            //resolve Name FK, resolve Info FK, split CardId, split Level information
            "CREATE VIEW hr_fb0x01 AS " +
            "SELECT fb0x01.uid as uid, Mesh, fb0x02.Text AS Name, fb0x02Info.Text AS Info, Class0, " +
            "printf(\"%d_%d_%d\", (CardId >> 16), ((CardId >> 8) & 7), (CardId & 7)) AS CardId, " +
            "printf('%d,%d,%d,%d,%d', (Level0 & 15), ((Level0 >> 4) & 15), ((Level0 >> 8) & 15), ((Level0 >> 12) & 3), (Level0 >> 16)) AS Level0, " +
            "printf('%d,%d,%d,%d,%d', (Level1 & 15), ((Level1 >> 4) & 15), ((Level1 >> 8) & 15), ((Level1 >> 12) & 3), (Level1 >> 16)) AS Level1, " +
            "printf('%d,%d,%d,%d,%d', (Level2 & 15), ((Level2 >> 4) & 15), ((Level2 >> 8) & 15), ((Level2 >> 12) & 3), (Level2 >> 16)) AS Level2, " +
            "printf('%d,%d,%d,%d,%d', (Level3 & 15), ((Level3 >> 4) & 15), ((Level3 >> 8) & 15), ((Level3 >> 12) & 3), (Level3 >> 16)) AS Level3, " +
            "printf('%d,%d,%d,%d,%d', (Level4 & 15), ((Level4 >> 4) & 15), ((Level4 >> 8) & 15), ((Level4 >> 12) & 3), (Level4 >> 16)) AS Level4, " +
            "printf('%d,%d,%d,%d,%d', (Level5 & 15), ((Level5 >> 4) & 15), ((Level5 >> 8) & 15), ((Level5 >> 12) & 3), (Level5 >> 16)) AS Level5, " +
            "printf('%d,%d,%d,%d,%d', (Level6 & 15), ((Level6 >> 4) & 15), ((Level6 >> 8) & 15), ((Level6 >> 12) & 3), (Level6 >> 16)) AS Level6, " +
            "printf('%d,%d,%d,%d,%d', (Level7 & 15), ((Level7 >> 4) & 15), ((Level7 >> 8) & 15), ((Level7 >> 12) & 3), (Level7 >> 16)) AS Level7, " +
            "printf('%d,%d,%d,%d,%d', (Level8 & 15), ((Level8 >> 4) & 15), ((Level8 >> 8) & 15), ((Level8 >> 12) & 3), (Level8 >> 16)) AS Level8, " +
            "printf('%d,%d,%d,%d,%d', (Level9 & 15), ((Level9 >> 4) & 15), ((Level9 >> 8) & 15), ((Level9 >> 12) & 3), (Level9 >> 16)) AS Level9, " +
            "MHP, EvolCId, EvolVar, MovSpeed, JumpPower, CriticalHit, Glow, Levelup, WingSound, fb0x01.unknown, unused1, unused2 " +
            "FROM fb0x01 " +
            "LEFT JOIN fb0x02 ON fb0x02.uid = substr(fb0x01.Name, 8) " +
            "LEFT JOIN fb0x02 AS fb0x02Info ON fb0x02Info.uid = substr(fb0x01.Info, 8) ",

            null, //nothing for fb0x02 (at least atm)

            //resolve Name FK, resolve Info FK, Split CardId
            "CREATE VIEW hr_fb0x03 AS " +
            "SELECT fb0x03.uid, fb0x02.Text AS Name, Type, " +
            "printf(\"%d_%d_%d\", (CardId >> 16), ((CardId >> 8) & 7), (CardId & 7)) AS CardId, " +
            "PriceA, PriceB, PriceC, fb0x02Info.Text AS Info, Mana, Loadup, Trajectory, " +
            "MissileEffect, ImpactEffect, Damage, Behaviour " +
            "FROM fb0x03 " +
            "LEFT JOIN fb0x02 ON fb0x02.uid = substr(fb0x03.Name, 8) " +
            "LEFT JOIN fb0x02 AS fb0x02Info ON fb0x02Info.uid = substr(fb0x03.Info, 8) ",

            //resolve Name FK, resolve Info FK, split CardId
            "CREATE VIEW hr_fb0x04 AS " +
            "SELECT fb0x04.uid AS uid, fb0x02.Text AS Name, " +
            "printf(\"%d_%d_%d\", (CardId >> 16), ((CardId >> 8) & 7), (CardId & 7)) AS CardId, " +
            "fb0x02Info.Text AS Info, Special, Script, fb0x04.`Group` " +
            "FROM fb0x04 " +
            "INNER JOIN fb0x02 ON fb0x02.uid = substr(fb0x04.Name, 8) " +
            "LEFT JOIN fb0x02 AS fb0x02Info ON fb0x02Info.uid = substr(fb0x04.Info, 8) ",

            //resolve Name FK
            "CREATE VIEW hr_fb0x05 AS " +
            "SELECT fb0x05.uid, fb0x02.Text AS Name, Script1, Script2, Script3, Script4, Script5, fb0x05.`Define` " +
            "FROM fb0x05 " +
            "INNER JOIN fb0x02 ON fb0x02.uid = substr(fb0x05.Name, 8) ",

            null //nothing for fb0x06 (at least atm)
        };

        private static SQLiteConnection conn = null;
        private static ParameterParser args;
        public static ZZDatabase[] modules = new ZZDatabase[6] { null, null, null, null, null, null };

        public static bool initialize(ParameterParser args)
        {
            SQLiteConvHelper.args = args;
            string fn = Path.GetFullPath(Path.Combine(args["output"] as string, args["sql-output"] as string));
            try
            {
                if (File.Exists(fn))
                    File.Delete(fn);
                conn = new SQLiteConnection(fn);
                return true;
            }
            catch(Exception e)
            {
                Console.Error.WriteLine("Could not open database at " + fn + ": " + e.Message);
                return false;
            }
        }

        public static void finish()
        {
            //decompile
            if ((bool)args["sql-human"])
            {
                bool noDecompile = false;
                for (int i=0; i<6; i++)
                {
                    if (modules[i] != null && sqlCreateHR[i] != null)
                    {
                        try
                        {
                            SQLiteCommand comm = createCommand(sqlCreateHR[i]);
                            if ((bool)args["sql-tables"] || ((bool)args["db-decompile"] && (i == 3 || i == 4)))
                                comm.CommandText = comm.CommandText.Replace("VIEW", "TABLE");
                            comm.ExecuteNonQuery();
                        }
                        catch(Exception e)
                        {
                            noDecompile = noDecompile || i == 3 || i == 4;
                            Console.Error.WriteLine("Warning: Could not create human readable view/table for module " + (i + 1) + " (" + e.Message + ")");
                        }
                    }
                }

                if ((bool)args["db-decompile"] && !noDecompile)
                {
                    int i;
                    for (i = 0; i < 6; i++)
                    {
                        if (modules[i] == null)
                            break;
                    }
                    if (i < 6)
                        Console.Error.WriteLine("Warning: Could not decompile scripts, all modules are required for this task");
                    else
                    {
                        ZZMappedDatabase mappedDB = new ZZMappedDatabase(modules);
                        var decompiler = new script.Decompiler(mappedDB);

                        //decompile scripts
                        for (int modI = 3; modI < 5; modI++) //for modules 4 and 5
                        {
                            try
                            {
                                for (i = 0; i < modules[modI].rows.Length; i++)
                                {
                                    SQLiteCommand comm = createCommand("UPDATE hr_fb0x0" + (modI + 1) + " SET ");
                                    for (int colI = 0; colI < mapping[modI].Length; colI++)
                                    {
                                        if (mapping[modI][colI].IndexOf("Script") >= 0)
                                        {
                                            if (!decompiler.decompile(modules[modI].rows[i].columns[colI].data as string))
                                            {
                                                Console.Error.WriteLine("Script errors for hr_fb0x05 at " + i + " in column " + mapping[modI][colI]);
                                                Console.Error.WriteLine("------------------------------------");
                                                foreach (string msg in decompiler.getErrorMessages())
                                                    Console.Error.WriteLine(msg);
                                                Console.Error.WriteLine();
                                            }
                                            if (!comm.CommandText.EndsWith(" "))
                                                comm.CommandText += ", ";
                                            comm.CommandText += "`" + mapping[modI][colI] + "`=@" + colI;
                                            comm.Bind(colI.ToString(), decompiler.getResult());
                                        }
                                    }
                                    comm.CommandText += " WHERE uid=@uid";
                                    comm.Bind("uid", modules[modI].rows[i].uid.ToString("X"));
                                    comm.ExecuteNonQuery();
                                }
                            }
                            catch (Exception e)
                            {
                                Console.Error.WriteLine("Canceled decompiling scripts for fb0x0" + (modI + 1) + ": " + e.Message);
                            }
                        }
                    }
                }
            }

            //clean up
            if (conn != null)
                conn.Close();
            conn = null;
        }

        public static void executeNonQuery(string command)
        {
            SQLiteCommand comm = conn.CreateCommand(command);
            comm.ExecuteNonQuery();
        }

        public static SQLiteCommand createCommand(string command)
        {
            SQLiteCommand comm = conn.CreateCommand(command);
            return comm;
        }
    }

    public class FBS_IndextoSQLite : IConverter
    {
        public FileType TypeFrom { get { return FileType.FBS_Index; } }
        public FileType TypeTo { get { return FileType.SQLite; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            byte[] buffer = new byte[from.Length];
            from.Read(buffer, 0, (int)from.Length);
            var obj = ZZDatabaseIndex.read(buffer);

            SQLiteConvHelper.executeNonQuery("DROP TABLE IF EXISTS fb0x00");
            SQLiteConvHelper.executeNonQuery(
                "CREATE TABLE fb0x00 (idx INTEGER PRIMARY KEY, name TEXT, num INTEGER)");
            for (uint i=0; i<obj.columnCount; i++)
            {
                SQLiteCommand comm = SQLiteConvHelper.createCommand(
                    "INSERT INTO fb0x00 (idx, name, num) VALUES (@idx, @name, @num)");
                comm.Bind("idx", i);
                comm.Bind("name", obj.columnNames[i]);
                comm.Bind("num", obj.columnNumbers[i]);
                comm.ExecuteNonQuery();
            }
        }
    }

    public class FBS_DatatoSQLite : IConverter
    {
        public FileType TypeFrom { get { return FileType.FBS_Data; } }
        public FileType TypeTo { get { return FileType.SQLite; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            var modIStr = Path.GetFileNameWithoutExtension(name);
            modIStr = "" + modIStr[modIStr.Length - 1];
            int moduleIndex;
            if (!int.TryParse(modIStr, out moduleIndex) || moduleIndex > 6 || moduleIndex < 1)
                throw new Exception("Invalid database module name \"" + name + "\"");
            var tabName = "fb0x0" + modIStr;

            byte[] buffer = new byte[from.Length];
            from.Read(buffer, 0, (int)from.Length);
            var db = ZZDatabase.read(buffer);
            SQLiteConvHelper.modules[moduleIndex - 1] = db;

            //Create table
            SQLiteConvHelper.executeNonQuery("DROP TABLE IF EXISTS " + tabName);
            SQLiteCommand comm = SQLiteConvHelper.createCommand(
                "CREATE TABLE " + tabName + " ( uid TEXT PRIMARY KEY");
            for (UInt32 i = 0; i < db.rows[0].columns.Length; i++)
            {
                comm.CommandText += ", `" + SQLiteConvHelper.mapping[moduleIndex-1][i] + "` ";
                switch (db.rows[0].columns[i].entryType)
                {
                    case (ZZDBDataType.String):
                    case (ZZDBDataType.UUID): { comm.CommandText += "TEXT"; } break;
                    case (ZZDBDataType.UInt):
                    case (ZZDBDataType.Byte): { comm.CommandText += "INTEGER"; } break;
                    default: { comm.CommandText += "BLOB"; } break;
                }
            }
            comm.CommandText += " );";
            comm.ExecuteNonQuery();

            //Fill table
            for (UInt32 idx = 0; idx < db.rows.Length; idx++)
            {
                ZZDBRow r = db.rows[idx];
                comm = SQLiteConvHelper.createCommand(
                    "INSERT INTO " + tabName + "(uid");
                string paramCmdText = ") VALUES (@uid";
                comm.Bind("@uid", r.uid.ToString("X"));
                for (UInt32 i = 0; i < db.rows[idx].columns.Length; i++)
                {
                    comm.CommandText += ", `" + SQLiteConvHelper.mapping[moduleIndex-1][i] + "`";
                    paramCmdText += ", @" + i.ToString();
                    if (db.rows[idx].columns[i].entryType == ZZDBDataType.UUID)
                        comm.Bind(i.ToString(), db.rows[idx].columns[i].data.ToString());
                    else
                        comm.Bind(i.ToString(), db.rows[idx].columns[i].data);
                }
                comm.CommandText += paramCmdText + ");";
                comm.ExecuteNonQuery();
            }
        }
    }
}
 