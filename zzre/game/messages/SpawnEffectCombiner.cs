using System;
using System.Numerics;

namespace zzre.game.messages;

public readonly record struct SpawnEffectCombiner(
    int EffectId,
    DefaultEcs.Entity? AsEntity = null,
    Vector3? Position = null,
    bool DepthTest = true);
