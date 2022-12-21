﻿using zzio;

namespace zzre.game.components;

public readonly struct ClumpMaterialInfo
{
    public readonly IColor Color { get; init; }
    public readonly SurfaceProperties SurfaceProperties { get; init; }
}
