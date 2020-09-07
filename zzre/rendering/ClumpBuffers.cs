using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio.primitives;
using zzio.rwbs;
using zzio.utils;
using zzio.vfs;
using zzre.materials;

namespace zzre
{
    public class ClumpBuffers : BaseDisposable
    {
        public readonly struct SubMesh
        {
            public readonly int IndexOffset, IndexCount;
            public readonly RWMaterial Material;

            public SubMesh(int io, int ic, RWMaterial m) => (IndexOffset, IndexCount, Material) = (io, ic, m);
        }

        private DeviceBuffer vertexBuffer;
        private DeviceBuffer indexBuffer;
        private DeviceBuffer? skinBuffer;
        private SubMesh[] subMeshes;

        public int VertexCount => (int)(vertexBuffer.SizeInBytes / ModelStandardVertex.Stride);
        public int TriangleCount => (int)(indexBuffer.SizeInBytes / (sizeof(ushort) * 3));
        public RWSkinPLG? Skin { get; }
        public IReadOnlyList<SubMesh> SubMeshes => subMeshes;
        public Vector3 BSphereCenter { get; }
        public float BSphereRadius { get; }
        public Box Bounds { get; }

        public ClumpBuffers(ITagContainer diContainer, FilePath path) : this(diContainer, diContainer.GetTag<IResourcePool>().FindFile(path) ??
            throw new FileNotFoundException($"Could not find model at {path.ToPOSIXString()}"))
        { }
        public ClumpBuffers(ITagContainer diContainer, IResource resource) : this(diContainer, resource.OpenAsRWBS<RWClump>()) { }

        public ClumpBuffers(ITagContainer diContainer, RWClump clump)
        {
            var device = diContainer.GetTag<GraphicsDevice>();
            var geometry = clump.FindChildById(SectionId.Geometry, true) as RWGeometry;
            var materialList = geometry?.FindChildById(SectionId.MaterialList, false) as RWMaterialList;
            var materials = materialList?.children.Where(s => s is RWMaterial).Cast<RWMaterial>().ToArray();
            var morphTarget = geometry?.morphTargets[0]; // TODO: morph support for the one model that uses it? 
            if (geometry == null || morphTarget == null || materials == null)
                throw new InvalidDataException("Could not find valid section structure in clump");
            BSphereCenter = morphTarget.bsphereCenter.ToNumerics();
            BSphereRadius = morphTarget.bsphereRadius;

            var vertices = new ModelStandardVertex[morphTarget.vertices.Length];
            var bounds = new Box(morphTarget.vertices.First().ToNumerics(), Vector3.Zero);
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].pos = morphTarget.vertices[i].ToNumerics();
                vertices[i].color = geometry.colors.Length > 0 ? geometry.colors[i] : new IColor(255);
                vertices[i].tex = geometry.texCoords.Length > 0 ? geometry.texCoords[0][i].ToNumerics() : Vector2.Zero;
                bounds = bounds.Union(vertices[i].pos);
            }
            Bounds = bounds;
            vertexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription((uint)vertices.Length * ModelStandardVertex.Stride, BufferUsage.VertexBuffer));
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
            indexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription((uint)indices.Length * 2, BufferUsage.IndexBuffer));
            device.UpdateBuffer(indexBuffer, 0, indices);

            Skin = clump.FindChildById(SectionId.SkinPLG, true) as RWSkinPLG;
            if (Skin != null)
            {
                if (vertices.Length != Skin.vertexWeights.GetLength(0))
                    throw new InvalidDataException("Vertex count in skin is not equal to geometry");
                var skinVertices = new SkinVertex[vertices.Length];
                for (int i = 0; i < skinVertices.Length; i++)
                {
                    skinVertices[i].bone0 = Skin.vertexIndices[i, 0];
                    skinVertices[i].bone1 = Skin.vertexIndices[i, 1];
                    skinVertices[i].bone2 = Skin.vertexIndices[i, 2];
                    skinVertices[i].bone3 = Skin.vertexIndices[i, 3];
                    skinVertices[i].weights.X = Skin.vertexWeights[i, 0];
                    skinVertices[i].weights.Y = Skin.vertexWeights[i, 1];
                    skinVertices[i].weights.Z = Skin.vertexWeights[i, 2];
                    skinVertices[i].weights.W = Skin.vertexWeights[i, 3];
                }
                skinBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription((uint)skinVertices.Length * SkinVertex.Stride, BufferUsage.VertexBuffer));
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
