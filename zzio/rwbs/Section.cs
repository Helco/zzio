using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace zzio.rwbs;

[Serializable]
public abstract class Section
{
    private static readonly Dictionary<SectionId, Func<Section>> sectionTypeCtors
        = new()
        {
            { SectionId.Atomic,        () => new RWAtomic() },
            { SectionId.AtomicSection, () => new RWAtomicSection() },
            { SectionId.BinMeshPLG,    () => new RWBinMeshPLG() },
            { SectionId.Clump,         () => new RWClump() },
            { SectionId.Extension,     () => new RWExtension() },
            { SectionId.FrameList,     () => new RWFrameList() },
            { SectionId.Geometry,      () => new RWGeometry() },
            { SectionId.GeometryList,  () => new RWGeometryList() },
            { SectionId.Material,      () => new RWMaterial() },
            { SectionId.MaterialList,  () => new RWMaterialList() },
            { SectionId.MorphPLG,      () => new RWMorphPLG() },
            { SectionId.PlaneSection,  () => new RWPlaneSection() },
            { SectionId.SkinPLG,       () => new RWSkinPLG() },
            { SectionId.String,        () => new RWString() },
            { SectionId.Struct,        () => new RWStruct() },
            { SectionId.Texture,       () => new RWTexture() },
            { SectionId.World,         () => new RWWorld() },
            { SectionId.CollisionPLG,  () => new RWCollision() },
            { SectionId.Unknown,       () => new UnknownSection() }
        };

    public abstract SectionId sectionId { get; }
    public ListSection? parent;
    public uint version;

    protected abstract void readBody(Stream stream);
    protected abstract void writeBody(Stream stream);

    public static void ReadHead(Stream stream, out SectionId sectionId, out uint size, out uint version)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
        sectionId = EnumUtils.intToEnum<SectionId>(reader.ReadInt32());
        size = reader.ReadUInt32();
        version = reader.ReadUInt32();
    }

    public static Section CreateSection(SectionId id) => sectionTypeCtors.TryGetValue(id, out var sectionCtor)
        ? sectionCtor()
        : new UnknownSection(id);

    public void Read(Stream stream)
    {
        SectionId readSectionId;
        uint readSize, readVersion;
        ReadHead(stream, out readSectionId, out readSize, out readVersion);

        if (readSectionId != sectionId)
        {
            string msg = string.Format("Trying to read a \"{0}\" from a \"{1}\"", readSectionId, sectionId);
            throw new InvalidDataException(msg);
        }
        version = readVersion;

        var expectedEndPosition = stream.Position + readSize;
        RangeStream rangeStream = new(stream, readSize, false, false);
        readBody(rangeStream);
        if (stream.Position > expectedEndPosition)
            throw new InvalidDataException("Did not read section correctly");
        stream.Position = expectedEndPosition;
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((int)sectionId);
        long sectionSizePos = stream.Position;
        writer.Write((uint)0);
        writer.Write(version);

        writeBody(new GatekeeperStream(stream));

        long afterBodyPos = stream.Position;
        stream.Seek(sectionSizePos, SeekOrigin.Begin);
        writer.Write(afterBodyPos - sectionSizePos - 8);
        stream.Seek(afterBodyPos, SeekOrigin.Begin);
    }

    public static Section ReadNew(Stream stream, ListSection? parent = null)
    {
        long oldPosition = stream.Position;
        ReadHead(stream, out var sectionId, out var size, out _);
        stream.Seek(oldPosition, SeekOrigin.Begin);

        Section section = CreateSection(sectionId);
        section.parent = parent;
        section.Read(stream);
        return section;
    }

    public ListSection? FindParentById(SectionId sectionId)
    {
        var current = parent;
        while (current != null)
        {
            if (current.sectionId == sectionId)
                return current;
            current = current.parent;
        }
        return null;
    }

    public virtual Section? FindChildById(SectionId sectionId, bool recursive = true) => null;
    public virtual IEnumerable<Section> FindAllChildrenById(SectionId sectionId, bool recursive = true) => [];
}
