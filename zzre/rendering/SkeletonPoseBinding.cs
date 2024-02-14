using System;
using System.Numerics;
using Veldrid;
using zzio;

namespace zzre.rendering;

public class SkeletonPoseBinding : BaseBinding
{
    private const uint MaxBoneCount = 128;

    private bool isContentDirty = true;
    private Skeleton? skeleton;
    private DeviceBuffer? poseBuffer;
    private DeviceBufferRange poseBufferRange;

    public Skeleton? Skeleton
    {
        get => skeleton;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Bones.Count > MaxBoneCount)
                throw new ArgumentException($"Too many bones in skeleton ({value.Bones.Count} > {MaxBoneCount})", nameof(value));
            skeleton = value;
            poseBuffer?.Dispose();
            poseBuffer = Parent.Device.ResourceFactory.CreateBuffer(new BufferDescription(
                MaxBoneCount * 4 * 4 * sizeof(float),
                BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic,
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
        foreach (var (bone, i) in Skeleton.Bones.Indexed())
            map[i] = Skeleton.BindingObjectToBone[i] * bone.LocalToWorld * Skeleton.Location.WorldToLocal;
        Parent.Device.Unmap(poseBuffer);
    }
}
