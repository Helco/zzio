using System.Numerics;

namespace zzre.game.components.ui
{
    public record struct UIOffset(Vector2 Offset, bool GameOnly)
    {
        private const float CanonicalRatio = 1024f / 768f;

        public Vector2 Calc(Vector2 position, Rect logicalScreen)
        {
            if (GameOnly)
            {
                var min = logicalScreen.Min;
                var size = logicalScreen.Size;
                if (min.X < 0)
                {
                    min.X = 0;
                    size.X = size.Y * CanonicalRatio;
                }
                if (min.Y < 0)
                {
                    min.Y = 0;
                    size.Y = size.X / CanonicalRatio;
                }
            }

            return MathEx.Floor(logicalScreen.AbsolutePos(Offset)) + position;
        }

        public static readonly UIOffset ScreenUpperLeft = new UIOffset(Vector2.Zero, GameOnly: false);
        public static readonly UIOffset GameUpperLeft = new UIOffset(Vector2.Zero, GameOnly: true);
        public static readonly UIOffset GameUpperRight = new UIOffset(Vector2.UnitX, GameOnly: true);
        public static readonly UIOffset GameLowerLeft = new UIOffset(Vector2.UnitY, GameOnly: true);
        public static readonly UIOffset GameLowerRight = new UIOffset(Vector2.One, GameOnly: true);
        public static readonly UIOffset Center = new UIOffset(Vector2.One / 2f, GameOnly: false);
    }
}
