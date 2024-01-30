using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.Resource;
using DefaultEcs.System;

namespace zzre.game.systems.effect;

public partial class EffectCombiner : AEntitySetSystem<float>
{
    private readonly IDisposable spawnEffectDisposable;

    public EffectCombiner(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
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
        entity.Set(ManagedResource<zzio.effect.EffectCombiner>.Create(msg.EffectId));
        var effect = entity.Get<zzio.effect.EffectCombiner>();
        entity.Set(new components.effect.CombinerPlayback(
            duration: effect.parts.Sum(p => p.Duration) + 1f,
            depthTest: msg.DepthTest));
        entity.Set(new Location()
        {
            LocalPosition = msg.Position ?? effect.position,
            LocalRotation = Quaternion.CreateFromRotationMatrix(
                Matrix4x4.CreateLookTo(Vector3.Zero, effect.forwards, effect.upwards))
        });
        
        foreach (var part in effect.parts)
        {
            var partEntity = World.CreateEntity();
            partEntity.Set(components.RenderOrder.LateEffect);
            partEntity.Set(components.Visibility.Visible);
            partEntity.Set(new components.Parent(entity));
            switch(part)
            {
                case zzio.effect.parts.MovingPlanes movingPlanes: partEntity.Set(movingPlanes); break;
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
