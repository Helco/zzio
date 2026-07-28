using System;
using System.Diagnostics;
using System.Numerics;
using Veldrid;

namespace zzre.rendering;

public class SkeletonPoseBinding : BaseBinding
{
    private const int MaxBoneCount = 128;

    private bool isContentDirty = true;
    private DeviceBuffer? poseBuffer;
    private DeviceBufferRange poseBufferRange;
    private Matrix4x4[] poseMatrices = [];

    public Skeleton? Skeleton
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Bones.Count, MaxBoneCount, nameof(value));
            field = value;
            poseBuffer?.Dispose();
            poseBuffer = Parent.Device.ResourceFactory.CreateBuffer(new BufferDescription(
                MaxBoneCount * 4 * 4 * sizeof(float),
                BufferUsage.StructuredBufferReadOnly | BufferUsage.DynamicWrite,
                4 * 4 * sizeof(float)));
            poseBuffer.Name = $"{field.Name} Pose {GetHashCode()}";
            poseBufferRange = new DeviceBufferRange(PoseBuffer, 0, poseBuffer.SizeInBytes);
            if (poseMatrices.Length < field.Bones.Count)
                poseMatrices = new Matrix4x4[field.Bones.Count];
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

        // Erst vollstaendig auf der CPU rechnen, dann am Stueck in den gemappten
        // Puffer schreiben. Vorher wurde die Hierarchie direkt im gemappten
        // GPU-Speicher aufgeloest (map[i] = ... * map[parentI]) — ein Rueckwaerts-
        // lesen aus Write-gemapptem Speicher. Auf Desktop-GPUs (gecachter Unified
        // Memory) geht das zufaellig gut; auf write-combined Upload-Speicher wie
        // dem des Apple A12 liefert es zeitweise veraltete Werte, und Kind-Bones
        // (Arme, Haende) erben dann kaputte Eltern-Matrizen — sichtbar als vom
        // Koerper geloeste, in der Luft fliegende Gliedmassen.
        for (int i = 0; i < Skeleton.Bones.Count; i++)
        {
            var parentI = Skeleton.Parents[i];
            Debug.Assert(parentI < i);
            Debug.Assert(parentI < 0 || Skeleton.Bones[parentI] == Skeleton.Bones[i].Parent);
            poseMatrices[i] = parentI < 0
                ? Skeleton.Bones[i].ParentToLocal
                : Skeleton.Bones[i].ParentToLocal * poseMatrices[parentI];
        }
        for (int i = 0; i < Skeleton.Bones.Count; i++)
            poseMatrices[i] = Skeleton.BindingObjectToBone[i] * poseMatrices[i];

        var map = Parent.Device.Map<Matrix4x4>(poseBuffer, MapMode.Write);
        for (int i = 0; i < Skeleton.Bones.Count; i++)
            map[i] = poseMatrices[i];
        Parent.Device.Unmap(poseBuffer);
    }
}
