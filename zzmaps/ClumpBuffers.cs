using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.vfs;
using zzre.materials;
using zzre.rendering;

namespace zzre;

[StructLayout(LayoutKind.Sequential)]
public struct ModelStandardVertex
{
    public Vector3 pos;
    public Vector2 tex;
    public IColor color;
    public static uint Stride =
        (3 + 2) * sizeof(float) +
        4 * sizeof(byte);
}

[StructLayout(LayoutKind.Sequential)]
public struct ModelColors
{
    public FColor tint;
    public float vertexColorFactor;
    public float tintFactor;
    public float alphaReference;
    public static uint Stride = (4 + 3) * sizeof(float);

    public static readonly ModelColors Default = new()
    {
        tint = FColor.White,
        vertexColorFactor = 1f,
        tintFactor = 1f,
        alphaReference = 0.6f
    };
}

public class ClumpBuffers : BaseDisposable
{
    public readonly struct SubMesh
    {
        public readonly int IndexOffset, IndexCount;
        public readonly RWMaterial Material;

        public SubMesh(int io, int ic, RWMaterial m) => (IndexOffset, IndexCount, Material) = (io, ic, m);
    }

    private readonly DeviceBuffer vertexBuffer;
    private readonly DeviceBuffer indexBuffer;
    private readonly SubMesh[] subMeshes;

    public int VertexCount => (int)(vertexBuffer.SizeInBytes / ModelStandardVertex.Stride);
    public int TriangleCount => (int)(indexBuffer.SizeInBytes / (sizeof(ushort) * 3));
    public RWSkinPLG? Skin { get; }
    public IReadOnlyList<SubMesh> SubMeshes => subMeshes;
    public Vector3 BSphereCenter { get; }
    public float BSphereRadius { get; }
    public Box Bounds { get; }
    public bool IsSolid { get; }
    public RWGeometry RWGeometry { get; }
    public string Name { get; set; }

    public ClumpBuffers(ITagContainer diContainer, FilePath path)
        : this(diContainer, diContainer.GetTag<IResourcePool>().FindFile(path) ??
        throw new FileNotFoundException($"Could not find model at {path.ToPOSIXString()}"))
    { }
    public ClumpBuffers(ITagContainer diContainer, IResource resource)
        : this(diContainer, resource.OpenAsRWBS<RWClump>(), resource.Name.Replace(".DFF", "", System.StringComparison.InvariantCultureIgnoreCase)) { }

    public ClumpBuffers(ITagContainer diContainer, RWClump clump, string name = "")
    {
        Name = name;
        var device = diContainer.GetTag<GraphicsDevice>();
        var atomic = clump.FindChildById(SectionId.Atomic, recursive: false) as RWAtomic;
        var geometry = clump.FindChildById(SectionId.Geometry, true) as RWGeometry;
        var materialList = geometry?.FindChildById(SectionId.MaterialList, false) as RWMaterialList;
        var materials = materialList?.children.Where(s => s is RWMaterial).Cast<RWMaterial>().ToArray();
        var morphTarget = geometry?.morphTargets[0]; // TODO: morph support for the one model that uses it? 
        if (geometry == null || morphTarget == null || materials == null || atomic == null)
            throw new InvalidDataException("Could not find valid section structure in clump");
        RWGeometry = geometry;
        BSphereCenter = morphTarget.bsphereCenter;
        BSphereRadius = morphTarget.bsphereRadius;
        IsSolid = atomic.flags.HasFlag(AtomicFlags.CollisionTest);

        var vertices = new ModelStandardVertex[morphTarget.vertices.Length];
        var bounds = new Box(morphTarget.vertices.First(), Vector3.Zero);
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].pos = morphTarget.vertices[i];
            vertices[i].color = geometry.colors.Length > 0 ? geometry.colors[i] : new IColor(255);
            vertices[i].tex = geometry.texCoords.Length > 0 ? geometry.texCoords[0][i] : Vector2.Zero;
            bounds = bounds.Union(vertices[i].pos);
        }
        Bounds = bounds;
        vertexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription((uint)vertices.Length * ModelStandardVertex.Stride, BufferUsage.VertexBuffer));
        vertexBuffer.Name = $"Clump {name} Vertices";
        device.UpdateBuffer(vertexBuffer, 0, vertices);

        // TODO: might have to correlate to the materialIndices member of materialList 
        var trianglesByMatIdx = geometry.triangles.GroupBy(t => t.m).Where(g => g.Any());
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
        indexBuffer.Name = $"Clump {name} Indices";
        device.UpdateBuffer(indexBuffer, 0, indices);

        Skin = null; // zzmaps does not need skinned meshes
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

public class ClumpBuffersAssetLoader : IAssetLoader<ClumpBuffers>
{
    public ITagContainer DIContainer { get; }

    public ClumpBuffersAssetLoader(ITagContainer diContainer)
    {
        DIContainer = diContainer;
    }

    public void Clear() { }

    public bool TryLoad(IResource resource, [NotNullWhen(true)] out ClumpBuffers? asset)
    {
        try
        {
            asset = new ClumpBuffers(DIContainer, resource);
            return true;
        }
        catch (Exception)
        {
            asset = null;
            return false;
        }
    }
}

public class CachedClumpBuffersLoader : CachedAssetLoader<ClumpBuffers>
{
    public CachedClumpBuffersLoader(ITagContainer diContainer) : base(new ClumpBuffersAssetLoader(diContainer)) { }
}
