using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace zzio {
    [System.Serializable]
    public class SkeletalAnimation {
        [System.Serializable]
        public struct KeyFrame {
            public Quaternion rot;
            public Vector pos;
            public float time;
            public UInt32 parentOffset;
        }

        public UInt32 flags;
        public float duration;
        public KeyFrame[][] frames; //for every bone a set of keyframes

        public SkeletalAnimation(UInt32 f, float d, KeyFrame[][] fr) {
            flags = f;
            duration = d;
            frames = fr;
        }

        public static SkeletalAnimation read(byte[] buffer) {
            BinaryReader reader = new BinaryReader(new MemoryStream(buffer, false));
            UInt32 frameCount = reader.ReadUInt32();
            UInt32 flags = reader.ReadUInt32();
            float duration = reader.ReadSingle();
            List<List<KeyFrame>> keyframes = new List<List<KeyFrame>>();
            int[] parents = new int[frameCount];
            for (UInt32 i=0; i<frameCount; i++) {
                KeyFrame f;
                f.rot = Quaternion.read(reader);
                f.pos = Vector.read(reader);
                f.time = reader.ReadSingle();
                f.parentOffset = reader.ReadUInt32();

                uint index = f.parentOffset / 36; //convert offset to index;
                List<KeyFrame> boneFrameSet;
                if (f.parentOffset == 0xffffffff || index >= frameCount) {
                    parents[i] = -1;
                    keyframes.Add(boneFrameSet = new List<KeyFrame>());
                }
                else {
                    parents[i] = parents[index];
                    if (parents[i] < 0)
                        parents[i] = (int)index;
                    boneFrameSet = keyframes[parents[i]];
                }
                boneFrameSet.Add(f);
            }

            KeyFrame[][] frames = new KeyFrame[keyframes.Count][];
            for (int i = 0; i < keyframes.Count; i++)
                frames[i] = keyframes[i].ToArray();
            return new SkeletalAnimation(flags, duration, frames);
        }

        public void write(Stream s)
        {
            uint frameCount = 0;
            for (int i = 0; i < frames.Length; i++)
                frameCount += (uint)frames[i].Length;

            BinaryWriter writer = new BinaryWriter(s);
            writer.Write(frameCount);
            writer.Write(flags);
            writer.Write(duration);

            //let just hope for the moment that there is no
            //intrinsic meaning to the order of the frames
            uint framesWritten = 0;
            for (int i=0; i<frames.Length; i++)
            {
                for (int j=0; j<frames[i].Length; j++, framesWritten++)
                {
                    KeyFrame f = frames[i][j];
                    f.rot.write(writer);
                    f.pos.write(writer);
                    writer.Write(f.time);
                    if (j == 0)
                        writer.Write((uint)0xffffffff);
                    else
                        writer.Write(framesWritten * 36); //the offset of the previous written frame
                }
            }
        }

        public byte[] write()
        {
            MemoryStream mem = new MemoryStream(2048);
            write(mem);
            return mem.ToArray();
        }
    }
}
