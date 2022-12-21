using System;

namespace zzio;

/*
 * Represents an effect value generated between [value-width, value+width] and animated by mod
 */
[Serializable]
public struct ValueRangeAnimation
{
    public float
        value,
        width,
        mod;

    public ValueRangeAnimation(float value = 0.0f, float width = 0.0f, float mod = 0.0f)
    {
        this.value = value;
        this.width = width;
        this.mod = mod;
    }
}