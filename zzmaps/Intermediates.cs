using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using zzio.vfs;

namespace zzmaps
{
    readonly struct ScenePattern
    {
        public ScenePattern(Regex pattern) => Pattern = pattern;

        public Regex Pattern { get; }
    }

    readonly struct SceneResource
    {
        public SceneResource(IResource resource) => Resource = resource;

        public IResource Resource { get; }
    }

    readonly struct LoadedScene
    {
        public LoadedScene(string sceneName, TileScene scene)
        {
            SceneName = sceneName;
            Scene = scene;
        }

        public string SceneName { get; }
        public TileScene Scene { get; }
    }

    readonly struct SceneTileId
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

    readonly struct RenderedSceneTile<TPixel> where TPixel : unmanaged, IPixel<TPixel>
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

    readonly struct EncodedSceneTile
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
}
