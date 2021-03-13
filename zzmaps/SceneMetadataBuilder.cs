using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Text.Json;
using zzre;
using zzio.db;
using zzio.primitives;
using System.Text.RegularExpressions;

namespace zzmaps
{
    [Serializable]
    struct NPCMetadata
    {
        public string Name;
        public string? Icon;
        public Vector Pos;
    }

    [Serializable]
    struct SceneMetadata
    {
        public string Name;
        public uint ID;
        public int MinZoom, MaxZoom;
        public float BasePixelsPerUnit;
        public uint TilePixelSize;
        public Vector MinBounds, MaxBounds, Origin;
        public FColor BackgroundColor;
        public zzio.scn.Trigger[] Triggers;
        public NPCMetadata[] NPCs;
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
                    MinBounds = new Vector(mapBounds.Min.X, mapBounds.Min.Y, mapBounds.Min.Z),
                    MaxBounds = new Vector(mapBounds.Max.X, mapBounds.Max.Y, mapBounds.Max.Z),
                    Origin = scene.sceneOrigin,
                    BackgroundColor = background.AsColor(scene),
                    Triggers = scene.triggers,
                    NPCs = CreateNPCMetadata(scene)
                };

                var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions()
                {
                    IncludeFields = true
                });
                progressStep.Increment();
                return new BuiltSceneMetadata(loadedScene.SceneName, metadataJson);
            });

        private static readonly Regex SetModelRegex = new Regex(@"\nC\.(\w+)\s*\r?\n", RegexOptions.Compiled);
        private NPCMetadata[] CreateNPCMetadata(zzio.scn.Scene scene) => scene.triggers
            .Where(t => t.type == zzio.scn.TriggerType.NpcStartpoint)
            .Select(t => (t, npc: mappedDb.TryGetNpc(new UID(t.ii1), out var npc) ? npc : null))
            .Where(t => t.npc != null)
            .Select(t =>
            {
                var (trg, npc) = t;
                var setModelMatch = SetModelRegex.Match(npc!.Script2);
                return new NPCMetadata()
                {
                    Name = npc.Name,
                    Pos = trg.pos,
                    Icon = setModelMatch.Success ? setModelMatch.Groups[1].Value : null
                };
            }).ToArray();
    }
}
