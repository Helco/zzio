﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.rwbs;

namespace zzre
{
    public partial class Skeleton
    {
        private BoneAnimator[]? currentAnimators, nextAnimators;

        public IReadOnlyList<Matrix4x4> BindingBoneToObject { get; }
        public IReadOnlyList<Matrix4x4> BindingObjectToBone { get; }
        public IReadOnlyList<Location> Bones { get; }
        public IReadOnlyList<int> UserIds { get; }
        public IReadOnlyList<int> Parents { get; }
        public IReadOnlyList<int> Roots { get; }
        public Location Location { get; } = new Location();

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
            BindingObjectToBone = skin.bones.Select(b => b.objectToBone.ResetRow3().ToNumerics()).ToArray();
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
                bones[index].Parent = parents[index] < 0 ? Location : bones[parents[index]];
                bones[index].WorldToLocal = BindingObjectToBone[index];
            }
            Bones = bones;
            Parents = parents;
            Roots = roots.ToArray();
            BindingBoneToObject = bones.Select(b => b.LocalToWorld).ToArray();
        }

        public void ResetToBinding()
        {
            foreach (var (bone, index) in Bones.Indexed())
                bone.LocalToWorld = BindingBoneToObject[index] * Location.LocalToWorld;
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
            {
                ResetToBinding();
                return;
            }

            foreach (var (bone, boneI) in Bones.Indexed())
            {
                currentAnimators[boneI].AddTime(delta);
                nextAnimators?[boneI].AddTime(delta);

                bone.LocalRotation = Quaternion.Conjugate(currentAnimators[boneI].CurRotation); // unfortunately no idea why the conjugate has to be used
                bone.LocalPosition = currentAnimators[boneI].CurTranslation;
            }
        }

        public void ApplySingleIK(int boneIdx, Vector3 worldTargetPos)
        {
            var objTargetPos = Vector3.Transform(worldTargetPos, Bones[boneIdx].WorldToLocal);
            var objTargetDir = Vector3.Normalize(objTargetPos);
            var boneForward = Vector3.UnitY;
            var rotationAxis = Vector3.Normalize(Vector3.Cross(objTargetDir, boneForward));

            var dot = Math.Clamp(Vector3.Dot(objTargetDir, boneForward), 0.0f, 1.0f);
            var newRotation = Quaternion.CreateFromAxisAngle(rotationAxis, MathF.Acos(dot));
            Bones[boneIdx].LocalRotation = Quaternion.Conjugate(newRotation);
        }
    }
}
