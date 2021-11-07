using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;
using zzio.primitives;

namespace zzio.rwbs
{
    [Serializable]
    public struct Frame
    {
        public float[] rotMatrix;
        public Vector position;
        public UInt32 frameIndex; //propably previous sibling?
        public UInt32 creationFlags;

        public static Frame ReadNew(BinaryReader reader)
        {
            Frame f;
            f.rotMatrix = new float[9];
            for (int i = 0; i < 9; i++)
                f.rotMatrix[i] = reader.ReadSingle();
            f.position = Vector.ReadNew(reader);
            f.frameIndex = reader.ReadUInt32();
            f.creationFlags = reader.ReadUInt32();
            return f;
        }

        public void Write(BinaryWriter w)
        {
            for (int i = 0; i < 9; i++)
                w.Write(rotMatrix[i]);
            position.Write(w);
            w.Write(frameIndex);
            w.Write(creationFlags);
        }
    }

    [Serializable]
    public class RWFrameList : StructSection
    {
        public override SectionId sectionId => SectionId.FrameList;

        public Frame[] frames = new Frame[0];

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            frames = new Frame[reader.ReadUInt32()];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Frame.ReadNew(reader);
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(frames.Length);
            foreach (Frame f in frames)
                f.Write(writer);
        }
    }
}