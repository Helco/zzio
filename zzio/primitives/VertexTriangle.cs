using System;
using System.IO;
using System.Runtime.InteropServices;

namespace zzio;

[StructLayout(LayoutKind.Sequential)]
public struct VertexTriangle
{
    public ushort m, v1, v2, v3;

    public VertexTriangle ShuffledForGeometry => new(v1, m, v3, v2);

    public VertexTriangle(ushort v1, ushort v2, ushort v3, ushort m)
    {
        this.m = m;
        this.v1 = v1;
        this.v2 = v2;
        this.v3 = v3;
    }

    public static VertexTriangle ReadNew(BinaryReader r)
    {
        VertexTriangle t;
        t.m = r.ReadUInt16();
        t.v1 = r.ReadUInt16();
        t.v2 = r.ReadUInt16();
        t.v3 = r.ReadUInt16();
        return t;
    }

    public void Write(BinaryWriter w)
    {
        w.Write(m);
        w.Write(v1);
        w.Write(v2);
        w.Write(v3);
    }
}
