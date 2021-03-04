using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using zzre;
using zzio.vfs;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using SixLabors.ImageSharp;
using Veldrid;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Diagnostics;
using System.Threading;
using System.Runtime;

namespace zzmaps
{
    class Scheduler : BaseDisposable
    {
        public long ScenesFound => scenesFound;
        public long ScenesLoaded => scenesLoaded;
        public long TilesRendered => tilesRendered;
        public long EmptyTiles => emptyTiles;
        public long TilesEncoded => tilesEncoded;
        public long TilesOutput => tilesOutput;

        private readonly ITagContainer diContainer;
        private readonly Options options;
        private readonly IResourcePool resourcePool;
        private readonly GraphicsDevice graphicsDevice;
        private readonly ConcurrentQueue<MapTileRenderer> rendererQueue = new ConcurrentQueue<MapTileRenderer>();
        private long scenesFound = 0, scenesLoaded = 0, tilesRendered = 0, emptyTiles = 0, tilesEncoded = 0, tilesOutput = 0;

        public Scheduler(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            options = diContainer.GetTag<Options>();
            resourcePool = diContainer.GetTag<IResourcePool>();
            graphicsDevice = diContainer.GetTag<GraphicsDevice>();

            for (int i = 0; i < options.Renderers; i++)
                rendererQueue.Enqueue(new MapTileRenderer(diContainer));
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            while (!rendererQueue.IsEmpty)
            {
                if (rendererQueue.TryDequeue(out var renderer))
                    renderer.Dispose();
            }
        }

        public async Task Run()
        {
            var dataflowOptions = new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = (int)options.Renderers,
                SingleProducerConstrained = false,
                BoundedCapacity = (int)options.Renderers
            };
            var sceneSelector = CreateSceneSelector();
            var sceneLoader = CreateSceneLoader(dataflowOptions);
            var tileRenderer = CreateTileRenderer(dataflowOptions);
            var encoder = CreateEncoder<Rgba32>(dataflowOptions);
            var output = CreateOutput();

            var linkOptions = new DataflowLinkOptions()
            {
                PropagateCompletion = true
            };
            sceneSelector.LinkTo(sceneLoader, linkOptions);
            sceneLoader.LinkTo(tileRenderer, linkOptions);
            tileRenderer.LinkTo(encoder, linkOptions);
            encoder.LinkTo(output, linkOptions);

            ThreadPool.GetMinThreads(out var prevWorkerThreads, out var prevCompletionThreads);
            ThreadPool.SetMinThreads((int)options.Renderers, prevCompletionThreads);
            var prevGCLatency = GCSettings.LatencyMode;
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            try
            {
                sceneSelector.Post(new ScenePattern(options.ScenePattern));
                sceneSelector.Complete();
                await output.Completion;
            }
            finally
            {
                GCSettings.LatencyMode = prevGCLatency;
                ThreadPool.SetMinThreads(prevWorkerThreads, prevCompletionThreads);
            }
        }

        private IPropagatorBlock<ScenePattern, SceneResource> CreateSceneSelector() =>
            new TransformManyBlock<ScenePattern, SceneResource>(FindScenes);

        private IEnumerable<SceneResource> FindScenes(ScenePattern pattern)
        {
            var folderQueue = new Queue<IResource>(resourcePool.Root);
            while (folderQueue.Any())
            {
                var folder = folderQueue.Dequeue();
                foreach (var child in folder)
                {
                    if (child.Type == ResourceType.Directory)
                        folderQueue.Enqueue(child);
                    else if (child.Name.EndsWith(".scn") && pattern.Pattern.IsMatch(child.Name))
                    {
                        Interlocked.Increment(ref scenesFound);
                        yield return new SceneResource(child);
                    }
                }
            }
        }

        private IPropagatorBlock<SceneResource, LoadedScene> CreateSceneLoader(ExecutionDataflowBlockOptions dataflowOptions) =>
            new TransformBlock<SceneResource, LoadedScene>(r =>
            {
                var scene = new LoadedScene(r.Resource.Name.Replace(".scn", ""), new TileScene(diContainer, r.Resource));
                Interlocked.Increment(ref scenesLoaded);
                return scene;
            }, dataflowOptions);

