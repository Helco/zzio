using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using zzio;

namespace zzre
{
    public partial class Skeleton
    {
        private struct BoneAnimator
        {
            private readonly AnimationKeyFrame[] frames;
            private readonly float duration;
            private readonly bool loop;
            private int currentFrameI;
            private float time;

            public Quaternion CurRotation { get; private set; }
            public Vector3 CurTranslation { get; private set; }

            public bool IsFinished => Time == duration;

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
                time = 0f;
                CurRotation = Quaternion.Identity;
                CurTranslation = Vector3.Zero;
                UpdatePose();
            }

            public void AddTime(float delta)
            {
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
                int nextFrameI = currentFrameI % frames.Length;
                currentFrameI = (currentFrameI + frames.Length - 1) % frames.Length;

                float nextFrameTime = nextFrameI > 0
                    ? frames[nextFrameI].time
                    : duration + frames[nextFrameI].time;
                float frameDuration = nextFrameTime - frames[currentFrameI].time;
                float nextFrameWeight = (time - frames[currentFrameI].time) / frameDuration;

                CurRotation = Quaternion.Lerp(frames[currentFrameI].rot.ToNumerics(), frames[nextFrameI].rot.ToNumerics(), nextFrameWeight);
                CurTranslation = Vector3.Lerp(frames[currentFrameI].pos.ToNumerics(), frames[nextFrameI].pos.ToNumerics(), nextFrameWeight);
            }
        }
    }
}
