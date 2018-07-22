using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    public enum BinMeshType
    {
        TriList = 0,
        TriStrip = 1,

        Unknown = -1
    }

    [System.Serializable]
    public struct SubMesh
    {
        public UInt32 matIndex;
        public UInt32[] indices; // as ZanZarah uses DirectX, this should always be 32 bit 
    }

    [Serializable]
    public class RWBinMeshPLG : Section
    {
        public override SectionId sectionId => SectionId.BinMeshPLG;

        public BinMeshType type;
        public UInt32 totalIndexCount;
        public SubMesh[] subMeshes;

        protected override void readBody(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            type = EnumUtils.intToEnum<BinMeshType>(reader.ReadInt32());
            subMeshes = new SubMesh[reader.ReadUInt32()];
            totalIndexCount = reader.ReadUInt32();

            for (int i = 0; i < subMeshes.Length; i++)
            {
                subMeshes[i].indices = new UInt32[reader.ReadUInt32()];
                subMeshes[i].matIndex = reader.ReadUInt32();

                for (int j = 0; j < subMeshes[i].indices.Length; j++)
                    subMeshes[i].indices[j] = reader.ReadUInt32();
            }
        }

        protected override void writeBody(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((int)type);
            writer.Write(subMeshes.Length);
            writer.Write(totalIndexCount);

            foreach (SubMesh m in subMeshes)
            {
                writer.Write(m.indices.Length);
                writer.Write(m.matIndex);

                foreach (UInt32 i in m.indices)
                    writer.Write(i);
            }
        }

        public override Section FindChildById(SectionId sectionId, bool recursive)
        {
            return null;
        }
    }
}
