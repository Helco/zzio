using System;

namespace zzre.game.components.ui
{
    public struct AnimatedLabel
    {
        public readonly string FullText;
        public readonly bool IsBlinking;
        public int SegmentsPerAdd;
        public int NextCharI;
        public int NextBlinkI;
        public float Timer;

        public bool IsDone => NextCharI >= (FullText?.Length ?? 0);

        public AnimatedLabel(string fullText, int segmentsPerAdd, bool isBlinking)
        {
            FullText = fullText;
            SegmentsPerAdd = segmentsPerAdd;
            IsBlinking = isBlinking;
            NextCharI = NextBlinkI = 0;
            Timer = 0f;
        }
    }
}
