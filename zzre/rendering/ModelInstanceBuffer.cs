using System;
using Veldrid;
using zzio;
using zzre.materials;

namespace zzre.rendering
{
    public class ModelInstanceBuffer : BaseDisposable
    {
        public DeviceBuffer DeviceBuffer { get; }
        public int TotalCount { get; }
        public int FreeCount { get; private set; }

        public ModelInstanceBuffer(ITagContainer diContainer, int totalCount, bool dynamic)
        {
            var resourceFactory = diContainer.GetTag<ResourceFactory>();
            var usageFlags = BufferUsage.VertexBuffer |
                (dynamic ? BufferUsage.Dynamic : default);
            DeviceBuffer = resourceFactory.CreateBuffer(new BufferDescription(
                ModelInstance.Stride * (uint)totalCount, usageFlags));
            DeviceBuffer.Name = nameof(ModelInstanceBuffer);
            TotalCount = totalCount;
            FreeCount = totalCount;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            DeviceBuffer.Dispose();
        }

        public void Clear() => FreeCount = TotalCount;

        public uint Reserve(int count)
        {
            if (count > FreeCount)
                throw new ArgumentOutOfRangeException($"ModelInstanceBuffer does not have enough capacity for {count} instances (only {FreeCount})");
            return (uint)(TotalCount - FreeCount);
        }
    }
}
