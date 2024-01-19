using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio;
using zzre.rendering;

namespace zzre.materials;

public struct ColoredVertex
{
    public Vector3 pos;
    public IColor color;

    public ColoredVertex(Vector3 pos, IColor color)
    {
        this.pos = pos;
        this.color = color;
    }

    public static uint Stride = sizeof(float) * 3 + sizeof(uint);
}

public struct SkinVertex
{
    public Vector4 weights;
    public byte bone0, bone1, bone2, bone3;
    public static uint Stride = sizeof(float) * 4 + sizeof(byte) * 4;
}

public class DebugMaterial : MlangMaterial, IStandardTransformMaterial
{
    public enum ColorMode : uint
    {
        VertexColor = 0,
        SkinWeights,
        SingleBoneWeight
    }

    public enum TopologyMode : uint
    {
        Triangles = 0,
        Lines
    }

    public bool IsSkinned { set => SetOption(nameof(IsSkinned), value); }
    public ColorMode Color { set => SetOption(nameof(ColorMode), (uint)value); }
    public TopologyMode Topology { set => SetOption(nameof(Topology), (uint)value); }
    public bool BothSided { set => SetOption(nameof(BothSided), value); }

    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }
    public SkeletonPoseBinding Pose { get; }

    public DebugMaterial(ITagContainer diContainer) : base(diContainer, "debug")
    {
        AddBinding("world", World = new(this));
        AddBinding("projection", Projection = new(this));
        AddBinding("view", View = new(this));
        AddBinding("pose", Pose = new(this));
    }
}

public class DebugDynamicMesh : DynamicMesh
{
    private readonly Attribute<Vector3> attrPos;
    private readonly Attribute<IColor> attrColor;

    public DebugDynamicMesh(ITagContainer diContainer, bool dynamic = true) : base(diContainer, dynamic)
    {
        attrPos = AddAttribute<Vector3>("inPos");
        attrColor = AddAttribute<IColor>("inColor");
    }

    public void Add(ColoredVertex v)
    {
        var index = Add(1);
        attrPos[index] = v.pos;
        attrColor[index] = v.color;
    }
}
