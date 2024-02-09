using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using zzio;
using System.Numerics;
using System.Runtime.InteropServices;

namespace zzio;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AnimationKeyFrame
{
    public Quaternion rot;
    public Vector3 pos;
    public float time;
    public int parentFrameOffset;

    public const int ExpectedSize = (4 + 3 + 1 + 1) * 4;

    public static AnimationKeyFrame ReadNew(BinaryReader reader)
    {
        return new AnimationKeyFrame
        {
            rot = reader.ReadQuaternion(),
            pos = reader.ReadVector3(),
            time = reader.ReadSingle()
        };
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(rot);
        writer.Write(pos);
        writer.Write(time);
    }
}

[Serializable]
public class SkeletalAnimation
{
    public uint flags = 0; // TODO: Format of flags are still unknown 
    public float duration = 0.0f;
    public AnimationKeyFrame[][] boneFrames = Array.Empty<AnimationKeyFrame[]>(); // a set of keyframes for every bone

    public int BoneCount => boneFrames.Length;

    public static SkeletalAnimation ReadNew(Stream stream)
    {
        SkeletalAnimation anim = new();
        using BinaryReader reader = new(stream);

        var frameCount = reader.ReadInt32();
        anim.flags = reader.ReadUInt32();
        anim.duration = reader.ReadSingle();

        var allFrames = reader.ReadStructureArray<AnimationKeyFrame>(frameCount, AnimationKeyFrame.ExpectedSize);
        int[] frameBones = new int[frameCount];
        int nextBoneI = 0;
        for(int i = 0; i < frameCount; i++)
        {
            if (allFrames[i].parentFrameOffset < 0) // new unknown bone
                frameBones[i] = nextBoneI++;
            else
                frameBones[i] = frameBones[allFrames[i].parentFrameOffset / AnimationKeyFrame.ExpectedSize];
        }
        anim.boneFrames = allFrames
            .Zip(frameBones)
            .GroupBy(t => t.Second)
            .Select(g => g.Select(t => t.First).ToArray())
            .ToArray();
        return anim;
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(boneFrames.Sum(frameSet => frameSet.Length));
        writer.Write(flags);
        writer.Write(duration);

        // sort and interleave the keyframes
        var sortedMapping = boneFrames
            .SelectMany((frameSet, boneI) =>
                frameSet.Select((frame, frameI) => new KeyValuePair<int, int>(boneI, frameI))
            ).OrderBy(pair => boneFrames[pair.Key][pair.Value].time)
            .ToArray();

        var lastParentOffsets = Enumerable.Repeat(-1, BoneCount).ToArray();
        for (int writtenI = 0; writtenI < sortedMapping.Length; writtenI++)
        {
            var mapping = sortedMapping[writtenI];
            boneFrames[mapping.Key][mapping.Value].Write(writer);
            writer.Write(lastParentOffsets[mapping.Key]);
            lastParentOffsets[mapping.Key] = writtenI * AnimationKeyFrame.ExpectedSize;
        }
    }
}
