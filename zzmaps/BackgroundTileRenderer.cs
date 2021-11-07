using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Tasks.Dataflow;

namespace zzmaps
{
    internal class BackgroundTileRenderer
    {
        private readonly int imageSize;
        private readonly ZZMapsBackground background;

        public BackgroundTileRenderer(Options options)
        {
            imageSize = (int)options.TileSize;
            background = options.Background;
        }

        public IPropagatorBlock<LoadedScene, RenderedSceneTile<Rgba32>> CreateTransform(ExecutionDataflowBlockOptions options, ProgressStep progressStep) =>
            new TransformBlock<LoadedScene, RenderedSceneTile<Rgba32>>(loadedScene =>
            {
                var zzcolor = background.AsColor(loadedScene.Scene.Scene);
                var image = new Image<Rgba32>(imageSize, imageSize, new Rgba32(zzcolor.r, zzcolor.g, zzcolor.b, zzcolor.a));
                progressStep.Increment();
                return new RenderedSceneTile<Rgba32>(
                    loadedScene.SceneName,
                    layer: -1, new TileID(0, 0, 0),
                    image);
            });
    }
}
