using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using zzio;

namespace zzre
{
    public partial class Skeleton
    {
        private class BoneAnimator
        {
            private readonly AnimationKeyFrame[] frames;
            private readonly float duration;
            private int currentFrameI = 0;
            private float time = 0.0f;

            public Quaternion CurRotation { get; private set; }
            public Vector3 CurTranslation { get; private set; }

            public float Time
            {
                get => time;
                set
                {
                    time = value;
                    NormalizeTime();
                    UpdatePose();
                }
            }

            public BoneAnimator(AnimationKeyFrame[] frames, float duration)
            {
                this.frames = frames;
                this.duration = duration;
                currentFrameI = 0;
                UpdatePose();
            }

            public void AddTime(float delta)
            {
                time += delta;
                NormalizeTime();
                UpdatePose();
            }

            private void NormalizeTime()
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
                while (frames[currentFrameI].time <= time && currentFrameI < frames.Length)
                    currentFrameI++;
                int nextFrameI = currentFrameI;
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
