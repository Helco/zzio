using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace zzmaps
{
    partial class Scheduler
    {
        private ITargetBlock<EncodedSceneTile> CreateOutput()
        {
            if ((options.OutputDb == null) == (options.OutputDir == null))
                throw new InvalidOperationException("Exactly one output has to be set");
            else if (options.OutputDb != null)
                throw new NotImplementedException("Output database is not yet implemented");
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
    }
}
