using System;
using System.IO;
using System.Collections.Generic;

namespace zzio.db;

[Serializable]
public class Table
{
    public Dictionary<UID, Row> rows = [];

    /// <summary>Number of rows skipped during <see cref="Read"/> because their UID appeared more than once</summary>
    public int DuplicateRowCount { get; private set; }

    public void Read(Stream stream)
    {
        rows.Clear();
        DuplicateRowCount = 0;
        using BinaryReader reader = new(stream);
        uint rowCount = reader.ReadUInt32();
        for (uint i = 0; i < rowCount; i++)
        {
            Row row = new();
            row.Read(reader);
            // Some databases (notably large mods like the Global Mod) contain
            // duplicate UIDs. The original game tolerates them, so keep the first
            // occurrence instead of throwing, and report how many were skipped.
            if (!rows.TryAdd(row.uid, row))
                DuplicateRowCount++;
        }
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(rows.Count);
        foreach (Row row in rows.Values)
            row.Write(writer);
    }
}
