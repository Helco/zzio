using System.Numerics;
using Veldrid;

namespace zzre.rendering;

public static class VeldridExtensions
{
    public static SamplerDescription AsDescription(this SamplerAddressMode addressMode, SamplerFilter filter) => new()
    {
        AddressModeU = addressMode,
        AddressModeV = addressMode,
        AddressModeW = addressMode,
        Filter = filter,
        MaximumLod = 1000
    };

    public static RgbaFloat ToRgbaFloat(this Vector4 v) => new(v);
}
