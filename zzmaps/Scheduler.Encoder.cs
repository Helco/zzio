using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace zzmaps
{
    partial class Scheduler
    {
        private IPropagatorBlock<RenderedSceneTile<TPixel>, EncodedSceneTile> CreateEncoder<TPixel>(ExecutionDataflowBlockOptions? dataflowOptions = null)
           where TPixel : unmanaged, IPixel<TPixel>
        {
            dataflowOptions ??= new ExecutionDataflowBlockOptions();
            if (options.Optimizer == null)
                return CreateImageEncoding<TPixel>(options.OutputFormat, dataflowOptions, stepTilesEncoded);

            var intermediate = CreateImageEncoding<TPixel>(options.TempFormat, dataflowOptions, stepTilesEncoded);
            var optimizer = CreateOptimizer(dataflowOptions, stepTilesOptimized);
            intermediate.LinkTo(optimizer);
            return intermediate;
        }

        private IPropagatorBlock<RenderedSceneTile<TPixel>, EncodedSceneTile> CreateImageEncoding<TPixel>(ZZMapsImageFormat format, ExecutionDataflowBlockOptions dataflowOptions, ProgressStep? progressStep)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            IImageEncoder encoder = format switch
            {
                ZZMapsImageFormat.Png => new PngEncoder()
                {
                    CompressionLevel = (PngCompressionLevel)Math.Clamp(options.PNGCompression,
                        (int)PngCompressionLevel.BestSpeed, (int)PngCompressionLevel.BestCompression)
                },
                ZZMapsImageFormat.Jpeg => new JpegEncoder()
                {
                    Quality = Math.Clamp(options.JPEGQuality, 0, 100)
                },
                _ => throw new NotSupportedException($"Unsupported image format {format}")
            };

            return new TransformBlock<RenderedSceneTile<TPixel>, EncodedSceneTile>(renderedTile =>
            {
                var stream = new MemoryStream();
                encoder.Encode(renderedTile.Image, stream);
                stream.Position = 0;
                progressStep?.Increment();
                return new EncodedSceneTile(renderedTile.SceneName, renderedTile.Layer, renderedTile.TileID, stream);
            }, dataflowOptions);
        }

        private IPropagatorBlock<EncodedSceneTile, EncodedSceneTile> CreateOptimizer(ExecutionDataflowBlockOptions dataflowOptions, ProgressStep progressStep)
        {
            if (options.Optimizer == null)
                throw new InvalidOperationException("No optimizer was given");
            var splitIndex = options.Optimizer.StartsWith('\"') && options.Optimizer.Length > 1
                ? options.Optimizer.IndexOf('\"', 1) + 1
                : options.Optimizer.IndexOf(' ');
            if (splitIndex < 1)
                throw new InvalidOperationException("Invalid optimizer string");
            var optimizerFile = options.Optimizer.Substring(0, splitIndex);
            var optimizerArgs = options.Optimizer.Substring(splitIndex);

            options.TempFolder.Create();
            return new TransformBlock<EncodedSceneTile, EncodedSceneTile>(async tile =>
            {
                var tempName = $"{tile.SceneName}-{tile.Layer}-{tile.TileID.ZoomLevel}-{tile.TileID.TileX}.{tile.TileID.TileZ}";
                var inputPath = Path.Combine(options.TempFolder.FullName, $"{tempName}-in{options.TempFormat.AsExtension()}");
                var outputPath = Path.Combine(options.TempFolder.FullName, $"{tempName}-out{options.OutputFormat.AsExtension()}");

                using (var inputStream = new FileStream(inputPath, FileMode.Create, FileAccess.Write))
                    await tile.Stream.CopyToAsync(inputStream);
                var process = Process.Start(new ProcessStartInfo()
                {
                    CreateNoWindow = true,
                    ErrorDialog = false,
                    FileName = optimizerFile,
                    Arguments = optimizerArgs
                    .Replace("$input", '\"' + inputPath + '\"')
                    .Replace("$output", '\"' + outputPath + '\"')
                });
                if (process == null)
                    throw new Exception($"No image optimizer process was started");
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                    throw new Exception($"Image optimizer failed with {process.ExitCode}");

                progressStep.Increment();
                return new EncodedSceneTile(
                    tile.SceneName, tile.Layer, tile.TileID,
                    new FileStream(outputPath, FileMode.Open, FileAccess.Read));
            });
        }
    }
}
