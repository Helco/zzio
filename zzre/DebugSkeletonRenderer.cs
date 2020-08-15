using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Veldrid;
using zzio.primitives;
using zzre.materials;
using zzre.rendering;
using Quaternion = System.Numerics.Quaternion;
using ImGuiNET;
using static ImGuiNET.ImGui;
using zzre.imgui;

namespace zzre
{
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
        private static readonly IColor[] Colors = new[] { IColor.Red.WithA(Alpha), IColor.Green.WithA(Alpha), IColor.Blue.WithA(Alpha) };

        private readonly GraphicsDevice device;
        private readonly DeviceBuffer vertexBuffer;
        private readonly DeviceBuffer indexBuffer;
        private readonly DeviceBuffer skinBuffer;
        private readonly IReadOnlyList<int> boneDepths;

        private DebugSkeletonRenderMode renderMode = DebugSkeletonRenderMode.Invisible;
        private int highlightedBoneI = -1;

        public DebugSkinnedMaterial BoneMaterial { get; }
        public DebugSkinAllMaterial SkinMaterial { get; }
        public DebugSkinSingleMaterial SkinHighlightedMaterial { get; }
        public RWGeometryBuffers Geometry { get; }
        public Skeleton Skeleton { get; }
        public ref DebugSkeletonRenderMode RenderMode => ref renderMode;

        public DebugSkeletonRenderer(ITagContainer diContainer, RWGeometryBuffers geometryBuffers, Skeleton skeleton)
        {
            Geometry = geometryBuffers;
            Skeleton = skeleton;
            BoneMaterial = new DebugSkinnedMaterial(diContainer);
            SkinMaterial = new DebugSkinAllMaterial(diContainer);
            SkinMaterial.Alpha.Ref = 1.0f;
            SkinHighlightedMaterial = new DebugSkinSingleMaterial(diContainer);
            SkinHighlightedMaterial.BoneIndex.Ref = -1;
            device = diContainer.GetTag<GraphicsDevice>();

            var vertices = Enumerable.Empty<ColoredVertex>();
            var skinVertices = Enumerable.Empty<SkinVertex>();
            var indices = Enumerable.Empty<ushort>();
            foreach (var (bone, index) in skeleton.Bones.Indexed())
            {
                if (bone.Parent == null)
                    continue;
                var to = bone.GlobalPosition;
                var from = bone.Parent.GlobalPosition;
                var length = (to - from).Length();
                var baseSize = length * RhombusBaseSize;
                var tangent = bone.GlobalUp * baseSize;
                var bitangent = bone.GlobalRight * baseSize;
                var baseCenter = from + bone.GlobalForward * length * RhombusBaseOffset;

                vertices = vertices.Concat(new[]
                {
                    from,
                    baseCenter - tangent - bitangent,
                    baseCenter + tangent - bitangent,
                    baseCenter + tangent + bitangent,
                    baseCenter - tangent + bitangent,
                    to
                }.Select(p => new ColoredVertex(p, Colors[index % Colors.Length])));
                skinVertices = skinVertices.Concat(Enumerable.Repeat(new SkinVertex()
                {
                    bone0 = (byte)Skeleton.Parents[index],
                    weights = Vector4.UnitX
                }, RhombusVertexCount));
                indices = indices.Concat(RhombusIndices.Select(i => (ushort)(i + index * RhombusVertexCount)));
            }

            var vertexArray = vertices.ToArray();
            var skinVertexArray = skinVertices.ToArray();
            var indexArray = indices.ToArray();
            vertexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)vertexArray.Length * ColoredVertex.Stride, BufferUsage.VertexBuffer));
            skinBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)skinVertexArray.Length * SkinVertex.Stride, BufferUsage.VertexBuffer));
            indexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)indexArray.Length * sizeof(ushort), BufferUsage.IndexBuffer));
            device.UpdateBuffer(vertexBuffer, 0, vertexArray);
            device.UpdateBuffer(skinBuffer, 0, skinVertexArray);
            device.UpdateBuffer(indexBuffer, 0, indexArray);

            var boneDepthsArr = new int[Skeleton.Bones.Count];
            for (int i = 0; i < Skeleton.Bones.Count; i++)
                boneDepthsArr[i] = Skeleton.Parents[i] < 0 ? 0 : boneDepthsArr[Skeleton.Parents[i]] + 1;
            boneDepths = boneDepthsArr;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            vertexBuffer.Dispose();
            skinBuffer.Dispose();
            indexBuffer.Dispose();
            BoneMaterial.Dispose();
            SkinMaterial.Dispose();
            SkinHighlightedMaterial.Dispose();
        }

        public void Render(CommandList cl)
        {
            if (RenderMode == DebugSkeletonRenderMode.Invisible)
                return;

            if (RenderMode == DebugSkeletonRenderMode.Skin)
            {
                (SkinMaterial as IMaterial).Apply(cl);
                Geometry.SetBuffers(cl);
                Geometry.SetSkinBuffer(cl);
                cl.DrawIndexed((uint)Geometry.SubMeshes.Sum(sm => sm.IndexCount));
            }
            if (RenderMode == DebugSkeletonRenderMode.SingleSkinBone)
            {
                (SkinHighlightedMaterial as IMaterial).Apply(cl);
                Geometry.SetBuffers(cl);
                Geometry.SetSkinBuffer(cl);
                cl.DrawIndexed((uint)Geometry.SubMeshes.Sum(sm => sm.IndexCount));
            }

            // always draw bones when visible
            (BoneMaterial as IMaterial).Apply(cl);
            cl.SetVertexBuffer(0, vertexBuffer);
            cl.SetVertexBuffer(1, skinBuffer);
            cl.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(indexBuffer.SizeInBytes / sizeof(ushort));
        }

        private void SetBoneAlpha(int boneI, byte alpha)
        {
            if (boneI < 1)
                return;
            boneI--; // the first bone has no rhombus

            // this is probably a terrible but also very lazy way of doing this
            for (int vertexI = 0; vertexI < RhombusVertexCount; vertexI++)
            {
                var offset =
                    (boneI * RhombusVertexCount + vertexI) * ColoredVertex.Stride +
                    3 * sizeof(float) + 3 * sizeof(byte);
                device.UpdateBuffer(vertexBuffer, (uint)offset, alpha);
            }
        }

        public void HighlightBone(int boneIdx)
        {
            if (highlightedBoneI >= 0)
                SetBoneAlpha(highlightedBoneI, 120);
            SetBoneAlpha(highlightedBoneI = boneIdx, 255);
            SkinHighlightedMaterial.BoneIndex.Ref = highlightedBoneI;
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
}
