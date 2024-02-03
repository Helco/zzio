using System;
using zzio;

namespace zzre.game.components;

public struct ActorLighting
{
    public static readonly ActorLighting Default = new()
    {
        CurColor = FColor.White,
        LastTime = -1f
    };

    public FColor CurColor;
    public float LastTime;
}
