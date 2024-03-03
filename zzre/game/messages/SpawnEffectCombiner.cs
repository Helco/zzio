using System;
using System.Numerics;
using zzio;

namespace zzre.game.messages;

public readonly record struct SpawnEffectCombiner(
    FilePath FullPath,
    DefaultEcs.Entity? AsEntity = null,
    Vector3? Position = null,
    bool DepthTest = true)
{
    private static readonly FilePath BasePath = new("resources/effects");

    public SpawnEffectCombiner(
        string Name,
        DefaultEcs.Entity? AsEntity = null,
        Vector3? Position = null,
    bool DepthTest = true)
    : this(BasePath.Combine(Name.EndsWith(".ed", StringComparison.OrdinalIgnoreCase) ? Name : Name + ".ed"),
          AsEntity, Position, DepthTest) { }

    public SpawnEffectCombiner(
    int EffectId,
    DefaultEcs.Entity? AsEntity = null,
    Vector3? Position = null,
        bool DepthTest = true)
        : this($"e{EffectId}.ed", AsEntity, Position, DepthTest) { }
}
