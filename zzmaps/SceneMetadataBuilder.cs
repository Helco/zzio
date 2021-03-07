using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Numerics;
using System.Text.Json;
using zzre;
using zzio.db;
using zzio.primitives;

namespace zzmaps
{
    [Serializable]
    struct SceneMetadata
    {
        public string Name;
        public uint ID;
        public int MinZoom, MaxZoom;
        public float BasePixelsPerUnit;
        public uint TilePixelSize;
        public Vector2 MinBounds, MaxBounds;
        public FColor BackgroundColor;
        public zzio.scn.Trigger[] Triggers;
    }

    class SceneMetadataBuilder
    {
        private readonly MappedDB mappedDb;
        private readonly ZZMapsBackground background;

        public SceneMetadataBuilder(ITagContainer diContainer)
        {
            var options = diContainer.GetTag<Options>();
            mappedDb = diContainer.GetTag<MappedDB>();
            background = options.Background;
        }

        public TransformBlock<LoadedScene, BuiltSceneMetadata> CreateTransform(ExecutionDataflowBlockOptions options, ProgressStep progressStep) =>
            new TransformBlock<LoadedScene, BuiltSceneMetadata>(loadedScene =>
            {
                var scene = loadedScene.Scene.Scene;
                var mapTiler = loadedScene.Scene.MapTiler;
                var mapBounds = mapTiler.TileUnitBoundsFor(mapTiler.Tiles.First());

                var metadata = new SceneMetadata()
                {
                    ID = scene.dataset.sceneId,
                    Name = scene.dataset.nameUID == 0xFFFFFFFF
                        ? "<none>"
                        : mappedDb.GetText(new UID(scene.dataset.nameUID)).Text,
                    MinZoom = mapTiler.MinZoomLevel,
                    MaxZoom = mapTiler.MaxZoomLevel,
                    BasePixelsPerUnit = mapTiler.BasePixelsPerUnit,
                    TilePixelSize = mapTiler.TilePixelSize,
                    MinBounds = new Vector2(mapBounds.Min.X, mapBounds.Min.Z),
                    MaxBounds = new Vector2(mapBounds.Max.X, mapBounds.Max.Z),
                    BackgroundColor = background.AsColor(scene),
                    Triggers = scene.triggers
                };

                var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions()
                {
                    IncludeFields = true
                });
                progressStep.Increment();
                return new BuiltSceneMetadata(loadedScene.SceneName, metadataJson);
            });
    }
}
