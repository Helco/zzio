using System.IO;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using zzio.vfs;

namespace zzmaps
{
    internal readonly struct ScenePattern
    {
        public ScenePattern(Regex pattern) => Pattern = pattern;

        public Regex Pattern { get; }
    }

    internal readonly struct SceneResource
    {
        public SceneResource(IResource resource) => Resource = resource;

        public IResource Resource { get; }
    }

    internal readonly struct LoadedScene
    {
        public LoadedScene(string sceneName, TileScene scene)
        {
            SceneName = sceneName;
            Scene = scene;
        }

        public string SceneName { get; }
        public TileScene Scene { get; }
    }

    internal readonly struct SceneTileId
    {
        public SceneTileId(string sceneName, int layer, TileID tileID)
        {
            SceneName = sceneName;
            Layer = layer;
            TileID = tileID;
        }

        public string SceneName { get; }
        public int Layer { get; }
        public TileID TileID { get; }
    }

    internal readonly struct RenderedSceneTile<TPixel> where TPixel : unmanaged, IPixel<TPixel>
    {
        public RenderedSceneTile(string sceneName, int layer, TileID tileID, Image<TPixel> image)
        {
            SceneName = sceneName;
            Layer = layer;
            TileID = tileID;
            Image = image;
        }

        public string SceneName { get; }
        public int Layer { get; }
        public TileID TileID { get; }
        public Image<TPixel> Image { get; }
    }

    internal readonly struct EncodedSceneTile
    {
        public EncodedSceneTile(string sceneName, int layer, TileID tileID, Stream stream)
        {
            SceneName = sceneName;
            Layer = layer;
            TileID = tileID;
            Stream = stream;
        }

        public string SceneName { get; }
        public int Layer { get; }
        public TileID TileID { get; }
        public Stream Stream { get; }
    }

    internal readonly struct BuiltSceneMetadata
    {
        public BuiltSceneMetadata(string sceneName, string data)
        {
            SceneName = sceneName;
            Data = data;
        }

        public string SceneName { get; }
        public string Data { get; }
    }
}
