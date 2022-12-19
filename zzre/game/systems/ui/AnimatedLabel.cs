using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;

namespace zzre.game.systems.ui
{
    public partial class AnimatedLabel : AEntitySetSystem<float>
    {
        private const float SegmentDuration = 0.03f;
        private const float BlinkDuration = 0.5f;
        private static readonly string[] BlinkTexts = { "{21}", "{20}" };

        public AnimatedLabel(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
        }

        [WithPredicate]
        private bool IsSomethingToDo(in components.ui.AnimatedLabel anim) => !anim.IsDone || anim.IsBlinking;

        [Update]
        private void Update(
            float elapsedTime,
            DefaultEcs.Entity entity,
            ref components.ui.AnimatedLabel anim,
            in components.ui.Label label)
        {
            anim.Timer += elapsedTime;
            if (!anim.IsDone && anim.Timer > SegmentDuration)
            {
                anim.Timer = 0f;
                for (int i = 0; i < anim.SegmentsPerAdd && !anim.IsDone; i++)
                    AdvanceSegment(ref anim);
                entity.Set(label with { Text = anim.FullText[..anim.NextCharI] });
            }
            else if (anim.IsBlinking && anim.Timer > BlinkDuration)
            {
                anim.Timer = 0f;
                var nextText = Blink(ref anim);
                entity.Set(label with { Text = nextText });
            }
        }

        private void AdvanceSegment(ref components.ui.AnimatedLabel anim)
        {
            var length = anim.FullText.Length;
            if (anim.NextCharI + 2 < length &&
                anim.FullText[anim.NextCharI] == '{' &&
                anim.FullText[anim.NextCharI + 2] == '*')
                anim.NextCharI += 3;

            if (anim.NextCharI < length &&
                anim.FullText[anim.NextCharI] == '{')
            {
                anim.NextCharI = anim.FullText.IndexOf('}', anim.NextCharI);
                if (anim.NextCharI < 0)
                    anim.NextCharI = anim.FullText.Length;
            }

            if (anim.NextCharI < length &&
                anim.FullText[anim.NextCharI] == '}')
                anim.NextCharI++;

            if (anim.NextCharI < length)
                anim.NextCharI++;
        }

        private string Blink(ref components.ui.AnimatedLabel anim)
        {
            anim.NextBlinkI = (anim.NextBlinkI + 1) % BlinkTexts.Length;
            return anim.FullText + BlinkTexts[anim.NextBlinkI];
        }
    }
}
