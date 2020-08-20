using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio.primitives;
using zzio.rwbs;
using zzre.materials;

namespace zzre.rendering
{
    public class RWWorldBuffers : BaseDisposable
    {
        public abstract class BaseSection
        {
            public PlaneSection? Parent { get; set; }
            public abstract bool IsMesh { get; }
            public bool IsPlane => !IsMesh;
        }

        public class MeshSection : BaseSection
        {
            public override bool IsMesh => true;
            public readonly int SubMeshStart, SubMeshCount;

            public MeshSection(int sms, int smc) => (SubMeshStart, SubMeshCount) = (sms, smc);
        }
        
        public class PlaneSection : BaseSection
        {
            public override bool IsMesh => false;
            public readonly BaseSection LeftChild;
            public readonly BaseSection RightChild;
            public readonly float LeftValue, RightValue;

            public PlaneSection(BaseSection lc, BaseSection rc, float lv, float rv)  =>
                (LeftChild, RightChild, LeftValue, RightValue) = (lc, rc, lv, rv);
        }

        public struct SubMesh
        {
            public readonly int IndexOffset, IndexCount;
            public readonly int MaterialIndex;

            public SubMesh(int indexOffset, int indexCount, int materialIndex) =>
                (IndexOffset, IndexCount, MaterialIndex) = (indexOffset, indexCount, materialIndex);
        }

        private DeviceBuffer vertexBuffer;
        private DeviceBuffer indexBuffer;
        private RWMaterial[] materials;
        private BaseSection[] sections;
        private SubMesh[] subMeshes;

        public int VertexCount => (int)(vertexBuffer.SizeInBytes / ModelStandardVertex.Stride);
        public int TriangleCount => (int)(indexBuffer.SizeInBytes / (sizeof(ushort) * 3));
        public IReadOnlyList<RWMaterial> Materials => materials;
        public IReadOnlyList<BaseSection> Sections => sections;
        public IReadOnlyList<SubMesh> SubMeshes => subMeshes;

        public RWWorldBuffers(ITagContainer diContainer, RWWorld world)
        {
            var device = diContainer.GetTag<GraphicsDevice>();
            var materialList = world.FindChildById(SectionId.MaterialList, false) as RWMaterialList;
            materials = materialList?.children.OfType<RWMaterial>().ToArray() ??
                throw new InvalidDataException("RWWorld has no materials");

            var sectionList = new List<BaseSection>();
            var subMeshList = new List<SubMesh>();
            var vertices = Enumerable.Empty<ModelStandardVertex>();
            var indices = Enumerable.Empty<ushort>();
            int vertexCount = 0, indexCount = 0;

            var rootPlane = world.FindChildById(SectionId.PlaneSection, false) as RWPlaneSection;
            var rootAtomic = world.FindChildById(SectionId.AtomicSection, false) as RWAtomicSection;
            if (rootPlane == null && rootAtomic == null)
                throw new InvalidDataException("RWWorld has no geometry");
            else if (rootPlane != null && rootAtomic != null)
                throw new InvalidDataException("RWWorld has both a root plane and a root atomic");
            else if (rootPlane != null)
                LoadPlane(rootPlane);
            else
                LoadAtomic(rootAtomic!);
            
            PlaneSection LoadPlane(RWPlaneSection plane)
            {
                int index = sectionList.Count;
                sectionList.Add(null!);

                BaseSection LoadChild(int skip)
                {
                    var section = plane.children.Skip(skip).FirstOrDefault();
                    if (section == null)
                        throw new InvalidDataException("RWPlaneSection has not enough children");
                    else if (section is RWPlaneSection)
                        return LoadPlane((RWPlaneSection)section);
                    else if (section is RWAtomicSection)
                        return LoadAtomic((RWAtomicSection)section);
                    else
                        throw new InvalidDataException($"Unexpected {section.sectionId} section in RWPlaneSection");
                }

                var result = new PlaneSection(LoadChild(0), LoadChild(1), plane.leftValue, plane.rightValue);
                sectionList[index] = result;
                result.LeftChild.Parent = result;
                result.RightChild.Parent = result;
                return result;
            }

            MeshSection LoadAtomic(RWAtomicSection atomic)
            {
                var trianglesByMaterial = atomic.triangles
                    .GroupBy(t => t.m)
                    .Where(g => g.Any())
                    .ToImmutableArray();
                var subMeshCount = trianglesByMaterial.Count();
                var subMeshStart = subMeshList.Count;
                subMeshList.AddRange(trianglesByMaterial.Select(group => new SubMesh(
                    indexOffset: indexCount,
                    indexCount: group.Count() * 3,
                    materialIndex: (int)(group.Key + atomic.matIdBase)
                )));

                vertices = vertices.Concat(Enumerable
                    .Range(0, atomic.vertices.Count())
                    .Select(i => new ModelStandardVertex()
                    {
                        pos = atomic.vertices[i].ToNumerics(),
                        tex = atomic.texCoords1[i].ToNumerics(),
                        color = atomic.colors[i]
                    }));
                vertexCount += atomic.vertices.Count();

                indices = indices.Concat(trianglesByMaterial.SelectMany(
                    group => group.SelectMany(t => new[] { t.v1, t.v2, t.v3 }.Select(i => (ushort)(i + indexCount)))
                ));
                indexCount += trianglesByMaterial.Sum(group => group.Count() * 3);

                var result = new MeshSection(subMeshStart, subMeshCount);
                sectionList.Add(result);
                return result;
            }

            var vertexArray = vertices.ToImmutableArray();
            vertexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)(vertexArray.Count() * ModelStandardVertex.Stride), BufferUsage.VertexBuffer));
            device.UpdateBuffer(vertexBuffer, 0, vertexArray);
            var indexArray = indices.ToImmutableArray();
            indexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)(indexArray.Count() * sizeof(ushort)), BufferUsage.IndexBuffer));
            device.UpdateBuffer(indexBuffer, 0, indexArray);
            sections = sectionList.ToArray();
            subMeshes = subMeshList.ToArray();
        }
    }
}
