using System.Numerics;

namespace zzre.game.components.ui
{
    public enum Alignment
    {
        Min,
        Max,
        Center
    }

    public record struct FullAlignment(Alignment Horizontal, Alignment Vertical)
    {
        private static float GetAsFactor(Alignment a) => a switch
        {
            Alignment.Min => 0f,
            Alignment.Max => 1f,
            Alignment.Center => 0.5f
        };

        public Vector2 AsFactor => new Vector2(GetAsFactor(Horizontal), GetAsFactor(Vertical));

        public static readonly FullAlignment Center = new(Alignment.Center, Alignment.Center);
        public static readonly FullAlignment TopLeft = default;
    }
}
