using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using CommandHandler = System.Action<System.Data.SQLite.SQLiteConnection, string[]>;
using System.IO;
using zzio.db;

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
            { "exportraw", exportRaw }
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
            var createTableCommand = new SQLiteCommand(
                $"CREATE TABLE IF NOT EXISTS {tableName} (" +
                "UID TEXT, " +
                string.Join(",", table.rows.Values.First().cells.Select(
                    (c, i) => $"col_{i}_{c.Type} {CellDataTypeToSQLType[c.Type]}")) +
                ", PRIMARY KEY (UID))", dbConnection);
            createTableCommand.ExecuteNonQuery();

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
}
