using System;
using System.Collections.Generic;
using System.Linq;
using zzio.utils;
using zzio.primitives;
using System.IO;

namespace zzio.rwbs
{
    public enum CollisionSectorType
    {
        X = 0,
        Y = 4,
        Z = 8
    }

    [Serializable]
    public struct CollisionSector
    {
        public CollisionSectorType type;
        public float value;
        public int index;
        public int count;
    }

    [Serializable]
    public struct CollisionSplit
    {
        public CollisionSector right, left;
    }

    [Serializable]
    public class RWCollision : Section
    {
        public const int SplitCount = -1;
        public override SectionId sectionId => SectionId.CollisionPLG;

        public CollisionSplit[] splits = Array.Empty<CollisionSplit>();
        public int[] map = Array.Empty<int>();

        protected override void readBody(Stream stream)
        {
            using var reader = new BinaryReader(stream);
            splits = new CollisionSplit[reader.ReadInt32() - 1];
            map = new int[reader.ReadInt32()];

            foreach (ref var split in splits.AsSpan())
            {
                uint types = reader.ReadUInt32();
                split.right.index = reader.ReadUInt16();
                split.left.index = reader.ReadUInt16();
                split.right.value = reader.ReadSingle();
                split.left.value = reader.ReadSingle();

                split.right.type = (CollisionSectorType)(types >> 16);
                split.left.type = split.right.type;
                split.right.count = ((types >> 0) & 0xff) == 2 ? SplitCount : 0;
                split.left.count = ((types >> 8) & 0xff) == 2 ? SplitCount : 0;
            }

            var stack = new Stack<(int splitI, bool isRight)>();
            stack.Push((0, true));
            stack.Push((0, false));
            while(stack.Any())
            {
                var (splitI, isRight) = stack.Pop();
                ref var cur = ref isRight
                    ? ref splits[splitI].right
                    : ref splits[splitI].left;
                if (cur.count == SplitCount)
                {
                    stack.Push((cur.index, true));
                    stack.Push((cur.index, false));
                    continue;
                }

                cur.index = reader.ReadUInt16();
                cur.count = reader.ReadUInt16();
            }

            reader.ReadStructureArray(map);
        }

        protected override void writeBody(Stream stream)
        {
            using var writer = new BinaryWriter(stream);
            writer.Write(splits.Length + 1);
            writer.Write(map.Length);

            foreach (var split in splits)
            {
                writer.Write(
                    ((split.right.count == SplitCount ? 2u : 0u) << 0) |
                    ((split.left.count == SplitCount ? 2u : 0u) << 8) |
                    ((uint)split.right.type << 16));
                writer.Write((ushort)split.right.index);
                writer.Write((ushort)split.left.index);
                writer.Write(split.right.value);
                writer.Write(split.left.value);
            }

            var stack = new Stack<(int splitI, bool isRight)>();
            stack.Push((0, true));
            stack.Push((0, false));
            while (stack.Any())
            {
                var (splitI, isRight) = stack.Pop();
                var cur = isRight
                    ? splits[splitI].right
                    : splits[splitI].left;
                if (cur.count == SplitCount)
                {
                    stack.Push((cur.index, true));
                    stack.Push((cur.index, false));
                    continue;
                }

                writer.Write((ushort)cur.index);
                writer.Write((ushort)cur.count);
            }

            writer.WriteStructureArray(map);
        }
    }
}
