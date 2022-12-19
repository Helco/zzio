using System;
using System.IO;
using System.Numerics;

namespace zzio.rwbs
{
    [Serializable]
    public unsafe struct Frame
    {
        public float[] rotMatrix;
        public Vector3 position;
        public uint frameIndex; //propably previous sibling?
        public uint creationFlags;

        public static Frame ReadNew(BinaryReader reader)
        {
            Frame f;
            f.rotMatrix = new float[9];
            for (int i = 0; i < 9; i++)
                f.rotMatrix[i] = reader.ReadSingle();
            f.position = reader.ReadVector3();
            f.frameIndex = reader.ReadUInt32();
            f.creationFlags = reader.ReadUInt32();
            return f;
        }

        public void Write(BinaryWriter w)
        {
            for (int i = 0; i < 9; i++)
                w.Write(rotMatrix[i]);
            w.Write(position);
            w.Write(frameIndex);
            w.Write(creationFlags);
        }
    }

    [Serializable]
    public class RWFrameList : StructSection
    {
        public override SectionId sectionId => SectionId.FrameList;

        public Frame[] frames = Array.Empty<Frame>();

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new(stream);
            frames = new Frame[reader.ReadUInt32()];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Frame.ReadNew(reader);
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new(stream);
            writer.Write(frames.Length);
            foreach (Frame f in frames)
                f.Write(writer);
        }
    }
}