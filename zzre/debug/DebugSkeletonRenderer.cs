using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;
using ImGuiNET;
using static ImGuiNET.ImGui;
using zzre.imgui;


namespace zzre;

public enum DebugSkeletonRenderMode
{
    Invisible,
    Bones,
    Skin,
    SingleSkinBone
}

public class DebugSkeletonRenderer : BaseDisposable
{
    /* Rhombus indices
     *           
     *       __-0-__
     *     1--------2
     *    /        /
     *   4---------3
     *    \       /
     *     \     /
     *      \   /
     *        5
     */
    private const int RhombusVertexCount = 6;
    private static readonly ushort[] RhombusIndices = new ushort[]
    {
        0, 2, 1,    0, 3, 2,    0, 4, 3,    0, 1, 4,
        5, 3, 4,    5, 2, 3,    5, 1, 2,    5, 4, 1
    };
    private const float RhombusBaseSize = 0.075f; // based on a rhombus of length 1
    private const float RhombusBaseOffset = 0.1f; // ^
    private const byte Alpha = 120;
    private const float LineLength = 0.1f;
    private static readonly IColor[] Colors = { IColor.Red.WithA(Alpha), IColor.Green.WithA(Alpha), IColor.Blue.WithA(Alpha) };

    private readonly LocationBuffer locationBuffer;
    private readonly GraphicsDevice device;
    private readonly StaticMesh boneMesh;
    private readonly StaticMesh.VertexAttribute boneColorAttribute;
    private readonly DebugLineRenderer lineRenderer;
    private readonly IReadOnlyList<int> boneDepths;
    private readonly DeviceBufferRange worldBufferRange;

    private DebugSkeletonRenderMode renderMode = DebugSkeletonRenderMode.Invisible;
    private int highlightedBoneI = -1;

    public DebugMaterial BoneMaterial { get; }
    public DebugMaterial SkinMaterial { get; }
    public DebugMaterial SkinHighlightedMaterial { get; }
    public DebugMaterial LinesMaterial => lineRenderer.Material;
    public StaticMesh Mesh { get; }
    public Skeleton Skeleton { get; }
    public ref DebugSkeletonRenderMode RenderMode => ref renderMode; // reference for using the field with ImGui

    public DebugSkeletonRenderer(ITagContainer diContainer, StaticMesh mesh, Skeleton skeleton)
    {
        Mesh = mesh;
        Skeleton = skeleton;
        var camera = diContainer.GetTag<Camera>();
        locationBuffer = diContainer.GetTag<LocationBuffer>();
        worldBufferRange = locationBuffer.Add(skeleton.Location);

        void LinkTransformsFor(IStandardTransformMaterial m)
        {
            m.LinkTransformsTo(camera);
            m.World.BufferRange = worldBufferRange;
        }
        BoneMaterial = new DebugMaterial(diContainer) { IsSkinned = true };
        LinkTransformsFor(BoneMaterial);
        BoneMaterial.Pose.Skeleton = skeleton;
        SkinMaterial = new(diContainer) { Color = DebugMaterial.ColorMode.SkinWeights };
        LinkTransformsFor(SkinMaterial);
        SkinHighlightedMaterial = new(diContainer) { Color = DebugMaterial.ColorMode.SingleBoneWeight };
        LinkTransformsFor(SkinHighlightedMaterial);
        lineRenderer = new(diContainer);
        LinkTransformsFor(LinesMaterial);
        device = diContainer.GetTag<GraphicsDevice>();

        var vertices = Enumerable.Empty<Vector3>();
        var colors = Enumerable.Empty<IColor>();
        var skinIndices = Enumerable.Empty<IColor>(); // color as this is also 4 bytes
        foreach (var (bone, index) in skeleton.Bones.Indexed())
        {
            if (bone.Parent == null)
                continue;
            var to = bone.GlobalPosition;
            var from = bone.Parent.GlobalPosition;
            var length = (to - from).Length();
            var baseSize = length * RhombusBaseSize;

            var normal = MathEx.CmpZero(length)
                ? Vector3.UnitY
                : (to - from) / length;
            var tangent = Vector3.Normalize(normal.SomeOrthogonal()) * baseSize;
            var bitangent = Vector3.Normalize(Vector3.Cross(normal, tangent)) * baseSize;
            var baseCenter = from + normal * length * RhombusBaseOffset;

            vertices = vertices.Concat(new[]
            {
                from,
                baseCenter - tangent - bitangent,
                baseCenter + tangent - bitangent,
                baseCenter + tangent + bitangent,
                baseCenter - tangent + bitangent,
                to
            });
            colors = colors.Concat(Enumerable.Repeat(Colors[index % Colors.Length], RhombusVertexCount));
            var indices = new IColor(unchecked((byte)Skeleton.Parents[index]), 0, 0, 0);
            skinIndices = skinIndices.Concat(Enumerable.Repeat(indices, RhombusVertexCount));
        }
        boneMesh = new(diContainer, "Debug Skeleton Bones");
        boneMesh.Add("Pos", "inPos", vertices.ToArray());
        boneColorAttribute = boneMesh.Add("Color", "inColor", colors.ToArray());
        boneMesh.Add("Bone Weights", "inWeights", Enumerable.Repeat(Vector4.UnitX, boneMesh.VertexCount).ToArray());
        boneMesh.Add("Bone Indices", "inIndices", skinIndices.ToArray());
        boneMesh.SetIndicesFromPattern(RhombusIndices);

        lineRenderer.Add(Colors[0], Vector3.Zero, Vector3.UnitX * LineLength);
        lineRenderer.Add(Colors[1], Vector3.Zero, Vector3.UnitY * LineLength);
        lineRenderer.Add(Colors[2], Vector3.Zero, Vector3.UnitZ * LineLength);

        var boneDepthsArr = new int[Skeleton.Bones.Count];
        for (int i = 0; i < Skeleton.Bones.Count; i++)
            boneDepthsArr[i] = Skeleton.Parents[i] < 0 ? 0 : boneDepthsArr[Skeleton.Parents[i]] + 1;
        boneDepths = boneDepthsArr;
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        boneMesh.Dispose();
        lineRenderer.Dispose();
        BoneMaterial.Dispose();
        SkinMaterial.Dispose();
        SkinHighlightedMaterial.Dispose();
        LinesMaterial.Dispose();
        locationBuffer.Remove(worldBufferRange);
    }

