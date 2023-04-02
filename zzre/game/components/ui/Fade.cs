﻿using System;

namespace zzre.game.components.ui;

public record struct Fade(float From, float To, float Duration, float Time = 0f)
{
    public float Value => MathEx.Lerp(From, To, Time / Duration);

    public static Fade In(float duration) => new(0f, 0.8f, duration, 0f);
    public static Fade Out(float duration) => new(0.8f, 0f, duration / 2.5f, 0f); // Yes fades are always faster...

    public static readonly Fade StdIn = In(1.5f);
    public static readonly Fade StdOut = Out(0.8f);
}
