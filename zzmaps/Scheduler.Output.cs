using SQLitePCL.pretty;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace zzmaps
{
    partial class Scheduler
    {
        static Scheduler()
        {
            SQLitePCL.Batteries_V2.Init();
        }

        private ITargetBlock<EncodedSceneTile> CreateOutput()
        {
            if ((options.OutputDb == null) == (options.OutputDir == null))
                throw new InvalidOperationException("Exactly one output has to be set");
            else if (options.OutputDb != null)
                return CreateSQLiteOutput(options.OutputDb);
            else if (options.OutputDir != null)
                return CreateDirectoryOutput(options.OutputDir);

            throw new InvalidProgramException("Unexpected fall-through");
        }

        private ITargetBlock<EncodedSceneTile> CreateDirectoryOutput(DirectoryInfo outputDir)
        {
            outputDir.Create();
            return new ActionBlock<EncodedSceneTile>(async tile =>
            {
                var extension = ExtensionFor(options.OutputFormat);
                var tileName = $"{tile.Layer}-{tile.TileID.ZoomLevel}-{tile.TileID.TileX}.{tile.TileID.TileZ}{extension}";
                var tilePath = Path.Combine(outputDir.FullName, tile.SceneName);
                Directory.CreateDirectory(tilePath);

                using var targetStream = new FileStream(Path.Combine(tilePath, tileName), FileMode.Create, FileAccess.Write);
                await tile.Stream.CopyToAsync(targetStream);
                Interlocked.Increment(ref tilesOutput);
            });
        }

        private ITargetBlock<EncodedSceneTile> CreateSQLiteOutput(FileInfo info)
        {
            var dbConnection = this.dbConnection = SQLiteDatabaseConnectionBuilder
                .Create(info.FullName)
                .Build();
            dbConnection.ExecuteAll(@"
CREATE TABLE IF NOT EXISTS Tiles(
  scene TEXT,
  layer INTEGER,
  zoom INTEGER,
  x INTEGER,
  z INTEGER,
  encoding TEXT,
  tile BLOB,
  PRIMARY KEY(scene, layer, zoom, x, z))
");
            var insertStmt = this.insertStmt = dbConnection.PrepareStatement(@"
INSERT OR REPLACE INTO Tiles VALUES (?, ?, ?, ?, ?, ?, ?)");
            dbConnection.Execute("BEGIN");
            hasTransactionOpen = true;

            uint tilesWritten = 0;
            return new ActionBlock<EncodedSceneTile>(tile =>
            {
                byte[] block;
                if (tile.Stream is MemoryStream)
                    block = ((MemoryStream)tile.Stream).ToArray();
                else
                {
                    var memory = new MemoryStream((int)tile.Stream.Length);
                    tile.Stream.CopyTo(memory);
                    block = memory.ToArray();
                }

                insertStmt.Reset();
                insertStmt.Bind(tile.SceneName,
                                tile.Layer,
                                tile.TileID.ZoomLevel,
                                tile.TileID.TileX,
                                tile.TileID.TileZ,
                                ExtensionFor(options.OutputFormat),
                                block);
                insertStmt.MoveNext();
                if (++tilesWritten >= options.CommitEvery && options.CommitEvery > 0)
                {
                    dbConnection.Execute("COMMIT");
                    dbConnection.Execute("BEGIN");
                    tilesWritten = 0;
                }
                Interlocked.Increment(ref tilesOutput);
            });
        }
    }
}
