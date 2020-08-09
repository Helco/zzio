using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.rwbs;

namespace zzre
{
    public partial class Skeleton
    {
        private Matrix4x4[] pose, invPose;
        private BoneAnimator[]? currentAnimators, nextAnimators;

        public int BoneCount => pose.Length;
        public IReadOnlyList<Matrix4x4> BindingObjectToBone;
        public IReadOnlyList<Matrix4x4> BindingBoneToObject;
        public IReadOnlyList<Matrix4x4> Pose => pose;
        public IReadOnlyList<Matrix4x4> InvPose => invPose;
        public IReadOnlyList<int> UserIds { get; }
        public IReadOnlyList<int> Parents { get; }
        public IReadOnlyList<int> Roots { get; }

        public SkeletalAnimation? CurrentAnimation { get; private set; }
        public SkeletalAnimation? NextAnimation { get; private set; }
        public float AnimationTime
        {
            get => currentAnimators == null ? 0.0f : currentAnimators[0].Time;
            set
            {
                if (currentAnimators == null)
                    return;
                foreach (var a in currentAnimators)
                    a.Time = value;
            }
        }
        public float NormalizedAnimationTime => AnimationTime / (CurrentAnimation?.duration ?? 1.0f);

        public Skeleton(RWSkinPLG skin)
        {
            pose = skin.bones.Select(b => b.objectToBone.ResetRow3().ToNumerics()).ToArray();
            invPose = new Matrix4x4[pose.Length];
            UserIds = skin.bones.Select(b => (int)b.id).ToArray();
            UpdateInvPose();
            BindingBoneToObject = pose.ToArray();
            BindingObjectToBone = invPose.ToArray();

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

        public void ResetToBinding()
        {
            invPose = BindingBoneToObject.ToArray();
            pose = BindingObjectToBone.ToArray();
        }

        public void JumpToAnimation(SkeletalAnimation animation)
        {
            CurrentAnimation = animation;
            currentAnimators = animation.boneFrames
                .Select(frameSet => new BoneAnimator(frameSet, animation.duration))
                .ToArray();
            NextAnimation = null;
            AddTime(0.0f);
        }

        public void AddTime(float delta)
        {
            if (currentAnimators == null)
                return;
            for (int boneI = 0; boneI < BoneCount; boneI++)
            {
                currentAnimators[boneI].AddTime(delta);
                if (nextAnimators != null)
                    nextAnimators[boneI].AddTime(delta);

                var rot = Quaternion.Conjugate(currentAnimators[boneI].CurRotation); // unfortunately no idea why the conjugate has to be used
                var pos = currentAnimators[boneI].CurTranslation;
                var parentPose = Parents[boneI] < 0 ? Matrix4x4.Identity : pose[Parents[boneI]];
                pose[boneI] = Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(pos) * parentPose;
            }

            UpdateInvPose();
        }
    }
}
