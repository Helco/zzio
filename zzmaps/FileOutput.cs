﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace zzmaps
{
    class FileOutput : IOutput
    {
        private readonly DirectoryInfo outputDir;
        private readonly string outputPath, extension;

        public FileOutput(Options options)
        {
            if (options.OutputDir == null)
                throw new ArgumentException("File output is disabled by options");
            outputDir = options.OutputDir;

            outputDir.Create();
            outputPath = outputDir.FullName;
            extension = options.OutputFormat.AsExtension();
        }

        public ITargetBlock<EncodedSceneTile> CreateTileTarget(ExecutionDataflowBlockOptions options, ProgressStep progressStep) =>
            new ActionBlock<EncodedSceneTile>(async tile =>
            {
                var tileName = $"{tile.Layer}-{tile.TileID.ZoomLevel}-{tile.TileID.TileX}.{tile.TileID.TileZ}{extension}";
                var tilePath = Path.Combine(outputPath, tile.SceneName);
                Directory.CreateDirectory(tilePath);

                using var targetStream = new FileStream(Path.Combine(tilePath, tileName), FileMode.Create, FileAccess.Write);
                await tile.Stream.CopyToAsync(targetStream);
                progressStep.Increment();
            });
    }
}
