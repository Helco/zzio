using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;
using zzio.primitives;

namespace zzio.rwbs
{
    [Flags]
    public enum BoneFlags
    {
        // the naming might be weird as it was intended
        // by RenderWare to represent matrix operations
        IsChildless = (1 << 0),
        HasNextSibling = (1 << 1),

        UnknownFlag3 = (1 << 3)
    }

    [System.Serializable]
    public struct Bone
    {
        public UInt32 id, idx;
        public BoneFlags flags;
        public Vector right, up, at, pos;
        public UInt32 p1, p2, p3, p4;
    }

    [Serializable]
    public class RWSkinPLG : Section
    {
        public override SectionId sectionId => SectionId.SkinPLG;

        public byte[,] vertexIndices = new byte[0,0]; // 4 per vertex
        public float[,] vertexWeights = new float[0,0]; // 4 per vertex
        public Bone[] bones = new Bone[0];

        protected override void readBody(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            bones = new Bone[reader.ReadUInt32()];
            UInt32 vertexCount = reader.ReadUInt32();
            vertexIndices = new byte[vertexCount, 4];
            vertexWeights = new float[vertexCount, 4];

            for (int i = 0; i < vertexCount; i++)
            {
                for (int j = 0; j < 4; j++)
                    vertexIndices[i, j] = reader.ReadByte();
            }

            for (int i = 0; i < vertexCount; i++)
            {
                for (int j = 0; j < 4; j++)
                    vertexWeights[i, j] = reader.ReadSingle();
            }

            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].id = reader.ReadUInt32();
                bones[i].idx = reader.ReadUInt32();
                bones[i].flags = EnumUtils.intToFlags<BoneFlags>(reader.ReadUInt32());

                bones[i].right = Vector.ReadNew(reader);
                bones[i].p1 = reader.ReadUInt32();
                bones[i].up = Vector.ReadNew(reader);
                bones[i].p2 = reader.ReadUInt32();
                bones[i].at = Vector.ReadNew(reader);
                bones[i].p3 = reader.ReadUInt32();
                bones[i].pos = Vector.ReadNew(reader);
                bones[i].p4 = reader.ReadUInt32();
            }
        }

        protected override void writeBody(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(bones.Length);
            int vertexCount = vertexIndices.GetLength(0);
            writer.Write(vertexCount);

            for (int i = 0; i < vertexCount; i++)
            {
                for (int j = 0; j < 4; j++)
                    writer.Write(vertexIndices[i, j]);
            }

            for (int i = 0; i < vertexCount; i++)
            {
                for (int j = 0; j < 4; j++)
                    writer.Write(vertexWeights[i, j]);
            }

            foreach (Bone b in bones)
            {
                writer.Write(b.id);
                writer.Write(b.idx);
                writer.Write((uint)b.flags);

                b.right.Write(writer);
                writer.Write(b.p1);
                b.up.Write(writer);
                writer.Write(b.p2);
                b.at.Write(writer);
                writer.Write(b.p3);
                b.pos.Write(writer);
                writer.Write(b.p4);
            }
        }

        public override Section FindChildById(SectionId sectionId, bool recursive)
        {
            return null;
        }
    }
}