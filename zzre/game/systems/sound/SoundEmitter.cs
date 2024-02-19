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
        if (!(IsEnabled = diContainer.TryGetTag(out context)))
            return;
        spawnEmitterSubscription = World.Subscribe<messages.SpawnSample>(HandleSpawnSample);
        emitterRemovedSubscription = World.SubscribeEntityComponentRemoved<components.SoundEmitter>(HandleEmitterRemoved);

        using var _ = context.EnsureIsCurrent();
        var sources = stackalloc uint[InitialSourceCount];
        device.AL.GenSources(InitialSourceCount, sources);
        for (int i = 0; i < InitialSourceCount; i++)
        {
            if (sources[i] == 0)
                throw new InvalidOperationException("Source was not generated");
            sourcePool.Enqueue(sources[i]);
        }
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
        if (entity.World != World)
            throw new ArgumentException("Sample entity has to be created in UI World", nameof(msg));
        entity.Set(ManagedResource<components.SoundBuffer>.Create(msg.SamplePath));
        bool is3D = msg.Position.HasValue || msg.ParentLocation != null;
        if (is3D)
        {
            entity.Set(new Location()
            {
                LocalPosition = msg.Position ?? Vector3.Zero,
                Parent = msg.ParentLocation
            });
        }

        using var _ = context.EnsureIsCurrent();
        if (!sourcePool.TryDequeue(out var sourceId))
            sourceId = device.AL.GenSource();
        if (sourceId == 0)
            throw new InvalidOperationException("Source was not generated");
        device.AL.SetSourceProperty(sourceId, SourceFloat.Gain, msg.Volume);
        device.AL.SetSourceProperty(sourceId, SourceFloat.ReferenceDistance, msg.RefDistance);
        device.AL.SetSourceProperty(sourceId, SourceFloat.MaxDistance, msg.MaxDistance);
        device.AL.SetSourceProperty(sourceId, SourceFloat.MinGain, 0f);
        device.AL.SetSourceProperty(sourceId, SourceFloat.MaxGain, 1f);
        device.AL.SetSourceProperty(sourceId, SourceFloat.RolloffFactor, is3D ? 1f : 0f);
        device.AL.SetSourceProperty(sourceId, SourceInteger.Buffer, entity.Get<components.SoundBuffer>().Id);
        device.AL.SetSourceProperty(sourceId, SourceBoolean.Looping, msg.Looping);
        device.AL.SetSourceProperty(sourceId, SourceBoolean.SourceRelative, false);
        if (!is3D)
            device.AL.SetSourceProperty(sourceId, SourceVector3.Position, Vector3.Zero);
        if (msg.Paused)
            device.AL.SourcePause(sourceId);
        else
            device.AL.SourcePlay(sourceId);
        entity.Set(new components.SoundEmitter(sourceId, msg.Volume, msg.RefDistance, msg.MaxDistance));

        if (is3D)
            Update(entity.Get<components.SoundEmitter>(), entity.Get<Location>());

        device.Logger.Verbose("Spawned emitter for {Sample}", msg.SamplePath);
    }

    private void HandleEmitterRemoved(in DefaultEcs.Entity entity, in components.SoundEmitter emitter)
    {
        using var _ = context.EnsureIsCurrent();
        device.AL.SourceStop(emitter.SourceId);
        device.AL.SetSourceProperty(emitter.SourceId, SourceInteger.Buffer, 0);
        sourcePool.Enqueue(emitter.SourceId);
        device.AL.ThrowOnError();
    }

    [Update]
    private void Update(
        in components.SoundEmitter emitter,
        Location location)
    {
        device.AL.GetListenerProperty(ListenerVector3.Position, out var listenerPosition);
        var myPosition = location.LocalPosition * new Vector3(1, 1, -1);
        var distToListener = Vector3.Distance(listenerPosition, myPosition);
        float newRefDistance;
        if (emitter.MaxDistance >= distToListener)
        {
            if (emitter.ReferenceDistance < distToListener)
                newRefDistance = emitter.ReferenceDistance * (1f - (distToListener - emitter.ReferenceDistance) / (emitter.MaxDistance - emitter.ReferenceDistance));
            else
                newRefDistance = emitter.ReferenceDistance;
        }
        else
            newRefDistance = 1.1754944e-38f;

        device.AL.SetSourceProperty(emitter.SourceId, SourceFloat.ReferenceDistance, newRefDistance);
        device.AL.SetSourceProperty(emitter.SourceId, SourceVector3.Position, myPosition);
        device.AL.SetSourceProperty(emitter.SourceId, SourceVector3.Direction, location.InnerForward * new Vector3(1, 1, -1));
        device.AL.ThrowOnError();
    }
}
