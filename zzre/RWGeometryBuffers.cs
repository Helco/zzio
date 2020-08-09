using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio.primitives;
using zzio.rwbs;
using zzre.core;
using zzre.materials;

namespace zzre
{
    public class RWGeometryBuffers : BaseDisposable
    {
        public readonly struct SubMesh
        {
            public readonly int IndexOffset, IndexCount;
            public readonly RWMaterial Material;

            public SubMesh(int io, int ic, RWMaterial m) => (IndexOffset, IndexCount, Material) = (io, ic, m);
        }

        private readonly GraphicsDevice device;
        private ResourceFactory Factory => device.ResourceFactory;
        private DeviceBuffer vertexBuffer;
        private DeviceBuffer indexBuffer;
        private DeviceBuffer? skinBuffer;
        private SubMesh[] subMeshes;

        public int VertexCount => (int)(vertexBuffer.SizeInBytes / 4);
        public int TriangleCount => (int)(indexBuffer.SizeInBytes / (2 * 3));
        public bool HasSkin => skinBuffer != null;
        public IReadOnlyList<SubMesh> SubMeshes => subMeshes;

        public RWGeometryBuffers(ITagContainer diContainer, RWClump clump)
        {
            device = diContainer.GetTag<GraphicsDevice>();
            var geometry = (RWGeometry)clump.FindChildById(SectionId.Geometry, true);
            var materialList = (RWMaterialList)geometry.FindChildById(SectionId.MaterialList, false);
            var materials = materialList.children.Where(s => s is RWMaterial).Cast<RWMaterial>().ToArray();
            var morphTarget = geometry.morphTargets[0]; // TODO: morph support for the one model that uses it?

            var vertices = new ModelStandardVertex[morphTarget.vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].pos = morphTarget.vertices[i].ToNumerics();
                vertices[i].color = geometry.colors.Length > 0 ? geometry.colors[i] : new IColor(255);
                vertices[i].tex = geometry.texCoords.Length > 0 ? geometry.texCoords[0][i].ToNumerics() : Vector2.Zero;
            }
            vertexBuffer = Factory.CreateBuffer(new BufferDescription((uint)vertices.Length * ModelStandardVertex.Stride, BufferUsage.VertexBuffer));
            device.UpdateBuffer(vertexBuffer, 0, vertices);

            // TODO: might have to correlate to the materialIndices member of materialList
            var trianglesByMatIdx = geometry.triangles.GroupBy(t => t.m).Where(g => g.Count() > 0);
            var indices = trianglesByMatIdx.SelectMany(
                g => g.SelectMany(t => new[] { t.v1, t.v2, t.v3 })
            ).ToArray();
            subMeshes = new SubMesh[trianglesByMatIdx.Count()];
            int nextIndexPtr = 0;
            foreach (var (group, idx) in trianglesByMatIdx.Indexed())
            {
                subMeshes[idx] = new SubMesh(nextIndexPtr, group.Count() * 3, materials[group.Key]);
                nextIndexPtr += subMeshes[idx].IndexCount;
            }
            indexBuffer = Factory.CreateBuffer(new BufferDescription((uint)indices.Length * 2, BufferUsage.IndexBuffer));
            device.UpdateBuffer(indexBuffer, 0, indices);

            var skin = (RWSkinPLG)clump.FindChildById(SectionId.SkinPLG, true);
            if (skin != null)
            {
                if (vertices.Length != skin.vertexWeights.GetLength(0))
                    throw new InvalidDataException("Vertex count in skin is not equal to geometry");
                var skinVertices = new SkinVertex[vertices.Length];
                for (int i = 0; i < skinVertices.Length; i++)
                {
                    skinVertices[i].bone0 = skin.vertexIndices[i, 0];
                    skinVertices[i].bone1 = skin.vertexIndices[i, 1];
                    skinVertices[i].bone2 = skin.vertexIndices[i, 2];
                    skinVertices[i].bone3 = skin.vertexIndices[i, 3];
                    skinVertices[i].weights.X = skin.vertexWeights[i, 0];
                    skinVertices[i].weights.Y = skin.vertexWeights[i, 1];
                    skinVertices[i].weights.Z = skin.vertexWeights[i, 2];
                    skinVertices[i].weights.W = skin.vertexWeights[i, 3];
                }
                skinBuffer = Factory.CreateBuffer(new BufferDescription((uint)skinVertices.Length * SkinVertex.Stride, BufferUsage.VertexBuffer));
                device.UpdateBuffer(skinBuffer, 0, skinVertices);
            }
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
            skinBuffer?.Dispose();
        }

        public void SetBuffers(CommandList commandList)
        {
            commandList.SetVertexBuffer(0, vertexBuffer);
            commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
        }

        public void SetSkinBuffer(CommandList commandList)
        {
            if (skinBuffer != null)
                commandList.SetVertexBuffer(1, skinBuffer);
        }
    }
}