        private IPropagatorBlock<LoadedScene, RenderedSceneTile<Rgba32>> CreateTileRenderer(ExecutionDataflowBlockOptions dataflowOptions) =>
            new TransformManyBlock<LoadedScene, RenderedSceneTile<Rgba32>>(async scene =>
            {
                MapTileRenderer? renderer;
                while (!rendererQueue.TryDequeue(out renderer))
                    await Task.Yield();

                renderer.Scene = scene.Scene;
                var tiles = renderer.RenderTiles()
                    .Select(t =>
                    {
                        if (t.Item3 == 0)
                        {
                            Interlocked.Increment(ref emptyTiles);
                            return new RenderedSceneTile<Rgba32>(null!, 0, default, null!);
                        }
                        ReadOnlySpan<byte> tileSpan;
                        var map = graphicsDevice.Map(t.Item1, MapMode.Read);
                        unsafe { tileSpan = new ReadOnlySpan<byte>(map.Data.ToPointer(), (int)(options.TileSize * options.TileSize * 4)); }
                        var image = Image.LoadPixelData<Rgba32>(tileSpan, (int)options.TileSize, (int)options.TileSize);
                        graphicsDevice.Unmap(map.Resource);
                        Interlocked.Increment(ref tilesRendered);
                        return new RenderedSceneTile<Rgba32>(scene.SceneName, 0, t.Item2, image);
                    }).Where(tile => tile.Image != null).ToArray();

                rendererQueue.Enqueue(renderer);
                return tiles;
            }, dataflowOptions);

        #region Output
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
        #endregion

        #region Encoder
        private IPropagatorBlock<RenderedSceneTile<TPixel>, EncodedSceneTile> CreateEncoder<TPixel>(ExecutionDataflowBlockOptions? dataflowOptions = null)
           where TPixel : unmanaged, IPixel<TPixel>
        {
            dataflowOptions ??= new ExecutionDataflowBlockOptions();
            if (options.Optimizer == null)
                return CreateImageEncoding<TPixel>(options.OutputFormat, dataflowOptions, true);

            var intermediate = CreateImageEncoding<TPixel>(options.TempFormat, dataflowOptions, false);
            var optimizer = CreateOptimizer(dataflowOptions);
            intermediate.LinkTo(optimizer);
            return intermediate;
        }

        private IPropagatorBlock<RenderedSceneTile<TPixel>, EncodedSceneTile> CreateImageEncoding<TPixel>(ZZMapsImageFormat format, ExecutionDataflowBlockOptions dataflowOptions, bool finalEncoding)
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
                if (finalEncoding)
                    Interlocked.Increment(ref tilesEncoded);
                return new EncodedSceneTile(renderedTile.SceneName, renderedTile.Layer, renderedTile.TileID, stream);
            }, dataflowOptions);
        }

        private static string ExtensionFor(ZZMapsImageFormat format) => format switch
        {
            ZZMapsImageFormat.Jpeg => ".jpeg",
            ZZMapsImageFormat.Png => ".png",
            _ => throw new NotSupportedException($"Unsupported image formt {format}")
        };

        private IPropagatorBlock<EncodedSceneTile, EncodedSceneTile> CreateOptimizer(ExecutionDataflowBlockOptions dataflowOptions)
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
                var inputPath = Path.Combine(options.TempFolder.FullName, $"{tempName}-in{ExtensionFor(options.TempFormat)}");
                var outputPath = Path.Combine(options.TempFolder.FullName, $"{tempName}-out{ExtensionFor(options.OutputFormat)}");

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

                Interlocked.Increment(ref tilesEncoded);
                return new EncodedSceneTile(
                    tile.SceneName, tile.Layer, tile.TileID,
                    new FileStream(outputPath, FileMode.Open, FileAccess.Read));
            });
        }
    #endregion
    }
}
