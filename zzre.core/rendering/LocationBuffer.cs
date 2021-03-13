using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;

namespace zzre.rendering
{
    public class LocationBuffer : BaseDisposable
    {
        private const uint MatrixSize = 4 * 4 * sizeof(float);
        private const uint MinimalMatrixStride = MatrixSize;

        private uint matrixStride;
        private int matrixStrideAsMultiple;
        private WeakReference<Location>?[] locations;
        private bool[] isInverted;
        private Matrix4x4[] matrices;
        private int nextFreeIndex = 0;
        private DeviceBuffer buffer;

        public int Capacity => matrices.Length;
        public int Count { get; private set; } = 0;
        public bool IsFull => Capacity == Count;

        public LocationBuffer(GraphicsDevice device, int capacity = 1024)
        {
            matrixStride = Math.Max(MinimalMatrixStride, device.UniformBufferMinOffsetAlignment);
            if (matrixStride % MatrixSize != 0)
                throw new NotSupportedException("UniformBufferMinOffsetAlignment must be a multiple of Matrix4x4 size");
            matrixStrideAsMultiple = (int)(matrixStride / MatrixSize);

            locations = new WeakReference<Location>?[capacity];
            isInverted = new bool[capacity];
            matrices = new Matrix4x4[capacity * matrixStrideAsMultiple];
            buffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)capacity * matrixStride, BufferUsage.UniformBuffer));
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            buffer.Dispose();
        }

        public DeviceBufferRange Add(Location location, bool inverted = false)
        {
            if (IsFull && !FindSlotToCleanup())
                throw new InvalidOperationException("LocationBuffer has no available slot free");

            Count++;
            int usedIndex = nextFreeIndex++;
            locations[usedIndex] = new WeakReference<Location>(location);
            isInverted[usedIndex] = inverted;
            if (!IsFull)
            {
                while (locations[nextFreeIndex]?.TryGetTarget(out _) ?? false)
                    nextFreeIndex++;
                if (locations[nextFreeIndex] != null)
                    Remove(nextFreeIndex);
            }
            else
                nextFreeIndex = Capacity;

            return new DeviceBufferRange(buffer, (uint)usedIndex * matrixStride, MatrixSize);
        }

        public void Remove(DeviceBufferRange range) => Remove((int)(range.Offset / matrixStride));

        private void Remove(int freeIndex)
        {
            if (locations[freeIndex] == null)
                return;
            Count--;
            locations[freeIndex] = null;
            nextFreeIndex = Math.Min(nextFreeIndex, freeIndex);
#if DEBUG
            matrices[freeIndex * matrixStrideAsMultiple] = new Matrix4x4() * float.NaN; // as canary value
#endif
        }

        private bool FindSlotToCleanup()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!(locations[i]?.TryGetTarget(out _) ?? false))
                {
                    Remove(i);
                    return true;
                }
            }
            return false;
        }

        private (int min, int max) UpdateMatrixArray()
        {
            int minIndex = -1, maxIndex = -1;
            int found = 0;
            for (int i = 0; found < Count && i < Capacity; i++)
            {
                Location? location = null;
                if (!(locations[i]?.TryGetTarget(out location) ?? false))
                {
                    Remove(i);
                    continue;
                }
                found++;
                if (minIndex < 0)
                    minIndex = i;
                maxIndex = i;
                matrices[i * matrixStrideAsMultiple] = isInverted[i]
                    ? location!.WorldToLocal
                    : location!.LocalToWorld;
            }
            return (minIndex, maxIndex);
        }

        public void Update(CommandList cl)
        {
            var (minI, maxI) = UpdateMatrixArray();
            if (minI < 0 || maxI < 0)
                return;
            cl.UpdateBuffer(buffer, (uint)minI * matrixStride, ref matrices[0], (uint)(maxI - minI + 1) * matrixStride);
        }

        public void Update(GraphicsDevice device)
        {
            var (minI, maxI) = UpdateMatrixArray();
            if (minI < 0 || maxI < 0)
                return;
            device.UpdateBuffer(buffer, (uint)minI * matrixStride, ref matrices[0], (uint)(maxI - minI + 1) * matrixStride);
        }
    }
}
