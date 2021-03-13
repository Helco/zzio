using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio.primitives;
using zzio.rwbs;
using zzio.scn;
using zzio.utils;
using zzio.vfs;
using zzre;
using zzre.rendering;
using Quaternion = System.Numerics.Quaternion;

namespace zzmaps
{
    record TileSceneObject
    {
        public TileSceneObject(IResource resource, ClumpBuffers clumpBuffers)
        {
            Resource = resource;
            ClumpBuffers = clumpBuffers;
        }

        public IResource Resource { get; }
        public ClumpBuffers ClumpBuffers { get; }
        public Vector3 Position { get; init; }
        public Quaternion Rotation { get; init; }
        public FColor Tint { get; init; }
    }

    class TileScene : BaseDisposable
    {
        private static readonly FilePath[] TextureBasePaths = new[]
        {
            new FilePath("resources/textures/models"),
            new FilePath("resources/textures/worlds")
        };

        // these really should be something like IReleasableAssetLoader
        private readonly RefCachedAssetLoader<ClumpBuffers> clumpBufferLoader;
        private readonly RefCachedAssetLoader<Texture> textureLoader;

        private readonly HashSet<IResource> textures = new HashSet<IResource>();

        public Scene Scene { get; }
        public WorldBuffers WorldBuffers { get; }
        public IReadOnlyList<TileSceneObject> Objects { get; }
        public MapTiler MapTiler { get; }

        public TileScene(ITagContainer diContainer, IResource resource)
        {
            var resourcePool = diContainer.GetTag<IResourcePool>();
            this.clumpBufferLoader = diContainer.GetTag<RefCachedAssetLoader<ClumpBuffers>>();
            this.textureLoader = diContainer.GetTag<RefCachedAssetLoader<Texture>>();
            var clumpBufferLoader = this.clumpBufferLoader as IAssetLoader<ClumpBuffers>;
            var textureLoader = this.textureLoader as IAssetLoader<Texture>;

            using var contentStream = resource.OpenContent();
            if (contentStream == null)
                throw new IOException($"Could not open scene at {resource.Path.ToPOSIXString()}");
            Scene = new Scene();
            Scene.Read(contentStream);

            var fullPath = new FilePath("resources").Combine(Scene.misc.worldPath, Scene.misc.worldFile + ".bsp");
            WorldBuffers = new WorldBuffers(diContainer, fullPath);
            var objects = new List<TileSceneObject>(Scene.models.Length + Scene.foModels.Length);
            Objects = objects;

            foreach (var (filename, pos, rot, color) in
                Scene.models.Select(m => (m.filename, m.pos, m.rot, m.color)).Concat(
                    Scene.foModels.Select(m => (m.filename, m.pos, m.rot, m.color))))
            {
                var filePath = new FilePath("resources/models/models").Combine(filename + ".dff");
                var clumpResource = resourcePool.FindFile(filePath);
                if (clumpResource == null)
                    throw new FileNotFoundException($"Could not find clump file {filePath}");
                if (!clumpBufferLoader.TryLoad(clumpResource, out var clumpBuffers))
                    continue;
                objects.Add(new TileSceneObject(clumpResource, clumpBuffers)
                {
                    Position = pos.ToNumerics(),
                    Rotation = rot.ToNumericsRotation(),
                    Tint = color.ToFColor()
                });
                PreloadMaterial(clumpBuffers);
            }

            void PreloadMaterial(ClumpBuffers clumpBuffers)
            {
                var textureNames = clumpBuffers.SubMeshes
                    .Select(m => m.Material.FindChildById(zzio.rwbs.SectionId.Texture, false) as RWTexture)
                    .Select(t => t?.FindChildById(SectionId.String, false) as RWString)
                    .Where(s => s != null);
                foreach (var textureName in textureNames)
                {
                    var textureRes = TextureBasePaths
                        .SelectMany(basePath => new[] { ".dds", ".bmp" }.Select(
                            ext => basePath.Combine(textureName!.value + ext)))
                        .Select(basePath => resourcePool.FindFile(basePath))
                        .FirstOrDefault(res => res == null ? false : textureLoader.TryLoad(res, out var _));
                    if (textureRes != null)
                        textures.Add(textureRes);
                }
            }

            MapTiler = new MapTiler(WorldBuffers.Sections.First().Bounds, diContainer.GetTag<Options>());
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            WorldBuffers.Dispose();
            clumpBufferLoader.Release(Objects.Select(obj => obj.Resource));
            textureLoader.Release(textures);
        }
    }
}
