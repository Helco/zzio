using System;
namespace zzre.game.components
{
    public struct NonFairyAnimation
    {
        public zzio.AnimationType Current;
        public zzio.AnimationType Next;
        public float Timer;
        public bool CanUseAlternativeIdles;

        public NonFairyAnimation(Random random)
        {
            Current = (zzio.AnimationType)(-1); // that is evil...
            Next = default;
            Timer = RandomStartTimer(random);
            CanUseAlternativeIdles = false;
        }

        public static float RandomStartTimer(Random random) => random.NextFloat() * 5000f + 0.2f;
    }
}
