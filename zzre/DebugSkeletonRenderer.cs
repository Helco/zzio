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

namespace zzre
{
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

        public DebugMaterial Material { get; }
        public Skeleton Skeleton { get; }

        public DebugSkeletonRenderer(ITagContainer diContainer, Skeleton skeleton)
        {
            Skeleton = skeleton;
            Material = new DebugMaterial(diContainer);
            device = diContainer.GetTag<GraphicsDevice>();

            var vertices = Enumerable.Empty<ColoredVertex>();
            var indices = Enumerable.Empty<ushort>();
            foreach (var (toMat, index) in skeleton.InvPose.Indexed())
            {
                if (skeleton.Parents[index] < 0)
                    continue;
                var to = toMat.Translation;
                var from = skeleton.InvPose[skeleton.Parents[index]].Translation;
                var length = (to - from).Length();
                var baseSize = length * RhombusBaseSize;
                var rot = Quaternion.CreateFromRotationMatrix(toMat);
                var normal = Vector3.Normalize(to - from);
                var tangent = Vector3.Normalize(Vector3.Cross(normal, Vector3.Transform(Vector3.UnitY, rot))) * baseSize;
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
                }.Select(p => new ColoredVertex(p, Colors[index % Colors.Length])));
                indices = indices.Concat(RhombusIndices.Select(i => (ushort)(i + index * RhombusVertexCount)));
            }

            var vertexArray = vertices.ToArray();
            var indexArray = indices.ToArray();
            vertexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)vertexArray.Length * ColoredVertex.Stride, BufferUsage.VertexBuffer));
            indexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)indexArray.Length * sizeof(ushort), BufferUsage.IndexBuffer));
            device.UpdateBuffer(vertexBuffer, 0, vertexArray);
            device.UpdateBuffer(indexBuffer, 0, indexArray);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
        }

        public void Render(CommandList cl)
        {
            cl.SetVertexBuffer(0, vertexBuffer);
            cl.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
            (Material as IMaterial).Apply(cl);
            cl.DrawIndexed(indexBuffer.SizeInBytes / sizeof(ushort));
        }

        public void SetBoneAlpha(int boneI, byte alpha)
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
    }
}
