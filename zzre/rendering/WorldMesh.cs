using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.vfs;
using zzre.materials;

namespace zzre.rendering;

public class WorldMesh : StaticMesh
{
    public abstract class BaseSection
    {
        public PlaneSection? Parent { get; set; }
        public Box Bounds { get; init; }
    }

    public class MeshSection : BaseSection
    {
        public int SubMeshSection { get; init; }
        public int VertexCount { get; init; }
        public int TriangleCount { get; init; }
        public required RWAtomicSection AtomicSection { get; init; }
    }

    public class PlaneSection : BaseSection
    {
        public required BaseSection LeftChild { get; init; }
        public required BaseSection RightChild { get; init; }
        public float CenterValue { get; init; }
        public float LeftValue { get; init; }
        public float RightValue { get; init; }
        public RWPlaneSectionType PlaneType { get; init; }
    }

    public RWWorld World { get; }
    public IReadOnlyList<RWMaterial> Materials { get; }
    public IReadOnlyList<BaseSection> Sections { get; }
    public Vector3 Origin { get; }
    public int TriangleCount => IndexCount / 3;

    public WorldMesh(ITagContainer diContainer, FilePath path) : this(diContainer, diContainer.GetTag<IResourcePool>().FindFile(path) ??
        throw new FileNotFoundException($"Could not find world at {path.ToPOSIXString()}"))
    { }

    public WorldMesh(ITagContainer diContainer, IResource resource)
        : this(diContainer, resource.OpenAsRWBS<RWWorld>(), resource.Name.Replace(".BSP", "", StringComparison.InvariantCultureIgnoreCase)) { }

    public unsafe WorldMesh(ITagContainer diContainer, RWWorld world, string name)
        : base(diContainer, name)
    {
        var device = diContainer.GetTag<GraphicsDevice>();
        var materialList = world.FindChildById(SectionId.MaterialList, false) as RWMaterialList;
        Materials = materialList?.children.OfType<RWMaterial>().ToArray() ??
            throw new InvalidDataException("RWWorld has no materials");
        Origin = world.origin;
        World = world;

        var sectionList = new List<BaseSection>();
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
        Sections = sectionList;

        PlaneSection LoadPlane(RWPlaneSection plane)
        {
            int index = sectionList.Count;
            sectionList.Add(null!);

            BaseSection LoadChild(int skip)
            {
                var section = plane.children.Skip(skip).FirstOrDefault();
                return section switch
                {
                    RWPlaneSection planeSection => LoadPlane(planeSection),
                    RWAtomicSection atomicSection => LoadAtomic(atomicSection),
                    null => throw new InvalidDataException("RWPlaneSection has not enough children"),
                    _ => throw new InvalidDataException($"Unexpected {section.sectionId} section in RWPlaneSection")
                };
            }

            var leftChild = LoadChild(0);
            var rightChild = LoadChild(1);
            var bounds = Box.Union(leftChild.Bounds, rightChild.Bounds);
            var result = new PlaneSection
            {
                LeftChild = leftChild,
                RightChild = rightChild,
                Bounds = bounds,
                CenterValue = plane.centerValue,
                LeftValue = plane.leftValue,
                RightValue = plane.rightValue,
                PlaneType = plane.sectorType
            };
            sectionList[index] = result;
            leftChild.Parent = result;
            rightChild.Parent = result;
            return result;
        }

        MeshSection LoadAtomic(RWAtomicSection atomic)
        {
            var result = new MeshSection()
            {
                AtomicSection = atomic,
                Bounds = Box.FromMinMax(atomic.bbox1, atomic.bbox2),
                VertexCount = atomic.vertices.Length,
                TriangleCount = atomic.triangles.Length,
                SubMeshSection = sectionList.Count,
            };
            vertexCount += result.VertexCount;
            indexCount += result.TriangleCount * 3;
            sectionList.Add(result);
            return result;
        }

        var posBuffer = Add("Pos", "inPos", vertexCount, sizeof(Vector3)).DeviceBuffer;
        var uvBuffer = Add("UV", "inUV", vertexCount, sizeof(Vector2)).DeviceBuffer;
        var colorBuffer = Add("Color", "inColor", vertexCount, sizeof(IColor)).DeviceBuffer;
        var indexBuffer = SetIndexCount(indexCount, IndexFormat.UInt16);
        var indices = new ushort[indexCount];
        uint vertexOffset = 0u;
        int indexOffset = 0;
        foreach (var meshSection in sectionList.OfType<MeshSection>())
        {
            var atomic = meshSection.AtomicSection;
            device.UpdateBuffer(posBuffer, vertexOffset * (uint)sizeof(Vector3), atomic.vertices);
            device.UpdateBuffer(uvBuffer, vertexOffset * (uint)sizeof(Vector2), atomic.texCoords1);
            device.UpdateBuffer(colorBuffer, vertexOffset * (uint)sizeof(IColor), atomic.colors);

            var trianglesByMaterial = atomic.triangles.GroupBy(t => t.m);
            foreach (var group in trianglesByMaterial)
            {
                AddSubMesh(
                    indexOffset: indexOffset,
                    indexCount: group.Count() * 3,
                    material: group.Key + (int)atomic.matIdBase,
                    section: meshSection.SubMeshSection);
                foreach (var triangle in group)
                {
                    checked
                    {
                        indices[indexOffset++] = (ushort)(triangle.v1 + vertexOffset);
                        indices[indexOffset++] = (ushort)(triangle.v2 + vertexOffset);
                        indices[indexOffset++] = (ushort)(triangle.v3 + vertexOffset);
                    }
                }
            }

            vertexOffset += (uint)meshSection.VertexCount;
        }
        device.UpdateBuffer(indexBuffer, 0u, indices);
    }
}
