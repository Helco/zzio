using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.Resource;
using DefaultEcs.System;
using zzio.effect;
using zzio.effect.parts;

namespace zzre.game.systems;

public partial class AdvanceEffectPlayback : AEntitySetSystem<float>
{
    private readonly IDisposable spawnEffectDisposable;

    public AdvanceEffectPlayback(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        spawnEffectDisposable = World.Subscribe<messages.SpawnEffectCombiner>(HandleSpawnEffect);
    }

    public override void Dispose()
    {
        base.Dispose();
        spawnEffectDisposable.Dispose();
    }

    private void HandleSpawnEffect(in messages.SpawnEffectCombiner msg)
    {
        var entity = msg.AsEntity ?? World.CreateEntity();
        entity.Set(ManagedResource<EffectCombiner>.Create(msg.EffectId));
        var effect = entity.Get<EffectCombiner>();
        entity.Set(new components.effect.CombinerPlayback(effect.parts.Sum(p => p.Duration) + 1f));
        entity.Set(new Location()
        {
            LocalPosition = msg.Position ?? effect.position,
            LocalRotation = Quaternion.CreateFromRotationMatrix(
                Matrix4x4.CreateLookTo(Vector3.Zero, effect.forwards, effect.upwards))
        });
        
        foreach (var part in effect.parts)
        {
            var partEntity = World.CreateEntity();
            partEntity.Set(new components.Parent(entity));
            switch(part)
            {
                case MovingPlanes movingPlanes: partEntity.Set(movingPlanes); break;
                default:
                    Console.WriteLine($"Warning: unsupported effect combiner part {part.Name}");
                    break;
            }
        }
    }

    [Update]
    private void Update(
        in DefaultEcs.Entity entity,
        float elapsedTime,
        ref components.effect.CombinerPlayback playback)
    {
        playback.CurTime += elapsedTime;
        if (playback.IsFinished)
        {
            playback.CurProgress = 0f;
            entity.Set<components.Dead>();
        }
    }
}
