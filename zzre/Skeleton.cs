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
        //private Matrix4x4[] pose;
        private BoneAnimator[]? currentAnimators, nextAnimators;

        public IReadOnlyList<Matrix4x4> BindingObjectToBone { get; }
        public IReadOnlyList<Matrix4x4> BindingBoneToObject { get; }
        public IReadOnlyList<Location> Bones { get; }
        public IReadOnlyList<int> UserIds { get; }
        public IReadOnlyList<int> Parents { get; }
        public IReadOnlyList<int> Roots { get; }
        //public IReadOnlyList<Matrix4x4> Pose => pose;

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
                AddTime(0.0f);
            }
        }
        public float NormalizedAnimationTime => AnimationTime / (CurrentAnimation?.duration ?? 1.0f);

        public Skeleton(RWSkinPLG skin)
        {
            BindingBoneToObject = skin.bones.Select(b => b.objectToBone.ResetRow3().ToNumerics()).ToArray();
            UserIds = skin.bones.Select(b => (int)b.id).ToArray();            

            var roots = new List<int>();
            var bones = new Location[skin.bones.Length];
            var parents = new int[skin.bones.Length];
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

                bones[index] = new Location();
                bones[index].Parent = parents[index] < 0 ? null : bones[parents[index]];
                bones[index].LocalToWorld = BindingBoneToObject[index];
            }
            Bones = bones;
            Parents = parents;
            Roots = roots.ToArray();
            BindingObjectToBone = bones.Select(b => b.WorldToLocal).ToArray();
            //pose = BindingObjectToBone.ToArray();
        }

        public void ResetToBinding()
        {
            foreach (var (bone, index) in Bones.Indexed())
                bone.WorldToLocal = BindingObjectToBone[index];
        }

        public void JumpToAnimation(SkeletalAnimation? animation)
        {
            NextAnimation = null;
            nextAnimators = null;
            if (animation == null)
            {
                CurrentAnimation = null;
                currentAnimators = null;
                return;
            }

            CurrentAnimation = animation;
            currentAnimators = animation.boneFrames
                .Select(frameSet => new BoneAnimator(frameSet, animation.duration))
                .ToArray();
            AddTime(0.0f);
        }

        public void AddTime(float delta)
        {
            if (currentAnimators == null)
                return;
            foreach (var (bone, boneI) in Bones.Indexed())
            {
                currentAnimators[boneI].AddTime(delta);
                if (nextAnimators != null)
                    nextAnimators[boneI].AddTime(delta);

                bone.LocalRotation = Quaternion.Conjugate(currentAnimators[boneI].CurRotation); // unfortunately no idea why the conjugate has to be used
                bone.LocalPosition = currentAnimators[boneI].CurTranslation;
                //var parentPose = Parents[boneI] < 0 ? Matrix4x4.Identity : pose[Parents[boneI]];
                //pose[boneI] = Matrix4x4.CreateFromQuaternion(bone.LocalRotation) * Matrix4x4.CreateTranslation(bone.LocalPosition) * parentPose;
            }
        }
    }
}
