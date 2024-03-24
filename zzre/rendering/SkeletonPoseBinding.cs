using System;
using System.Diagnostics;
using System.Numerics;
using Veldrid;

namespace zzre.rendering;

public class SkeletonPoseBinding : BaseBinding
{
    private const int MaxBoneCount = 128;

    private bool isContentDirty = true;
    private Skeleton? skeleton;
    private DeviceBuffer? poseBuffer;
    private DeviceBufferRange poseBufferRange;

    public Skeleton? Skeleton
    {
        get => skeleton;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Bones.Count, MaxBoneCount, nameof(value));
            skeleton = value;
            poseBuffer?.Dispose();
            poseBuffer = Parent.Device.ResourceFactory.CreateBuffer(new BufferDescription(
                MaxBoneCount * 4 * 4 * sizeof(float),
                BufferUsage.StructuredBufferReadOnly | BufferUsage.DynamicWrite,
                4 * 4 * sizeof(float)));
            poseBuffer.Name = $"{skeleton.Name} Pose {GetHashCode()}";
            poseBufferRange = new DeviceBufferRange(PoseBuffer, 0, poseBuffer.SizeInBytes);
            isContentDirty = true;
            isBindingDirty = true;
        }
    }

    public DeviceBuffer PoseBuffer => poseBuffer!; // TODO: this is not the cleanest way... 
    public override BindableResource? Resource => poseBufferRange;

    public SkeletonPoseBinding(IMaterial material) : base(material) { }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        poseBuffer?.Dispose();
    }

    public void MarkPoseDirty() => isContentDirty = true;

    public override void Update(CommandList cl)
    {
        if (!isContentDirty || poseBuffer == null || Skeleton == null)
            return;
        isContentDirty = false;

        var map = Parent.Device.Map<Matrix4x4>(poseBuffer, MapMode.Write);
        for (int i = 0; i < Skeleton.Bones.Count; i++)
        {
            var parentI = Skeleton.Parents[i];
            Debug.Assert(parentI < i);
            Debug.Assert(parentI < 0 || Skeleton.Bones[parentI] == Skeleton.Bones[i].Parent);
            map[i] = parentI < 0
                ? Skeleton.Bones[i].ParentToLocal
                : Skeleton.Bones[i].ParentToLocal * map[parentI];
        }
        for (int i = 0; i < Skeleton.Bones.Count; i++)
            map[i] = Skeleton.BindingObjectToBone[i] * map[i];
        Parent.Device.Unmap(poseBuffer);
    }
}
