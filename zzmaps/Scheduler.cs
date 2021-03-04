using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Threading;
using System.Runtime;
using zzre;
using zzio.vfs;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using Veldrid;
using SQLitePCL.pretty;

namespace zzmaps
{
    partial class Scheduler : ListDisposable
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

        private SQLiteDatabaseConnection? dbConnection;
        private IStatement? insertStmt;
        private bool hasTransactionOpen;

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

            insertStmt?.Dispose();
            if (dbConnection != null)
            {
                if (hasTransactionOpen)
                    dbConnection.Execute("COMMIT");
                dbConnection.Dispose();
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
    }
}
