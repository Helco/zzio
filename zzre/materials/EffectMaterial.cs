using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using zzio;
using zzre.rendering;

namespace zzre.materials;

[StructLayout(LayoutKind.Sequential)]
public struct EffectFactors
{
    public float alphaReference;

    public static readonly EffectFactors Default = new()
    {
        alphaReference = 0.03f
    };
}

public class EffectMaterial : MlangMaterial, ITexturedMaterial
{
    public enum BillboardMode : uint
    {
        None,
        View,
        Spark,
        LensFlare
    }

    public enum BlendMode : uint
    {
        Additive,
        AdditiveAlpha,
        Alpha
    }

    public bool DepthTest { set => SetOption(nameof(DepthTest), value); }
    public BillboardMode Billboard { set => SetOption(nameof(Billboard), (uint)value); }
    public BlendMode Blend { set => SetOption(nameof(Blend), (uint)value); }
    public bool HasFog { set => SetOption(nameof(HasFog), value); }

    public TextureBinding Texture { get; }
    public SamplerBinding Sampler { get; }
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<EffectFactors> Factors { get; }
    public UniformBinding<FogParams> FogParams { get; }

    public EffectMaterial(ITagContainer diContainer) : base(diContainer, "effect")
    {
        DepthTest = true;
        AddBinding("mainTexture", Texture = new(this));
        AddBinding("mainSampler", Sampler = new(this));
        AddBinding("projection", Projection = new(this));
        AddBinding("view", View = new(this));
        AddBinding("factors", Factors = new(this));
        AddBinding("fogParams", FogParams = new(this));
    }
}

public class EffectMesh : DynamicMesh
{
    public Attribute<Vector3> Pos { get; }
    public Attribute<Vector2> UV { get; }
    public Attribute<IColor> Color { get; }
    public Attribute<Vector3> Center { get; }
    public Attribute<Vector3> Direction { get; }

    public EffectMesh(ITagContainer diContainer, int initialVertices, int initialIndices) : base(diContainer, dynamic: true)
    {
        Pos = AddAttribute<Vector3>("inVertexPos");
        UV = AddAttribute<Vector2>("inUV");
        Color = AddAttribute<IColor>("inColor");
        Center = AddAttribute<Vector3>("inCenterPos");
        Direction = AddAttribute<Vector3>("inDirection");
        Preallocate(initialVertices, initialIndices);
    }

    public Range RentPatternIndices(Range vertexRange, IReadOnlyList<ushort> pattern)
    {
        var (vertexOffset, vertexCount) = vertexRange.GetOffsetAndLength(VertexCapacity);
        var verticesPerPrimitive = pattern.Max() + 1;
        if (vertexCount % verticesPerPrimitive != 0)
            throw new ArgumentException("Vertex range does not align with pattern");
        var primitiveCount = vertexCount / verticesPerPrimitive;
        var indexCount = primitiveCount * pattern.Count;
        var indexRange = RentIndices(indexCount);
        StaticMesh.GeneratePatternIndices(
            WriteIndices(indexRange),
            pattern,
            primitiveCount,
            verticesPerPrimitive,
            vertexOffset);
        return indexRange;
    }

    private static readonly ushort[] SingleSidedQuadIndexPattern = [0, 2, 1, 0, 3, 2];
    private static readonly ushort[] DoubleSidedQuadIndexPattern = [0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3];
    public Range RentQuadIndices(Range vertexRange, bool doubleSided = false) =>
        RentPatternIndices(vertexRange, doubleSided
            ? DoubleSidedQuadIndexPattern
            : SingleSidedQuadIndexPattern);

    public void SetQuad(Range vertexRange, int offset, bool applyCenter, Vector3 center, Vector3 right, Vector3 up, IColor color, Rect texCoords)
    {
        var (vertexOffset, vertexCount) = vertexRange.GetOffsetAndLength(VertexCapacity);
        if (vertexCount < offset + 4)
            throw new ArgumentException("Quad does not fit given vertex range with offset");
        vertexOffset += offset;
        var centerToApply = applyCenter ? center : Vector3.Zero;
        var attrPos = Pos.Write(vertexOffset, 4);
        attrPos[0] = centerToApply - right - up;
        attrPos[1] = centerToApply - right + up;
        attrPos[2] = centerToApply + right + up;
        attrPos[3] = centerToApply + right - up;
        var attrUV = UV.Write(vertexOffset, 4);
        attrUV[0] = new(texCoords.Min.X, texCoords.Min.Y);
        attrUV[1] = new(texCoords.Min.X, texCoords.Max.Y);
        attrUV[2] = new(texCoords.Max.X, texCoords.Max.Y);
        attrUV[3] = new(texCoords.Max.X, texCoords.Min.Y);
        Color.Write(vertexOffset, 4).Fill(color);
        if (!applyCenter)
            Center.Write(vertexOffset, 4).Fill(center);
    }

    private const int OriginalTexSize = 256;
    public static Rect GetTileUV(uint tileW, uint tileH, uint tileId)
    {
        float texTileW = tileW / (float)OriginalTexSize;
        float texTileH = tileH / (float)OriginalTexSize;
        uint tilesInX = OriginalTexSize / tileW;
        return new Rect(new Vector2(
            (tileId % tilesInX + 0.5f) * texTileW,
            (tileId / tilesInX + 0.5f) * texTileH),
            new Vector2(texTileW, texTileH));
    }

    public static Rect TexShift(Rect input, float angle, float amplitude)
    {
        var newMin = input.Min + Vector2.One * MathF.Sin(angle) * amplitude;
        var newMax = input.Max + Vector2.One * MathF.Cos(angle) * amplitude;
        return new Rect((newMin + newMax) / 2f, newMax - newMin);
    }
}
