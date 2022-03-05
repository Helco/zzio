using System;
using System.Numerics;
using zzio;

namespace zzre
{
    public partial class Skeleton
    {
        private struct BoneAnimator
        {
            private readonly AnimationKeyFrame[] frames;
            private readonly float duration, blendDuration;
            private readonly bool loop;
            private readonly Quaternion startRotation;
            private readonly Vector3 startTranslation;
            private int currentFrameI;
            private float time, blendTime;

            public Quaternion CurRotation { get; private set; }
            public Vector3 CurTranslation { get; private set; }

            public bool IsFinished => MathEx.Cmp(Time, duration) && !loop;

            public float Time
            {
                get => time;
                set
                {
                    time = value;
                    NormalizeLoopTime();
                    UpdatePose();
                }
            }

            public BoneAnimator(AnimationKeyFrame[] frames, float duration, bool loop)
            {
                this.frames = frames;
                this.duration = duration;
                this.loop = loop;
                currentFrameI = 0;
                time = blendTime = 0f;
                blendDuration = float.NaN;
                CurRotation = startRotation = Quaternion.Identity;
                CurTranslation = startTranslation = Vector3.Zero;
                UpdatePose();
            }

            public BoneAnimator(AnimationKeyFrame[] frames, float duration, bool loop, float blendDuration, Quaternion startRotation, Vector3 startTranslation)
                : this(frames, duration, loop)
            {
                this.blendDuration = blendDuration;
                this.startRotation = startRotation;
                this.startTranslation = startTranslation;
            }

            public void AddTime(float delta)
            {
                if (float.IsFinite(blendTime))
                {
                    blendTime += delta;
                    if (blendTime >= blendDuration)
                        blendTime = float.NaN;
                }

                time += delta;
                if (loop)
                    NormalizeLoopTime();
                else
                    time = Math.Clamp(time, 0f, duration);

                UpdatePose();
            }

            private void NormalizeLoopTime()
            {
                while (time >= duration)
                    time -= duration;
                while (time < 0.0f)
                    time += duration;
            }

            private void UpdatePose()
            {
                if (frames[currentFrameI].time > time)
                    currentFrameI = 0;
                while (currentFrameI < frames.Length && frames[currentFrameI].time <= time)
                    currentFrameI++;
                int nextFrameI;
                if (loop)
                {
                    nextFrameI = currentFrameI % frames.Length;
                    currentFrameI = (currentFrameI + frames.Length - 1) % frames.Length;
                }
                else
                {
                    nextFrameI = Math.Min(currentFrameI, frames.Length - 1);
                    currentFrameI = Math.Max(0, currentFrameI - 1);
                }

                float nextFrameWeight;
                if (currentFrameI == nextFrameI)
                    nextFrameWeight = 1f;
                else
                {
                    float nextFrameTime = nextFrameI > 0
                        ? frames[nextFrameI].time
                        : duration + frames[nextFrameI].time;
                    float frameDuration = nextFrameTime - frames[currentFrameI].time;
                    nextFrameWeight = (time - frames[currentFrameI].time) / frameDuration;
                }

                var targetRotation = Quaternion.Lerp(frames[currentFrameI].rot, frames[nextFrameI].rot, nextFrameWeight);
                var targetTranslation = Vector3.Lerp(frames[currentFrameI].pos, frames[nextFrameI].pos, nextFrameWeight);

                if (float.IsFinite(blendTime))
                {
                    var blendWeight = blendTime / blendDuration;
                    CurRotation = Quaternion.Lerp(startRotation, targetRotation, blendWeight);
                    CurTranslation = Vector3.Lerp(startTranslation, targetTranslation, blendWeight);
                }
                else
                {
                    CurRotation = targetRotation;
                    CurTranslation = targetTranslation;
                }
            }
        }
    }
}
