using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    [Serializable]
    public abstract class Section
    {
        private static readonly Dictionary<SectionId, Func<Section>> sectionTypeCtors
            = new Dictionary<SectionId, Func<Section>>()
            {
                { SectionId.Atomic, () => new RWAtomic() },
                { SectionId.AtomicSection, () => new RWAtomicSection() },
                { SectionId.BinMeshPLG, () => new RWBinMeshPLG() },
                { SectionId.Clump, () => new RWClump() },
                { SectionId.Extension, () => new RWExtension() },
                { SectionId.FrameList, () => new RWFrameList() },
                { SectionId.Geometry, () => new RWGeometry() },
                { SectionId.GeometryList, () => new RWGeometryList() },
                { SectionId.Material, () => new RWMaterial() },
                { SectionId.MaterialList, () => new RWMaterialList() },
                { SectionId.MorphPLG, () => new RWMorphPLG() },
                { SectionId.PlaneSection, () => new RWPlaneSection() },
                { SectionId.SkinPLG, () => new RWSkinPLG() },
                { SectionId.String, () => new RWString() },
                { SectionId.Struct, () => new RWStruct() },
                { SectionId.Texture, () => new RWTexture() },
                { SectionId.World, () => new RWWorld() },
                { SectionId.Unknown, () => new UnknownSection(SectionId.Unknown) }
            };

        public abstract SectionId sectionId { get; }
        public ListSection parent = null;
        public uint version;

        protected abstract void readBody(Stream stream);
        protected abstract void writeBody(Stream stream);

        public static void readHead(Stream stream, out SectionId sectionId, out UInt32 size, out UInt32 version)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            sectionId = Utils.intToEnum<SectionId>(reader.ReadInt32());
            size = reader.ReadUInt32();
            version = reader.ReadUInt32();
        }

        public static Section createSection(SectionId id)
        {
            if (sectionTypeCtors.ContainsKey(id))
                return sectionTypeCtors[id]();
            else
                return new UnknownSection(id);
        }

        public void read(Stream stream)
        {
            SectionId readSectionId;
            UInt32 readSize, readVersion;
            readHead(stream, out readSectionId, out readSize, out readVersion);

            if (readSectionId != sectionId) {
                String msg = String.Format("Trying to read a \"{0}\" from a \"{1}\"", readSectionId, sectionId);
                throw new InvalidDataException(msg);
            }
            version = readVersion;

            RangeStream rangeStream = new RangeStream(stream, readSize, false, false);
            readBody(rangeStream);
        }

        public void write(Stream stream, ListSection parent = null)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write((Int32)sectionId);
            long sectionSizePos = stream.Position;
            writer.Write((UInt32)0);
            writer.Write(version);

            writeBody(stream);

            long afterBodyPos = stream.Position;
            stream.Seek(sectionSizePos, SeekOrigin.Begin);
            writer.Write(afterBodyPos - sectionSizePos - 8);
            stream.Seek(afterBodyPos, SeekOrigin.Begin);
        }

        public static Section readNew(Stream stream, ListSection parent = null)
        {
            SectionId sectionId;
            UInt32 size, version;
            long oldPosition = stream.Position;
            readHead(stream, out sectionId, out size, out version);
            stream.Seek(oldPosition, SeekOrigin.Begin);
            long afterPosition = oldPosition + 12 + size;

            Section section = createSection(sectionId);
            section.parent = parent;
            section.read(stream);
            return section;
        }

        public ListSection findParentById(SectionId sectionId)
        {
            ListSection current = parent;
            while (current != null)
            {
                if (current.sectionId == sectionId)
                    return current;
                current = current.parent;
            }
            return null;
        }

        public abstract Section findChildById(SectionId sectionId, bool recursive = true);
    }
}
