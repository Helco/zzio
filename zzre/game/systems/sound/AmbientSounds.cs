using System;
using System.Collections.Generic;
using DefaultEcs.System;
using Serilog;
using zzio.scn;

namespace zzre.game.systems;

internal sealed class AmbientSounds : ISystem<float>
{
    private const float AmbientFadeLength = 0.25f;
    private const float LandscapeFadeLength = 2f;
    private const float LandscapeFadeDelay = 1.5f;
    private const float LandscapeUpdateTime = 0.1f;
    private const float LandscapeRadius = 10f;
    private const int TargetLandscapeSounds = 2;
    private const float QuietVolume = 0.4f;
    private const float NormalVolume = 1f;
    private const string MusicBasePath = "resources/audio/music/_m";
    private const string AmbientBasePath = "resources/audio/sfx/ambients/_a";
    private const string LandscapeBasePath = "resources/audio/sfx/landscapes/_l";
    private const string MusicExtension = ".mp3";
    private const string AmbientExtension = ".wav";
    private const string LandscapeExtension = ".wav";

    private readonly Random Random = Random.Shared;
    private readonly ILogger logger;
    private readonly GameTime time;
    private readonly UI ui;
    private readonly DefaultEcs.World gameWorld;
    private readonly IDisposable? playerLeavingSubscription;
    private readonly IDisposable? playerEnterSubscription;
    private readonly IDisposable? sceneLoadedSubscription;
    private readonly IDisposable? setAmbientSubscription;
    private readonly List<DefaultEcs.Entity> landscapeEntities = [];

    private float lastUpdateTime = -1f;
    private IReadOnlyList<int> landscapeSamples = Array.Empty<int>();
    private int landscapeChance;
    private DefaultEcs.Entity musicEntity, ambientEntity;
    private StdSceneInfo.AmbientMode? lastMusic, lastAmbient;

    public bool IsEnabled { get; set; }

    public AmbientSounds(ITagContainer diContainer)
    {
        logger = diContainer.GetLoggerFor<AmbientSounds>();
        time = diContainer.GetTag<GameTime>();
        ui = diContainer.GetTag<UI>();
        gameWorld = diContainer.GetTag<DefaultEcs.World>();
        if (!(IsEnabled = ui.HasTag<SoundContext>()))
            return;
        playerLeavingSubscription = gameWorld.Subscribe<messages.PlayerLeaving>(HandleLeaving);
        playerEnterSubscription = gameWorld.Subscribe<messages.PlayerEntered>(HandleEntering);
        sceneLoadedSubscription = gameWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        setAmbientSubscription = gameWorld.Subscribe<messages.SetAmbient>(HandleSetAmbient);
    }

    public void Dispose()
    {
        playerLeavingSubscription?.Dispose();
        playerEnterSubscription?.Dispose();
        sceneLoadedSubscription?.Dispose();
        setAmbientSubscription?.Dispose();
    }

    private void HandleLeaving(in messages.PlayerLeaving msg)
    {
        FadeOutPermanent(musicEntity, StdSceneInfo.GetMusicMode(msg.NextScene));
        FadeOutPermanent(ambientEntity, StdSceneInfo.GetAmbientMode(msg.NextScene));
    }

    private void HandleEntering(in messages.PlayerEntered msg)
    {
        var sceneName = $"sc_{gameWorld.Get<Scene>().dataset.sceneId:D4}";
        HandleSetAmbient(new(sceneName));
    }

    private void HandleSetAmbient(in messages.SetAmbient msg)
    {
        var musicMode = StdSceneInfo.GetMusicMode(msg.SceneName);
        var ambientMode = StdSceneInfo.GetAmbientMode(msg.SceneName);
        SetNewPermanent(isMusic: true, ref musicEntity, ref lastMusic, musicMode);
        SetNewPermanent(isMusic: false, ref ambientEntity, ref lastAmbient, ambientMode);
        logger.Debug("Set to music {Music}, ambient {Ambient} with {Landscape} landscape samples", musicMode, ambientMode, landscapeEntities.Count);
    }

    private static void FadeOutPermanent(in DefaultEcs.Entity entity, StdSceneInfo.AmbientMode? nextMode)
    {
        if (entity.TryGet<components.SoundEmitter>(out var emitter) && nextMode is null)
            entity.Set(components.SoundFade.Out(emitter.Volume, AmbientFadeLength));
    }

    private void SetNewPermanent(
        bool isMusic,
        ref DefaultEcs.Entity entity,
        ref StdSceneInfo.AmbientMode? lastMode,
        StdSceneInfo.AmbientMode? nextMode)
    {
        if (entity.IsAlive)
        {
            if (nextMode == lastMode)
                return;
            else if (nextMode is not null && nextMode.Value.Id == lastMode?.Id)
            {
                ui.World.Publish(new messages.SetEmitterVolume(entity, nextMode.Value.IsQuiet ? QuietVolume : NormalVolume));
                lastMode = nextMode;
                return;
            }
            else if (!entity.Has<components.SoundFade>())
                entity.Dispose();
            entity = default;
        }
        lastMode = nextMode;
        if (nextMode is null)
            return;

        var samplePath = GetSamplePath(nextMode.Value.Id,
            isMusic ? MusicBasePath : AmbientBasePath,
            isMusic ? MusicExtension : AmbientExtension);
        var volume = nextMode.Value.IsQuiet ? QuietVolume : NormalVolume;

        entity = ui.World.CreateEntity();
        ui.World.Publish(new messages.SpawnSample(
            samplePath,
            Volume: volume,
            Looping: true,
            AsEntity: entity));
    }

