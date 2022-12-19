using System;
using System.Diagnostics;
using System.Threading;

namespace zzre
{
    public class GameTime
    {
        private readonly Stopwatch watch = new();

        private int curFPS = 0;
        private TimeSpan lastSecond;
        private TimeSpan frameStart;

        public int TargetFramerate { get; set; } = 60;
        public float TotalElapsed => (float)watch.Elapsed.TotalSeconds;
        public float Delta { get; private set; } = 0.0f;
        public int Framerate { get; private set; } = 0;
        public bool HasFramerateChanged { get; private set; } = false;

        private TimeSpan TargetFrametime => TimeSpan.FromSeconds(1.0 / TargetFramerate);

        public GameTime()
        {
            watch.Start();
            lastSecond = frameStart = watch.Elapsed;
        }

        public void BeginFrame()
        {
            Delta = (float)(watch.Elapsed - frameStart).TotalSeconds;
            frameStart = watch.Elapsed;

            curFPS++;
            HasFramerateChanged = false;
            if ((frameStart - lastSecond).TotalSeconds >= 1)
            {
                Framerate = (int)(curFPS / (frameStart - lastSecond).TotalSeconds + 0.5);
                lastSecond = frameStart;
                curFPS = 0;
                HasFramerateChanged = true;
            }
        }

        public void EndFrame()
        {
            int delayMs = (int)(TargetFrametime - (watch.Elapsed - frameStart)).TotalMilliseconds;
            if (delayMs > 0)
                Thread.Sleep(delayMs);
        }
    }
}
