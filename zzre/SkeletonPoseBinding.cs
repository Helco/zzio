using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Veldrid;

namespace zzre.rendering
{
    public class SkeletonPoseBinding : BaseBinding
    {
        private bool isContentDirty = true;
        private Skeleton? skeleton = null;
        private DeviceBuffer? poseBuffer = null;

        public Skeleton? Skeleton
        {
            get => skeleton;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                skeleton = value;
                poseBuffer?.Dispose();
                poseBuffer = Parent.Device.ResourceFactory.CreateBuffer(new BufferDescription(
                    (uint)value.BoneCount * 4 * 4 * sizeof(float),
                    BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic,
                    (uint)4 * 4 * sizeof(float)));
                isContentDirty = true;
                isBindingDirty = true;
            }
        }

        public DeviceBuffer PoseBuffer => poseBuffer!; // TODO: this is not the cleanest way...
        public override BindableResource? Resource => poseBuffer;

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
            for (int i = 0; i < Skeleton.BoneCount; i++)
                map[i] = Skeleton.BindingBoneToObject[i] * Skeleton.Pose[i];
            Parent.Device.Unmap(poseBuffer);
        }
    }
}
