using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace zzio.rwbs;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe struct Frame
{
    public Vector3 rotMatrix0, rotMatrix1, rotMatrix2;
    public Vector3 position;
    public uint frameIndex; //propably previous sibling?
    public uint creationFlags;

    public const int ExpectedSize = 4 * 3 * 4 + 4 + 4;
}

[Serializable]
public class RWFrameList : StructSection
{
    public override SectionId sectionId => SectionId.FrameList;

    public Frame[] frames = [];

    protected override void readStruct(Stream stream)
    {
        using BinaryReader reader = new(stream);
        frames = new Frame[reader.ReadUInt32()];
        reader.ReadStructureArray(frames, Frame.ExpectedSize);
    }

    protected override void writeStruct(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(frames.Length);
        writer.WriteStructureArray(frames, Frame.ExpectedSize);
    }
}