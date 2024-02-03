using System;
using System.Numerics;
using zzio;

namespace zzre.game.components;

public readonly record struct FindActorFloorCollisions(float MaxDistance)
{
    public static readonly FindActorFloorCollisions Default = new(10f);
}

public readonly record struct ActorFloorCollision(
    Vector3 Point,
    FColor Color,
    string TextureName);
