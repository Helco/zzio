using System;
using System.IO;
using System.Numerics;
using zzio;

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
        public Matrix4x4 objectToBone;
    }

    [Serializable]
    public class RWSkinPLG : Section
    {
        public override SectionId sectionId => SectionId.SkinPLG;

        public byte[,] vertexIndices = new byte[0, 0]; // 4 per vertex
        public float[,] vertexWeights = new float[0, 0]; // 4 per vertex
        public Bone[] bones = new Bone[0];

        protected override void readBody(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
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
                bones[i].objectToBone = reader.ReadMatrix4x4();
            }
        }

        protected override void writeBody(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
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
                writer.Write(b.objectToBone);
            }
        }
    }
}