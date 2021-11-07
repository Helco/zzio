using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio.rwbs;
using zzio.utils;
using zzio.vfs;
using zzre.materials;

namespace zzre.rendering
{
    public class WorldBuffers : BaseDisposable
    {
        public abstract record BaseSection
        {
            public PlaneSection? Parent { get; set; }
            public abstract bool IsMesh { get; }
            public bool IsPlane => !IsMesh;
            public Box Bounds { get; init; }
        }

        public record MeshSection : BaseSection
        {
            public override bool IsMesh => true;
            public Range SubMeshes { get; init; }
            public int VertexCount { get; init; }
            public int TriangleCount { get; init; }
            public RWAtomicSection RWAtomicSection { get; }

            public MeshSection(RWAtomicSection rwAtomicSection) => RWAtomicSection = rwAtomicSection;
        }
        
        public record PlaneSection : BaseSection
        {
            public override bool IsMesh => false;
            public BaseSection LeftChild { get; set; }
            public BaseSection RightChild { get; set; }
            public float CenterValue { get; init; }
            public float LeftValue { get; init; }
            public float RightValue { get; init; }
            public RWPlaneSectionType PlaneType { get; init; }

            public PlaneSection(BaseSection lc, BaseSection rc) => (LeftChild, RightChild) = (lc, rc);
        }

        public readonly struct SubMesh
        {
            public readonly int IndexOffset, IndexCount;
            public readonly int MaterialIndex;

            public SubMesh(int indexOffset, int indexCount, int materialIndex) =>
                (IndexOffset, IndexCount, MaterialIndex) = (indexOffset, indexCount, materialIndex);
        }

        private readonly DeviceBuffer vertexBuffer;
        private readonly DeviceBuffer indexBuffer;
        private readonly ImmutableArray<RWMaterial> materials;
        private readonly ImmutableArray<BaseSection> sections;
        private readonly ImmutableArray<SubMesh> subMeshes;

        public int VertexCount => (int)(vertexBuffer.SizeInBytes / ModelStandardVertex.Stride);
        public int TriangleCount => (int)(indexBuffer.SizeInBytes / (sizeof(ushort) * 3));
        public IReadOnlyList<RWMaterial> Materials => materials;
        public IReadOnlyList<BaseSection> Sections => sections;
        public IReadOnlyList<SubMesh> SubMeshes => subMeshes;
        public Vector3 Origin { get; }
        public RWWorld RWWorld { get; }

        public WorldBuffers(ITagContainer diContainer, FilePath path) : this(diContainer, diContainer.GetTag<IResourcePool>().FindFile(path) ??
            throw new FileNotFoundException($"Could not find world at {path.ToPOSIXString()}"))
        { }
        public WorldBuffers(ITagContainer diContainer, IResource resource) : this(diContainer, resource.OpenAsRWBS<RWWorld>()) { }

        public WorldBuffers(ITagContainer diContainer, RWWorld world)
        {
            var device = diContainer.GetTag<GraphicsDevice>();
            var materialList = world.FindChildById(SectionId.MaterialList, false) as RWMaterialList;
            materials = materialList?.children.OfType<RWMaterial>().ToImmutableArray() ??
                throw new InvalidDataException("RWWorld has no materials");
            Origin = world.origin.ToNumerics();
            RWWorld = world;

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

                var leftChild = LoadChild(0);
                var rightChild = LoadChild(1);
                var bounds = Box.Union(leftChild.Bounds, rightChild.Bounds);
                var result = new PlaneSection(leftChild, rightChild)
                {
                    Bounds = bounds,
                    CenterValue = plane.centerValue,
                    LeftValue = plane.leftValue,
                    RightValue = plane.rightValue,
                    PlaneType = plane.sectorType
                };
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
                var subMeshCount = trianglesByMaterial.Length;
                var subMeshStart = subMeshList.Count;
                int offset = 0;
                foreach (var group in trianglesByMaterial)
                {
                    subMeshList.Add(new SubMesh(
                        indexOffset: indexCount + offset,
                        indexCount: group.Count() * 3,
                        materialIndex: (int)(group.Key + atomic.matIdBase)));
                    offset += group.Count() * 3;
                }

                vertices = vertices.Concat(Enumerable
                    .Range(0, atomic.vertices.Count())
                    .Select(i => new ModelStandardVertex()
                    {
                        pos = atomic.vertices[i].ToNumerics(),
                        tex = atomic.texCoords1[i].ToNumerics(),
                        color = atomic.colors[i]
                    }));
                int vertexBase = vertexCount;
                vertexCount += atomic.vertices.Count();

                indices = indices.Concat(trianglesByMaterial.SelectMany(
                    group => group.SelectMany(t => new[] { t.v1, t.v2, t.v3 }.Select(i => (ushort)(i + vertexBase)))
                ));
                int triangleCount = trianglesByMaterial.Sum(group => group.Count());
                indexCount += triangleCount * 3;

                var bounds = Box.FromMinMax(atomic.bbox1.ToNumerics(), atomic.bbox2.ToNumerics());
                var result = new MeshSection(atomic)
                {
                    Bounds = bounds,
                    SubMeshes = subMeshStart..(subMeshStart + subMeshCount),
                    VertexCount = atomic.vertices.Count(),
                    TriangleCount = triangleCount
                };
                sectionList.Add(result);
                return result;
            }

            var vertexArray = vertices.ToArray();
            vertexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)(vertexArray.Count() * ModelStandardVertex.Stride), BufferUsage.VertexBuffer));
            device.UpdateBuffer(vertexBuffer, 0, vertexArray);
            var indexArray = indices.ToArray();
            indexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)(indexArray.Count() * sizeof(ushort)), BufferUsage.IndexBuffer));
            device.UpdateBuffer(indexBuffer, 0, indexArray);
            sections = sectionList.ToImmutableArray();
            subMeshes = subMeshList.ToImmutableArray();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
        }

        public void SetBuffers(CommandList commandList)
        {
            commandList.SetVertexBuffer(0, vertexBuffer);
            commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
        }
    }
}
