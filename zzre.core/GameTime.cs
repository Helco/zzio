using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace zzre;

public class GameTime
{
    private readonly Stopwatch watch = new();
    private readonly List<double> curFrametimes = new List<double>(60);

    private TimeSpan lastSecond;
    private TimeSpan frameStart;

    public int TargetFramerate { get; set; } = 60;
    public float TotalElapsed => (float)watch.Elapsed.TotalSeconds;
    public float MaxDelta { get; set; } = 1 / 30f;
    public float Delta => Math.Min(MaxDelta, UnclampedDelta);
    public float UnclampedDelta { get; private set; }
    public int Framerate { get; private set; }
    public double FrametimeAvg { get; private set; }
    public double FrametimeSD { get; private set; }
    public bool HasFramerateChanged { get; private set; }

    private TimeSpan TargetFrametime => TimeSpan.FromSeconds(1.0 / TargetFramerate);
    public string FormattedStats => $"FPS: {Framerate} | FT: {FrametimeAvg:F2}ms";

    public GameTime()
    {
        watch.Start();
        lastSecond = frameStart = watch.Elapsed;
    }

    public void BeginFrame()
    {
        UnclampedDelta = (float)(watch.Elapsed - frameStart).TotalSeconds;
        frameStart = watch.Elapsed;

        HasFramerateChanged = false;
        if ((frameStart - lastSecond).TotalSeconds >= 1 && curFrametimes.Any())
        {
            Framerate = (int)(curFrametimes.Count / (frameStart - lastSecond).TotalSeconds + 0.5);
            FrametimeAvg = curFrametimes.Average();
            FrametimeSD = curFrametimes.Sum(f => Math.Pow(f - FrametimeAvg, 2)) / curFrametimes.Count;
            curFrametimes.Clear();
            lastSecond = frameStart;
            HasFramerateChanged = true;
        }
    }

    public void EndFrame()
    {
        curFrametimes.Add((watch.Elapsed - frameStart).TotalMilliseconds);
        int delayMs = (int)(TargetFrametime - (watch.Elapsed - frameStart)).TotalMilliseconds;
        if (delayMs > 0)
            Thread.Sleep(delayMs);
    }
}
