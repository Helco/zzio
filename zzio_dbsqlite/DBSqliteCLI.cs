using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using CommandHandler = System.Action<System.Data.SQLite.SQLiteConnection, string[]>;
using System.IO;
using zzio.db;
using System.Data.Common;
using System.Data;
using zzio.script;

namespace zzio.dbsqlitecli;

public static class DBSqliteCLI
{
    /*
     * importraw zz.db Data/_fb0x01.fbs Data/_fb0x02.fbs Data/_fb0x03.fbs Data/_fb0x04.fbs Data/_fb0x05.fbs Data/_fb0x06.fbs
     * exportraw zz.db _fb0x01 _fb0x02 _fb0x03 _fb0x04 _fb0x05 _fb0x06
     */
    public static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("nope");
            return;
        }

        string command = args[0].ToLowerInvariant();
        string db = args[1];

        SQLiteConnection dbConnection = new("URI=file:" + db);
        dbConnection.Open();

        IReadOnlyDictionary<string, CommandHandler> commands = new Dictionary<string, CommandHandler>()
        {
            { "importraw", importRaw },
            { "exportraw", exportRaw },
            { "importpretty", importPretty }
        };
        if (!commands.ContainsKey(command))
        {
            Console.WriteLine("What is " + command + "?");
            return;
        }
        commands[command](dbConnection, args.Skip(2).ToArray());
        dbConnection.Close();
    }

    private static readonly IReadOnlyDictionary<CellDataType, string> CellDataTypeToSQLType = new Dictionary<CellDataType, string>()
    {
        { CellDataType.Unknown, "BLOB" },
        { CellDataType.String, "STRING" },
        { CellDataType.Integer, "INTEGER" },
        { CellDataType.ForeignKey, "TEXT" },
        { CellDataType.Byte, "INTEGER" },
        { CellDataType.Buffer, "BLOB" }
    };

    private static void importRaw(SQLiteConnection dbConnection, string[] tableFiles)
    {
        foreach (string tableFile in tableFiles)
        {
            var table = new zzio.db.Table();
            using (var stream = new FileStream(tableFile, FileMode.Open, FileAccess.Read))
                table.Read(stream);

            var tableName = Path.GetFileNameWithoutExtension(tableFile);
            Console.WriteLine("Importing raw table " + tableName);
            var createTableCommand = new SQLiteCommand(
                $"CREATE TABLE IF NOT EXISTS {tableName} (" +
                "UID TEXT, " +
                string.Join(",", table.rows.Values.First().cells.Select(
                    (c, i) => $"col_{i}_{c.Type} {CellDataTypeToSQLType[c.Type]}")) +
                ", PRIMARY KEY (UID))", dbConnection);
            createTableCommand.ExecuteNonQuery();

            execute(dbConnection, "BEGIN TRANSACTION");
            int columnCount = table.rows.Values.First().cells.Length;
            foreach (var row in table.rows.Values)
            {
                var insertRowCommand = new SQLiteCommand(
                $"REPLACE INTO {tableName} VALUES (@uid, " +
                string.Join(",", table.rows.Values.First().cells.Select((c, i) => $"@col_{i}")) +
                ")", dbConnection);
                insertRowCommand.Prepare();
                insertRowCommand.Parameters.Clear();
                insertRowCommand.Parameters.AddWithValue("@uid", row.uid.ToString());
                for (int i = 0; i < row.cells.Length; i++)
                {
                    var cell = row.cells[i];
                    switch (cell.Type)
                    {
                        case CellDataType.Buffer:
                        case CellDataType.Unknown:
                            insertRowCommand.Parameters.AddWithValue($"@col_{i}", cell.Buffer);
                            break;
                        case CellDataType.Byte:
                            insertRowCommand.Parameters.AddWithValue($"@col_{i}", cell.Byte);
                            break;
                        case CellDataType.Integer:
                            insertRowCommand.Parameters.AddWithValue($"@col_{i}", cell.Integer);
                            break;
                        case CellDataType.String:
                            insertRowCommand.Parameters.AddWithValue($"@col_{i}", cell.String);
                            break;
                        case CellDataType.ForeignKey:
                            insertRowCommand.Parameters.AddWithValue($"@col_{i}", cell.ForeignKey.ToString());
                            break;
                        default: throw new Exception("WUT?");
                    }
                }
                for (int i = row.cells.Length; i < columnCount; i++)
                    insertRowCommand.Parameters.AddWithValue($"@col_{i}", null);
                insertRowCommand.ExecuteNonQuery();
            }
            execute(dbConnection, "COMMIT TRANSACTION");
        }
    }

    private static void exportRaw(SQLiteConnection dbConnection, string[] tables)
    {
        foreach (string tableName in tables)
        {
            var table = new Table();

            var selectCommand = new SQLiteCommand("SELECT * FROM " + tableName, dbConnection);
            var reader = selectCommand.ExecuteReader();
            if (!reader.HasRows)
            {
                Console.WriteLine("WARNING: No rows for " + tableName);
                continue;
            }

            while (reader.Read())
            {
                var row = new Row
                {
                    uid = UID.Parse(reader.GetFieldValue<string>(0)),
                    cells = new Cell[reader.FieldCount - 1]
                };
                for (int i = 1; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var type = Enum.Parse<CellDataType>(columnName[(1 + columnName.LastIndexOf('_'))..]);
                    switch (type)
                    {
                        case CellDataType.Buffer:
                        case CellDataType.Unknown:
                            throw new Exception("Not supported yet");
                        case CellDataType.Byte:
                            row.cells[i - 1] = new Cell(reader.GetByte(i));
                            break;
                        case CellDataType.Integer:
                            if (reader.IsDBNull(i))
                                row.cells[i - 1] = new Cell(0);
                            else
                                row.cells[i - 1] = new Cell(reader.GetInt32(i));
                            break;
                        case CellDataType.String:
                            row.cells[i - 1] = new Cell(reader.GetFieldValue<string>(i));
                            break;
                        case CellDataType.ForeignKey:
                            var parts = reader.GetString(i).Split("|").Select(UID.Parse).ToArray();
                            row.cells[i - 1] = new Cell(new ForeignKey(parts[0], parts[1]));
                            break;
                        default:
                            throw new Exception("WUT?");
                    }
                }
                table.rows.Add(row.uid, row);
            }

            using (var stream = new FileStream(tableName + ".fbs", FileMode.Create, FileAccess.Write))
                table.Write(stream);
        }
    }

    private static void importPretty(SQLiteConnection sqliteDb, string[] tableFiles)
    {
        importRaw(sqliteDb, tableFiles);

        var mappedDb = new MappedDB();
        foreach (var tableFile in tableFiles)
        {
            try
            {
                var table = new Table();
                using (var stream = new FileStream(tableFile, FileMode.Open, FileAccess.Read))
                    table.Read(stream);
                mappedDb.AddTable(table);
            }
            catch(Exception e)
            {
                Console.WriteLine($"Ignoring {tableFile} for pretty tables: {e.Message}");
            }
        }

        decompileAllScripts(sqliteDb, mappedDb);

        execute(sqliteDb, @"DROP VIEW IF EXISTS NPCs");
        execute(sqliteDb, @"
CREATE VIEW NPCs AS SELECT
    _fb0x05.UID AS UID,
    _fb0x02.col_0_String AS Name,
    scr1.script AS OnTrigger,
    scr2.script AS OnInit,
    scr3.script AS OnUpdate,
    scr4.script AS OnDefeated,
    scr5.script AS OnVictorious,
    _fb0x05.col_6_String AS InternalName
FROM _fb0x05
LEFT JOIN _fb0x02 ON _fb0x02.UID = substr(_fb0x05.col_0_ForeignKey, 1, 8)
LEFT JOIN scripts AS scr1 ON scr1.uid = _fb0x05.UID AND scr1.column = 1
LEFT JOIN scripts AS scr2 ON scr2.uid = _fb0x05.UID AND scr2.column = 2
LEFT JOIN scripts AS scr3 ON scr3.uid = _fb0x05.UID AND scr3.column = 3
LEFT JOIN scripts AS scr4 ON scr4.uid = _fb0x05.UID AND scr4.column = 4
LEFT JOIN scripts AS scr5 ON scr5.uid = _fb0x05.UID AND scr5.column = 5
");
    }

    private static void execute(SQLiteConnection sqliteDb, string commandText)
    {
        using var command = new SQLiteCommand(commandText, sqliteDb);
        command.ExecuteNonQuery();
    }

    private static void decompileAllScripts(SQLiteConnection sqliteDb, MappedDB mappedDb)
    {
        Console.WriteLine("Decompiling scripts");

        execute(sqliteDb, @"
CREATE TABLE IF NOT EXISTS scripts (
    uid TEXT,
    column INTEGER,
    script TEXT,
    PRIMARY KEY (uid, column))");

        using var insertRowCommand = new SQLiteCommand(
            "REPLACE INTO scripts VALUES (@uid, @column, @script)",
            sqliteDb);
        insertRowCommand.Prepare();

        execute(sqliteDb, "BEGIN TRANSACTION");
        foreach (var item in mappedDb.Items)
            decompileScript(insertRowCommand, item.Uid, 4, item.Script);
        foreach (var npc in mappedDb.Npcs)
        {
            decompileScript(insertRowCommand, npc.Uid, 1, npc.TriggerScript);
            decompileScript(insertRowCommand, npc.Uid, 2, npc.InitScript);
            decompileScript(insertRowCommand, npc.Uid, 3, npc.UpdateScript);
            decompileScript(insertRowCommand, npc.Uid, 4, npc.DefeatedScript);
            decompileScript(insertRowCommand, npc.Uid, 5, npc.VictoriousScript);
        }
        execute(sqliteDb, "COMMIT TRANSACTION");
    }

    private static void decompileScript(SQLiteCommand command, UID uid, int column, string scriptCompiled)
    {
        string scriptDecompiled = "";
        if (!string.IsNullOrWhiteSpace(scriptCompiled))
        {
            try
            {
                var instructions = scriptCompiled
                    .Split('\n')
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => new RawInstruction(l))
                    .ToArray();
                using var stringWriter = new StringWriter();
                zzsc.CLI.decompile(stringWriter, instructions, null);
                scriptDecompiled = stringWriter.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error decompiling script {uid}@{column}: {e.Message}");
                return;
            }
        }

        command.Reset();
        command.Parameters.Clear();
        command.Parameters.AddWithValue("@uid", uid.ToString());
        command.Parameters.AddWithValue("@column", column);
        command.Parameters.AddWithValue("@script", scriptDecompiled);
        command.ExecuteNonQuery();
    }
}