    // the landscape sounds (in contrast to ambient) are loaded from the scene itself and can differ to ambient mode
    private void HandleSceneLoaded(in messages.SceneLoaded msg)
    {
        for (int i = 0; i < landscapeEntities.Count; i++)
        {
            if (landscapeEntities[i].IsAlive)
                landscapeEntities[i].Set(components.SoundFade.Out(1f, LandscapeFadeLength, LandscapeFadeDelay));
            landscapeEntities[i] = default;
        }
        landscapeSamples = GetLandscapeSamples(msg.Scene.ambientSound, out landscapeChance);

        // Keep the same number of entities as samples
        landscapeEntities.EnsureCapacity(landscapeSamples.Count);
        for (int i = landscapeEntities.Count; i < landscapeSamples.Count; i++)
            landscapeEntities.Add(default);
        if (landscapeEntities.Count > landscapeSamples.Count)
            landscapeEntities.RemoveRange(landscapeSamples.Count, landscapeEntities.Count - landscapeSamples.Count);
    }

    private static string GetSamplePath(int id, string basePath, string extension) =>
        basePath + id.ToString("D3") + extension;

    public void Update(float _)
    {
        if (time.TotalElapsed - lastUpdateTime < LandscapeUpdateTime)
            return;
        lastUpdateTime = time.TotalElapsed;
        int curPlaying = 0;
        foreach (var entity in landscapeEntities)
            curPlaying += entity.IsAlive ? 1 : 0;
        if (curPlaying >= TargetLandscapeSounds || Random.Next(99) + 1 >= landscapeChance)
            return;

        var relativePosition = Random.InCube() * LandscapeRadius;
        var position = gameWorld.Get<components.PlayerEntity>().Entity.Get<Location>().LocalPosition + relativePosition;
        int freeSlotIndex = Random.Next(landscapeEntities.Count - curPlaying);
        for (int i = 0; i < landscapeEntities.Count; i++)
        {
            if (landscapeEntities[i].IsAlive || freeSlotIndex-- != 0)
                continue;
            landscapeEntities[i] = ui.World.CreateEntity();
            ui.World.Publish(new messages.SpawnSample(
                GetSamplePath(landscapeSamples[i], LandscapeBasePath, LandscapeExtension),
                RefDistance: 5f,
                MaxDistance: 20f,
                Position: position,
                AsEntity: landscapeEntities[i]));
            logger.Verbose("Spawned landscape sample {Sample} at relative {Position}", landscapeSamples[i], relativePosition);
            return;
        }
    }
    
    private readonly record struct LandscapeMode(int Chance, int[] Samples);
    private static IReadOnlyList<int> GetLandscapeSamples(uint mode, out int chance)
    {
        var landscapeMode = mode == 0 ? default : LandscapeModes.GetValueOrDefault(mode - 1);
        chance = landscapeMode.Chance;
        return landscapeMode.Samples ?? [];

    }

    private static readonly IReadOnlyDictionary<uint, LandscapeMode> LandscapeModes = new Dictionary<uint, LandscapeMode>()
    {
        { 0, new(Chance: 25, [513, 514, 521, 522, 523, 524, 525, 554, 555, 556, 557]) },
        { 1, new(Chance: 10, [513, 514, 521, 522, 523, 524, 525, 554, 555, 556, 557]) },
        { 2, new(Chance: 25, [511, 512, 513, 514, 515, 516, 529, 530, 517, 518, 519, 520, 550, 551, 552, 553]) },
        { 4, new(Chance: 3, [513, 514, 521, 522, 523, 524, 525, 558, 559, 560, 561]) },
        { 5, new(Chance: 20, [544, 545, 546, 547, 548, 549]) },
        { 6, new(Chance: 25, [536, 537, 538, 539, 540, 541, 542, 543]) },
        { 7, new(Chance: 10, [500, 501, 502, 503, 504, 505]) },
        { 8, new(Chance: 10, [526, 527, 528, 531, 532]) },
        { 9, new(Chance: 10, [531, 532, 533, 534, 535]) },
        { 10, new(Chance: 5, [506, 508, 509, 510]) },
        { 11, new(Chance: 5, [506, 508, 509]) },
        { 12, new(Chance: 10, [578, 579, 580, 581, 582, 583, 584]) },
        { 14, new(Chance: 5, [562, 563, 564, 565, 566, 567, 568, 569, 570]) },
        { 15, new(Chance: 25, [571, 572, 573, 574, 575, 576, 577]) },
        { 16, new(Chance: 5, [585, 586, 587, 588, 589, 590, 591, 592, 593]) }
    };
}