    public void Render(CommandList cl)
    {
        if (RenderMode == DebugSkeletonRenderMode.Invisible)
            return;

        if (RenderMode == DebugSkeletonRenderMode.Skin)
            RenderMesh(cl, SkinMaterial);
        if (RenderMode == DebugSkeletonRenderMode.SingleSkinBone)
            RenderMesh(cl, SkinHighlightedMaterial);

        // always draw bones when visible
        (BoneMaterial as IMaterial).Apply(cl);
        BoneMaterial.ApplyAttributes(cl, boneMesh);
        cl.SetIndexBuffer(boneMesh.IndexBuffer, boneMesh.IndexFormat);
        cl.DrawIndexed((uint)boneMesh.IndexCount);

        if (highlightedBoneI >= 0)
        {
            LinesMaterial.World.Ref = Skeleton.Bones[highlightedBoneI].LocalToWorld;
            lineRenderer.Render(cl);
        }
    }

    private void RenderMesh(CommandList cl, MlangMaterial material)
    {
        (material as IMaterial).Apply(cl);
        material.ApplyAttributes(cl, Mesh);
        cl.SetIndexBuffer(Mesh.IndexBuffer, Mesh.IndexFormat);
        cl.DrawIndexed(
            indexCount: (uint)Mesh.SubMeshes.Sum(sm => sm.IndexCount),
            instanceCount: 1,
            indexStart: 0,
            vertexOffset: 0,
            instanceStart: (uint)(highlightedBoneI < 0
                ? Skeleton.Bones.Count
                : highlightedBoneI));
    }

    private void SetBoneAlpha(int boneI, byte alpha)
    {
        if (boneI < 1)
            return;

        // this is probably a terrible but also very lazy way of doing this
        for (int vertexI = 0; vertexI < RhombusVertexCount; vertexI++)
        {
            var offset = (boneI * RhombusVertexCount + vertexI) * 4 + 3;
            device.UpdateBuffer(boneColorAttribute.DeviceBuffer, (uint)offset, alpha);
        }
    }

    public void HighlightBone(int boneIdx)
    {
        if (highlightedBoneI >= 0)
            SetBoneAlpha(highlightedBoneI, 120);
        SetBoneAlpha(highlightedBoneI = boneIdx, 255);
    }

    public bool Content()
    {
        var hasChanged = false;
        hasChanged |= ImGuiEx.EnumRadioButtonGroup(ref renderMode);
        NewLine();

        int curDepth = 0;
        for (int i = 0; i < Skeleton.Bones.Count; i++)
        {
            if (curDepth < boneDepths[i])
                continue;
            while (curDepth > boneDepths[i])
            {
                curDepth--;
                TreePop();
            }

            var flags = (i == highlightedBoneI ? ImGuiTreeNodeFlags.Selected : 0) |
                ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow |
                ImGuiTreeNodeFlags.DefaultOpen;
            if (TreeNodeEx($"Bone {i} \"{Skeleton.UserIds[i]}\"", flags))
                curDepth++;
            if (IsItemClicked() && i != highlightedBoneI)
            {
                HighlightBone(i);
                hasChanged = true;
            }
        }
        while (curDepth > 0)
        {
            curDepth--;
            TreePop();
        }

        return hasChanged;
    }
}
