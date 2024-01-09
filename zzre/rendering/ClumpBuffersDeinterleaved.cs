using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.vfs;
using zzre.materials;

namespace zzre;

public class ClumpBuffersDeinterleaved : BaseDisposable
{
    public readonly struct SubMesh
    {
        public readonly int IndexOffset, IndexCount;
        public readonly RWMaterial Material;

        public SubMesh(int io, int ic, RWMaterial m) => (IndexOffset, IndexCount, Material) = (io, ic, m);
    }

    private readonly DeviceBuffer posBuffer;
    private readonly DeviceBuffer normalBuffer;
    private readonly DeviceBuffer uvBuffer;
    private readonly DeviceBuffer colorBuffer;
    private readonly DeviceBuffer? boneWeightBuffer;
    private readonly DeviceBuffer? boneIndexBuffer;
    private readonly DeviceBuffer indexBuffer;
    private readonly SubMesh[] subMeshes;

    public int VertexCount => (int)(posBuffer.SizeInBytes / (sizeof(float) * 3));
    public int TriangleCount => (int)(indexBuffer.SizeInBytes / (sizeof(ushort) * 3));
    public RWSkinPLG? Skin { get; }
    public IReadOnlyList<SubMesh> SubMeshes => subMeshes;
    public Vector3 BSphereCenter { get; }
    public float BSphereRadius { get; }
    public Box Bounds { get; }
    public bool IsSolid { get; }
    public RWGeometry RWGeometry { get; }
    public string Name { get; set; }

    public ClumpBuffersDeinterleaved(ITagContainer diContainer, FilePath path)
        : this(diContainer, diContainer.GetTag<IResourcePool>().FindFile(path) ??
        throw new FileNotFoundException($"Could not find model at {path.ToPOSIXString()}"))
    { }
    public ClumpBuffersDeinterleaved(ITagContainer diContainer, IResource resource)
        : this(diContainer, resource.OpenAsRWBS<RWClump>(), resource.Name.Replace(".DFF", "", System.StringComparison.InvariantCultureIgnoreCase)) { }


    private unsafe DeviceBuffer BufferFromArray<T>(GraphicsDevice device, string attrName, T[]? array, int length = -1, T defValue = default) where T : unmanaged
    {
        if (array?.Length == 0)
            array = null;
        if (array == null && length < 0)
            throw new System.ArgumentException();
        if (array != null)
            length = array.Length;
        var buffer = device.ResourceFactory.CreateBuffer(new((uint)(sizeof(T) * length), BufferUsage.VertexBuffer));
        buffer.Name = $"Clump {Name} {attrName}";
        device.UpdateBuffer(buffer, 0, array ?? Enumerable.Repeat(defValue, length).ToArray());
        return buffer;
    }

    public ClumpBuffersDeinterleaved(ITagContainer diContainer, RWClump clump, string name = "")
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

        var vertexCount = morphTarget.vertices.Length;
        posBuffer = BufferFromArray(device, "Pos", morphTarget.vertices);
        normalBuffer = BufferFromArray<Vector3>(device, "Normal", null, vertexCount);
        uvBuffer = BufferFromArray(device, "UV", geometry.texCoords.Length > 0 ? geometry.texCoords[0] : null, vertexCount);
        colorBuffer = BufferFromArray(device, "Color", geometry.colors, vertexCount, new IColor(255));
        
        var vertices = new ModelStandardVertex[morphTarget.vertices.Length];
        var bounds = new Box(morphTarget.vertices.First(), Vector3.Zero);
        for (int i = 0; i < vertices.Length; i++)
            bounds = bounds.Union(vertices[i].pos);
        Bounds = bounds;

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

        Skin = clump.FindChildById(SectionId.SkinPLG, true) as RWSkinPLG;
        if (Skin != null)
        {
            if (vertices.Length != Skin.vertexWeights.GetLength(0))
                throw new InvalidDataException("Vertex count in skin is not equal to geometry");
            /*var skinVertices = new SkinVertex[vertices.Length];
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
            skinBuffer.Name = $"Clump {name} Skin";
            device.UpdateBuffer(skinBuffer, 0, skinVertices);*/
        }
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        posBuffer.Dispose();
        normalBuffer.Dispose();
        uvBuffer.Dispose();
        colorBuffer.Dispose();
        boneWeightBuffer?.Dispose();
        boneIndexBuffer?.Dispose();
        indexBuffer.Dispose();
    }

    public void SetBuffers(CommandList commandList)
    {
        commandList.SetVertexBuffer(0, posBuffer);
        commandList.SetVertexBuffer(1, normalBuffer);
        commandList.SetVertexBuffer(2, uvBuffer);
        commandList.SetVertexBuffer(3, colorBuffer);
        commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
    }

    public void SetSkinBuffer(CommandList commandList)
    {
    }
}
