using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SQLitePCL.pretty;
using zzre;

namespace zzmaps
{
    class SQLiteOutput : ListDisposable, IOutput
    {
        private readonly SQLiteDatabaseConnection dbConnection;
        private readonly IStatement insertTileStmt, insertMetaStmt;
        private readonly string extension;
        private uint tilesWritten, commitEvery;

        static SQLiteOutput() => SQLitePCL.Batteries_V2.Init();

        public SQLiteOutput(Options options)
        {
            if (options.OutputDb == null)
                throw new ArgumentException("SQLite output is disabled by options");
            extension = options.OutputFormat.AsExtension();
            commitEvery = options.CommitEvery;

            dbConnection = SQLiteDatabaseConnectionBuilder
                .Create(options.OutputDb.FullName)
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
  PRIMARY KEY(scene, layer, zoom, x, z));

CREATE TABLE IF NOT EXISTS SceneMeta(
  scene TEXT,
  meta TEXT,
  PRIMARY KEY(scene))
");

            insertTileStmt = dbConnection.PrepareStatement(@"
INSERT OR REPLACE INTO Tiles VALUES (?, ?, ?, ?, ?, ?, ?)");
            insertMetaStmt = dbConnection.PrepareStatement(@"
INSERT OR REPLACE INTO SceneMeta VALUES (?, ?)");

            dbConnection.Execute("BEGIN");

            AddDisposable(insertTileStmt);
            AddDisposable(insertMetaStmt);
            AddDisposable(dbConnection);
        }

        protected override void DisposeManaged()
        {
            dbConnection.Execute("COMMIT");
            base.DisposeManaged();
        }

        public ITargetBlock<EncodedSceneTile> CreateTileTarget(ExecutionDataflowBlockOptions options, ProgressStep progressStep) =>
            new ActionBlock<EncodedSceneTile>(tile =>
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

                insertTileStmt.Reset();
                insertTileStmt.Bind(tile.SceneName,
                                tile.Layer,
                                tile.TileID.ZoomLevel,
                                tile.TileID.TileX,
                                tile.TileID.TileZ,
                                extension,
                                block);
                insertTileStmt.MoveNext();
                if (++tilesWritten >= commitEvery && commitEvery > 0)
                {
                    lock (dbConnection)
                    {
                        dbConnection.Execute("COMMIT");
                        dbConnection.Execute("BEGIN");
                    }
                    tilesWritten = 0;
                }
                progressStep.Increment();
            });

        public ITargetBlock<BuiltSceneMetadata> CreateMetaTarget(ExecutionDataflowBlockOptions options, ProgressStep progressStep) =>
            new ActionBlock<BuiltSceneMetadata>(meta =>
            {
                insertMetaStmt.Reset();
                insertMetaStmt.Bind(meta.SceneName, meta.Data);
                insertMetaStmt.MoveNext();
                progressStep.Increment();
            });
    }
}
