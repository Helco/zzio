using System.Numerics;

namespace zzre.game.messages;

public readonly record struct SpawnEffectCombiner(
    string EffectFilename,
    DefaultEcs.Entity? AsEntity = null,
    Vector3? Position = null,
    bool DepthTest = true)
{
    public SpawnEffectCombiner(
        int EffectId,
        DefaultEcs.Entity? AsEntity = null,
        Vector3? Position = null,
        bool DepthTest = true)
        : this($"e{EffectId}", AsEntity, Position, DepthTest) { }
}
