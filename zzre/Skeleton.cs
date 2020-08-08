using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio.rwbs;

namespace zzre
{
    public class Skeleton
    {
        private readonly IReadOnlyList<Matrix4x4> objectToBone; // also called binding pose
        private readonly Matrix4x4[] pose, invPose;

        public int BoneCount => pose.Length;
        public IReadOnlyList<Matrix4x4> Pose => pose;
        public IReadOnlyList<Matrix4x4> InvPose => invPose;
        public IReadOnlyList<int> UserIds { get; }
        public IReadOnlyList<int> Parents { get; }
        public IReadOnlyList<int> Roots { get; }

        public Skeleton(RWSkinPLG skin)
        {
            objectToBone = skin.bones.Select(b => b.objectToBone.ResetRow3().ToNumerics()).ToArray();
            pose = objectToBone.ToArray();
            invPose = new Matrix4x4[pose.Length];
            UserIds = skin.bones.Select(b => (int)b.id).ToArray();
            UpdateInvPose();

            var roots = new List<int>();
            var parents = new int[BoneCount];
            var parentStack = new Stack<int>();
            foreach (var (bone, index) in skin.bones.Indexed())
            {
                if (parentStack.Any())
                    parents[index] = parentStack.Peek();
                else
                {
                    parents[index] = -1;
                    roots.Add(index);
                }

                if (!bone.flags.HasFlag(BoneFlags.IsChildless))
                    parentStack.Push(index);
                else if (!bone.flags.HasFlag(BoneFlags.HasNextSibling))
                {
                    while (parentStack.Any() &&
                        !skin.bones[parentStack.Pop()].flags.HasFlag(BoneFlags.HasNextSibling));
                }
            }
            Parents = parents;
            Roots = roots.ToArray();
        }

        private void UpdateInvPose()
        {
            for (int i = 0; i < pose.Length; i++)
            {
                if (!Matrix4x4.Invert(pose[i], out invPose[i]))
                    throw new InvalidProgramException();
            }
        }
    }
}
