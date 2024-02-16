using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs.Resource;
using DefaultEcs.System;
using Silk.NET.OpenAL;

namespace zzre.game.systems;

public sealed partial class SoundEmitter : AEntitySetSystem<float>
{
    private const int InitialSourceCount = 16;
    private readonly OpenALDevice device;
    private readonly SoundContext context;
    private readonly Queue<uint> sourcePool = new(InitialSourceCount);
    private readonly IDisposable? spawnEmitterSubscription;
    private readonly IDisposable? emitterRemovedSubscription;

    public unsafe SoundEmitter(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        diContainer.TryGetTag(out device);
        if (!(IsEnabled = diContainer.HasTag<SoundContext>()))
            return;
        spawnEmitterSubscription = World.Subscribe<messages.SpawnSample>(HandleSpawnSample);
        emitterRemovedSubscription = World.SubscribeEntityComponentRemoved<components.SoundEmitter>(HandleEmitterRemoved);

        var sources = stackalloc uint[InitialSourceCount];
        device.AL.GenSources(InitialSourceCount, sources);
        for (int i = 0; i < InitialSourceCount; i++)
            sourcePool.Enqueue(sources[i]);
    }

    public override void Dispose()
    {
        base.Dispose();
        spawnEmitterSubscription?.Dispose();
        emitterRemovedSubscription?.Dispose();
    }

    private void HandleSpawnSample(in messages.SpawnSample msg)
    {
        var entity = msg.AsEntity ?? World.CreateEntity();
        entity.Set(ManagedResource<components.SoundBuffer>.Create(msg.SamplePath));
        entity.Set(new Location()
        {
            LocalPosition = msg.Position ?? Vector3.Zero,
            Parent = msg.ParentLocation
        });

        using var _ = context.EnsureIsCurrent();
        if (!sourcePool.TryDequeue(out var sourceId))
            sourceId = device.AL.GenSource();
        device.AL.SetSourceProperty(sourceId, SourceFloat.Gain, msg.Volume);
        device.AL.SetSourceProperty(sourceId, SourceFloat.ReferenceDistance, msg.RefDistance);
        device.AL.SetSourceProperty(sourceId, SourceFloat.MaxDistance, msg.MaxDistance);
        device.AL.SetSourceProperty(sourceId, SourceInteger.Buffer, entity.Get<components.SoundBuffer>().Id);
        if (msg.Paused)
            device.AL.SourcePause(sourceId);
        else
            device.AL.SourcePlay(sourceId);
    }

    private void HandleEmitterRemoved(in DefaultEcs.Entity entity, in components.SoundEmitter emitter)
    {
        using var _ = context.EnsureIsCurrent();
        device.AL.SourceStop(emitter.SourceId);
        device.AL.SetSourceProperty(emitter.SourceId, SourceInteger.Buffer, 0);
        sourcePool.Enqueue(emitter.SourceId);
    }

    [Update]
    private void Update(
        in components.SoundEmitter emitter,
        Location location)
    {
        device.AL.SetSourceProperty(emitter.SourceId, SourceVector3.Position, location.LocalPosition * -Vector3.UnitZ);
        device.AL.SetSourceProperty(emitter.SourceId, SourceVector3.Direction, location.InnerForward * -Vector3.UnitZ);
    }
}
