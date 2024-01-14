using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.rwbs;
using zzio.vfs;
using zzre.materials;

namespace zzre.rendering;

public class ClumpMesh : Mesh
{
    public RWGeometry Geometry { get; }
    public RWSkinPLG? Skin { get; }
    public IReadOnlyList<RWMaterial> Materials { get; }
    public Box BoundingBox { get; }
    public Sphere BoundingSphere { get; }
    public bool HasCollisionTest { get; }
    public int TriangleCount => IndexCount / 3;

    public ClumpMesh(ITagContainer diContainer, FilePath path)
        : this(diContainer, diContainer.GetTag<IResourcePool>().FindFile(path) ??
        throw new FileNotFoundException($"Could not find model at {path.ToPOSIXString()}"))
    { }

    public ClumpMesh(ITagContainer diContainer, IResource resource)
        : this(diContainer, resource.OpenAsRWBS<RWClump>(), resource.Name.Replace(".DFF", "", System.StringComparison.InvariantCultureIgnoreCase))
    { }

    public unsafe ClumpMesh(ITagContainer diContainer, RWClump clump, string name)
        : base(diContainer, name)
    {
        var atomic = clump.FindChildById(SectionId.Atomic, recursive: false) as RWAtomic;
        var geometry = clump.FindChildById(SectionId.Geometry, recursive: true) as RWGeometry;
        var materialList = geometry?.FindChildById(SectionId.MaterialList, recursive: false) as RWMaterialList;
        var materials = materialList?.children.OfType<RWMaterial>().ToArray();
        var morphTarget = geometry?.morphTargets[0]; // TODO: morph support for the one model that uses it? 
        Skin = clump.FindChildById(SectionId.SkinPLG, true) as RWSkinPLG;
        if (geometry == null || morphTarget == null || materials == null || atomic == null)
            throw new InvalidDataException("Could not find valid section structure in clump");
        Geometry = geometry;
        Materials = materials;
        BoundingSphere = new(morphTarget.bsphereCenter, morphTarget.bsphereRadius);
        HasCollisionTest = atomic.flags.HasFlag(AtomicFlags.CollisionTest);

        BoundingBox = new(morphTarget.vertices.First(), Vector3.Zero);
        foreach (var vertex in morphTarget.vertices)
            BoundingBox = BoundingBox.Union(vertex);

        Add("Pos", "inPos", morphTarget.vertices);
        // TODO: Add normal attribute to geometry
        if (geometry.texCoords.Any())
            Add("UV", "inUV", geometry.texCoords[0]);
        else
            Add("UV", "inUV", VertexCount, sizeof(Vector2));
        if (geometry.colors.Any())
            Add("Color", "inColor", geometry.colors);
        else
        {
            var attribute = Add("Color", "inColor", VertexCount, sizeof(IColor));
            var colors = Enumerable.Repeat(new IColor(255), VertexCount).ToArray();
            graphicsDevice.UpdateBuffer(attribute.DeviceBuffer, 0, colors);
        }
        if (Skin != null)
        {
            // TODO: Replace multidimensional arrays in RWSkin
            var boneIndices = Add("Bone Indices", "inIndices", VertexCount, sizeof(byte) * 4);
            var boneWeights = Add("Bone Weights", "inWeights", VertexCount, sizeof(Vector4));
            graphicsDevice.UpdateBuffer(boneIndices.DeviceBuffer, 0, ref Skin.vertexIndices[0, 0], (uint)(sizeof(byte) * 4 * VertexCount));
            graphicsDevice.UpdateBuffer(boneWeights.DeviceBuffer, 0, ref Skin.vertexWeights[0, 0], (uint)(sizeof(Vector4) * VertexCount));
        }

        var trianglesByMatIdx = geometry.triangles.GroupBy(t => t.m).Where(g => g.Any());
        var indices = trianglesByMatIdx.SelectMany(
            g => g.SelectMany(t => new[] { t.v1, t.v2, t.v3 })
        ).ToArray();
        SetIndices(indices);
        int nextIndex = 0;
        foreach (var (group, idx) in trianglesByMatIdx.Indexed())
        {
            AddSubMesh(nextIndex, group.Count() * 3, material: group.Key);
            nextIndex += group.Count() * 3;
        }
    }

}
