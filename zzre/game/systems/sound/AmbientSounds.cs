using System;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems;

internal sealed class AmbientSounds : ISystem<float>
{
    private const float AmbientFadeLength = 0.25f;
    private const float QuietVolume = 0.4f;
    private const float NormalVolume = 1f;
    private const string MusicBasePath = "resources/audio/music/_m";
    private const string AmbientBasePath = "resources/audio/sfx/ambients/_a";
    //private const string LandscapeBasePath = "resources/audio/sfx/landscapes/_l";
    private const string MusicExtension = ".mp3";
    private const string AmbientExtension = ".wav";
    //private const string LandscapeExtension = ".wav";

    private readonly UI ui;
    private readonly DefaultEcs.World gameWorld;
    private readonly IDisposable? playerLeavingSubscription;
    private readonly IDisposable? playerEnterSubscription;
    private readonly IDisposable? setAmbientSubscription;
    //private readonly List<DefaultEcs.Entity> landscapeSounds = [];

    //private IReadOnlyList<string> landscapeSamples = Array.Empty<string>();
    //private int landscapeChance = 0;
    private DefaultEcs.Entity musicEntity, ambientEntity;
    private StdSceneInfo.AmbientMode? lastMusic, lastAmbient;

    public bool IsEnabled { get; set; }

    public AmbientSounds(ITagContainer diContainer)
    {
        ui = diContainer.GetTag<UI>();
        gameWorld = diContainer.GetTag<DefaultEcs.World>();
        if (!(IsEnabled = ui.HasTag<SoundContext>()))
            return;
        playerLeavingSubscription = gameWorld.Subscribe<messages.PlayerLeaving>(HandleLeaving);
        playerEnterSubscription = gameWorld.Subscribe<messages.PlayerEntered>(HandleEntering);
        setAmbientSubscription = gameWorld.Subscribe<messages.SetAmbient>(HandleSetAmbient);
    }

    public void Dispose()
    {
        playerLeavingSubscription?.Dispose();
        playerEnterSubscription?.Dispose();
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
        SetNewPermanent(isMusic: true, ref musicEntity, ref lastMusic, StdSceneInfo.GetMusicMode(msg.SceneName));
        SetNewPermanent(isMusic: false, ref ambientEntity, ref lastAmbient, StdSceneInfo.GetAmbientMode(msg.SceneName));
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

    private static string GetSamplePath(int id, string basePath, string extension) =>
        basePath + id.ToString("D3") + extension;

    public void Update(float state)
    {
    }
}
