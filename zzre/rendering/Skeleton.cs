using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.rwbs;

namespace zzre;

public partial class Skeleton
{
    private BoneAnimator[]? animators;

    public string Name { get; set; }
    public IReadOnlyList<Matrix4x4> BindingBoneToObject { get; }
    public IReadOnlyList<Matrix4x4> BindingObjectToBone { get; }
    public IReadOnlyList<Location> Bones { get; }
    public IReadOnlyList<int> UserIds { get; }
    public IReadOnlyList<int> Parents { get; }
    public IReadOnlyList<int> Roots { get; }
    public Location Location { get; } = new Location();

    public SkeletalAnimation? Animation { get; private set; }
    public float AnimationTime
    {
        get => animators == null ? 0.0f : animators[0].Time;
        set
        {
            if (animators == null)
                return;
            foreach (ref var a in animators.AsSpan())
                a.Time = value;
            AddTime(0.0f);
        }
    }
    public float NormalizedAnimationTime => AnimationTime / (Animation?.duration ?? 1.0f);

    public Skeleton(RWSkinPLG skin, string name = "")
    {
        Name = name;
        BindingObjectToBone = skin.bones.Select(b => b.objectToBone with { M14 = 0f, M24 = 0f, M34 = 0f, M44 = 1f }).ToArray();
        UserIds = skin.bones.Select(b => (int)b.id).ToArray();

        var roots = new List<int>();
        var bones = new Location[skin.bones.Length];
        var parents = new int[skin.bones.Length];
        var parentStack = new Stack<int>();
        for (int i = 0; i < bones.Length; i++)
        {
            var bone = skin.bones[i];
            if (parentStack.Any())
                parents[i] = parentStack.Peek();
            else
            {
                parents[i] = -1;
                roots.Add(i);
            }

            if (!bone.flags.HasFlag(BoneFlags.IsChildless))
                parentStack.Push(i);
            else if (!bone.flags.HasFlag(BoneFlags.HasNextSibling))
            {
                while (parentStack.Any() &&
                    !skin.bones[parentStack.Pop()].flags.HasFlag(BoneFlags.HasNextSibling)) ;
            }

            bones[i] = new Location
            {
                Parent = parents[i] < 0 ? Location : bones[parents[i]],
                WorldToLocal = BindingObjectToBone[i]
            };
        }
        Bones = bones;
        Parents = parents;
        Roots = roots.ToArray();
        BindingBoneToObject = bones.Select(b => b.LocalToWorld).ToArray();
    }

    public void ResetToBinding()
    {
        for (int i = 0; i < Bones.Count; i++)
            Bones[i].LocalToWorld = BindingBoneToObject[i] * Location.LocalToWorld;
    }

    public void JumpToAnimation(SkeletalAnimation? nextAnimation, bool loop = true)
    {
        if (nextAnimation == null)
        {
            Animation = null;
            animators = null;
        }
        else
            BlendToAnimation(nextAnimation, 0.0f, loop);
    }

    public void BlendToAnimation(SkeletalAnimation nextAnimation, float blendDuration, bool loop = true)
    {
        Animation = nextAnimation;
        animators = nextAnimation.boneFrames
            .Select((frameSet, i) => new BoneAnimator(frameSet, nextAnimation.duration, loop,
                blendDuration, Quaternion.Conjugate(Bones[i].LocalRotation), Bones[i].LocalPosition))
            .ToArray();
        AddTime(0f);
    }

    public void AddTime(float delta)
    {
        if (animators == null)
            return;

        for (int i = 0; i < Bones.Count; i++)
        {
            // unfortunately no idea why the conjugate has to be used
            var bone = Bones[i];
            animators[i].AddTime(delta);
            bone.LocalRotation = Quaternion.Conjugate(animators[i].CurRotation);
            bone.LocalPosition = animators[i].CurTranslation;
        }

        if (animators.All(a => a.IsFinished))
        {
            Animation = null;
            animators = null;
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
