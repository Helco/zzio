using System;
using System.IO;

namespace zzio.rwbs;

[Serializable]
public abstract class StructSection : ListSection
{
    public uint structVersion;

    protected abstract void readStruct(Stream stream);
    protected abstract void writeStruct(Stream stream);

    protected override void readBody(Stream stream)
    {
        SectionId structSectionId;
        uint structSize;
        Section.ReadHead(new GatekeeperStream(stream), out structSectionId, out structSize, out structVersion);
        if (structSectionId != SectionId.Struct)
            throw new InvalidDataException("Struct list section does not contain struct section");

        long oldPosition = stream.Position;
        RangeStream structStream = new(stream, structSize, false, false);
        readStruct(structStream);
        stream.Position = oldPosition + structSize;

        base.readBody(stream);
    }

    protected override void writeBody(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write((int)SectionId.Struct);
        long sectionSizePos = stream.Position;
        writer.Write((uint)0);
        writer.Write(version);

        writeStruct(new GatekeeperStream(stream));

        long afterStructPos = stream.Position;
        stream.Seek(sectionSizePos, SeekOrigin.Begin);
        writer.Write(afterStructPos - sectionSizePos - 8);
        stream.Seek(afterStructPos, SeekOrigin.Begin);

        base.writeBody(stream);
    }
}
