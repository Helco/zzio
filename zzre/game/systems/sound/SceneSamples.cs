using System;
using System.Collections.Generic;
using DefaultEcs.System;
using Serilog;
using zzio;

namespace zzre.game.systems;

[With<zzio.scn.Sample3D>]
public sealed partial class SceneSamples : ISystem<float>
{
    private static readonly FilePath BasePath = new("resources/audio/sfx/landscapes/");

    private readonly ILogger logger;
    private readonly DefaultEcs.World uiWorld;
    private readonly IDisposable? sceneChangingSubscription;
    private readonly IDisposable? sceneLoadedSubscription;
    private readonly List<DefaultEcs.Entity> samples = [];

    public bool IsEnabled { get; set; }

    public SceneSamples(ITagContainer diContainer)
    {
        logger = diContainer.GetLoggerFor<SceneSamples>();
        var ui = diContainer.GetTag<UI>();
        uiWorld = ui.World;
        IsEnabled = ui.HasTag<SoundContext>();
        if (!IsEnabled)
            return;

        var gameWorld = diContainer.GetTag<DefaultEcs.World>();
        sceneChangingSubscription = gameWorld.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        sceneLoadedSubscription = gameWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
    }

    public void Dispose()
    {
        sceneChangingSubscription?.Dispose();
        sceneLoadedSubscription?.Dispose();
    }

    private void HandleSceneChanging(in messages.SceneChanging _)
    {
        foreach (var entity in samples)
        {
            if (entity.IsAlive)
                entity.Dispose();
        }
        samples.Clear();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded msg)
    {
        samples.EnsureCapacity(msg.Scene.samples3D.Length);
        foreach (var sample in msg.Scene.samples3D)
        {
            if (sample.loopCount > 1)
            {
                logger.Warning("3D Sample {Index} has an unsupported loop count of {LoopCount}, set to infinite looping", sample.idx, sample.loopCount);
                sample.loopCount = 0;
            }
            var entity = uiWorld.CreateEntity();
            uiWorld.Publish(new messages.SpawnSample(
                BasePath.Combine(sample.filename + ".wav").ToPOSIXString(),
                RefDistance: sample.minDist,
                MaxDistance: sample.maxDist,
                Volume: sample.volume / 100f,
                Looping: sample.loopCount == 0,
                AsEntity: entity,
                Position: sample.pos));
            samples.Add(entity);
        }
    }

    public void Update(float _) { }
}
