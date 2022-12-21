using System;
using System.Numerics;

namespace zzre.game.components.ui;

public record struct Slider(Vector2 SizeFactor, Vector2 Current, bool WasHovered)
{
    public static readonly Slider Horizontal = new(Vector2.UnitX, Vector2.Zero, false);
    public static readonly Slider Vertical = new(Vector2.UnitY, Vector2.Zero, false);
}
