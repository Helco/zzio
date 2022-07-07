using System;

namespace zzre.game.components.ui
{
    // lower values are drawn later (as-if z values), Material order is runtime-defined
    public record struct RenderOrder(int Depth, int MaterialHash = int.MinValue) : IComparable<RenderOrder>
    {
        public static bool operator <(RenderOrder left, RenderOrder right) => left.CompareTo(right) < 0;
        public static bool operator <=(RenderOrder left, RenderOrder right) => left.CompareTo(right) <= 0;
        public static bool operator >(RenderOrder left, RenderOrder right) => left.CompareTo(right) > 0;
        public static bool operator >=(RenderOrder left, RenderOrder right) => left.CompareTo(right) >= 0;

        public int CompareTo(RenderOrder other) => Depth == other.Depth
            ? other.MaterialHash.CompareTo(MaterialHash) // to prevent overflows
            : other.Depth - Depth; // not necessary, we don't use max values for depth
    }
}
