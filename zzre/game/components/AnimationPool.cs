﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using zzio;

namespace zzre.game.components
{
    public readonly struct AnimationPool : IReadOnlyCollection<KeyValuePair<AnimationType, SkeletalAnimation>>
    {
        private static readonly int AnimationCount = Enum
            .GetValues<AnimationType>()
            .Select(v => (int)v)
            .Max() + 1;

        private readonly SkeletalAnimation?[] animations;

        private AnimationPool(AnimationType type, SkeletalAnimation animation)
        {
            animations = new SkeletalAnimation[AnimationCount];
        }
        public static AnimationPool CreateWith(AnimationType type, SkeletalAnimation animation) => new AnimationPool(type, animation);

        public bool Contains(AnimationType type) => animations?[(int)type] != null;

        public SkeletalAnimation this[AnimationType type] => animations?[(int)type] ??
            throw new IndexOutOfRangeException($"Animation pool does not contain animation {type}");

        public void Add(AnimationType type, SkeletalAnimation animation)
        {
            if (animations == null)
                throw new InvalidOperationException("Invalid animation pool cannot be modified");
            if (animations[(int)type] != null)
                throw new InvalidOperationException($"Animation pool already contains an animation {type}");
            animations[(int)type] = animation;
        }

        private IEnumerable<KeyValuePair<AnimationType, SkeletalAnimation>> AsEnumerable => animations
            .Select((anim, i) => new KeyValuePair<AnimationType, SkeletalAnimation>((AnimationType)i, anim!))
            .Where(t => t.Value != null);

        public int Count => AsEnumerable.Count();
        public IEnumerator<KeyValuePair<AnimationType, SkeletalAnimation>> GetEnumerator() => AsEnumerable.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
