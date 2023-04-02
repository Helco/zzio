using System;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Numerics;
using zzre;
using zzio.db;
using zzio;

namespace zzmaps;

[Serializable]
internal struct NPCMetadata
{
    public string Name;
    public string? Icon;
    public Vector3 Pos;
}

[Serializable]
internal struct SceneMetadata
{
    public string Name;
    public uint ID;
    public int MinZoom, MaxZoom;
    public float BasePixelsPerUnit;
    public uint TilePixelSize;
    public Vector3 MinBounds, MaxBounds, Origin;
    public FColor BackgroundColor;
    public zzio.scn.Trigger[] Triggers;
    public NPCMetadata[] NPCs;
}

internal class SceneMetadataBuilder
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
        new(loadedScene =>
        {
            var scene = loadedScene.Scene.Scene;
            var mapTiler = loadedScene.Scene.MapTiler;
            var mapBounds = mapTiler.TileUnitBoundsFor(mapTiler.Tiles.First());

            var metadata = new SceneMetadata()
            {
                ID = scene.dataset.sceneId,
                Name = scene.dataset.nameUID == UID.Invalid
                    ? "<none>"
                    : mappedDb.GetText(scene.dataset.nameUID).Text,
                MinZoom = mapTiler.MinZoomLevel,
                MaxZoom = mapTiler.MaxZoomLevel,
                BasePixelsPerUnit = mapTiler.BasePixelsPerUnit,
                TilePixelSize = mapTiler.TilePixelSize,
                MinBounds = mapBounds.Min,
                MaxBounds = mapBounds.Max,
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

    private static readonly Regex SetModelRegex = new(@"\nC\.(\w+)\s*\r?\n", RegexOptions.Compiled);
    private NPCMetadata[] CreateNPCMetadata(zzio.scn.Scene scene) => scene.triggers
        .Where(t => t.type == zzio.scn.TriggerType.NpcStartpoint)
        .Select(t => (t, npc: mappedDb.TryGetNpc(new UID(t.ii1), out var npc) ? npc : null))
        .Where(t => t.npc != null)
        .Select(t =>
        {
            var (trg, npc) = t;
            var setModelMatch = SetModelRegex.Match(npc!.InitScript);
            return new NPCMetadata()
            {
                Name = npc.Name,
                Pos = trg.pos,
                Icon = setModelMatch.Success ? setModelMatch.Groups[1].Value : null
            };
        }).ToArray();
}
