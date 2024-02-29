using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using zzio;

namespace zzre.game.components;

public struct AnimationPool : IEnumerable<KeyValuePair<AnimationType, SkeletalAnimation>>
{
    [InlineArray((int)AnimationType.@Count)]
    private struct Animations
    {
        public SkeletalAnimation? _;
    }
    private Animations animations;

    public bool Contains(AnimationType type) => animations[(int)type] != null;

    public SkeletalAnimation this[AnimationType type] => animations[(int)type] ??
        throw new KeyNotFoundException($"Animation pool does not contain animation {type}");

    public void Add(AnimationType type, SkeletalAnimation animation)
    {
        // Here was a check to see whether animations were overridden, but the original chr01.aed would not load...
        animations[(int)type] = animation;
    }

    public IEnumerator<KeyValuePair<AnimationType, SkeletalAnimation>> GetEnumerator()
    {
        for (int i = 0; i < (int)AnimationType.@Count; i++)
        {
            if (animations[i] != null)
                yield return new((AnimationType)i, animations[i]!);
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
