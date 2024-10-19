using System;
using System.Linq;
using Veldrid;
using zzio;

namespace zzre.rendering;

public class DynamicGraphicsBuffer : BaseDisposable
{
    private const int MaxUploadDistance = 512;

    private readonly GraphicsDevice device;
    private readonly BufferUsage usage;
    private readonly string bufferName;
    private readonly float minGrowFactor;
    private readonly RangeCollection dirtyBytes = new(0);
    private readonly RangeCollection usedElements = new(0);
    private byte[]? bytes;
    private int sizePerElement;

    public DeviceBuffer? OptionalBuffer { get; private set; }
    public DeviceBuffer Buffer => OptionalBuffer ??
        throw new InvalidOperationException("Buffer was not created yet");

    public int SizePerElement
    {
        get => sizePerElement;
        set
        {
            if (Count > 0)
                throw new InvalidOperationException("Cannot change sizePerElement of a non-empty buffer");
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            sizePerElement = value;
        }
    }
    public int CommittedCapacity => SizePerElement > 0
        ? (bytes?.Length ?? 0) / SizePerElement
        : 0;
    public int ReservedCapacity
    {
        get => usedElements.MaxRangeValue;
        private set => usedElements.MaxRangeValue = value;
    }
    public int Count => usedElements.Area;
    public int FreeCount => ReservedCapacity - Count;
    private int FreeCountAtEnd => ReservedCapacity - (LastUsedIndex + 1);
    private int LastUsedIndex => usedElements.MaxValue;

    public DynamicGraphicsBuffer(GraphicsDevice device, BufferUsage usage,
        string bufferName = nameof(DynamicGraphicsBuffer),
        float minGrowFactor = 1.5f)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minGrowFactor, 1f);
        this.device = device;
        this.usage = usage;
        this.bufferName = bufferName;
        this.minGrowFactor = minGrowFactor;
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        OptionalBuffer?.Dispose();
        OptionalBuffer = null;
        bytes = null;
        dirtyBytes.MaxRangeValue = 0;
        Clear();
    }

    /// <summary>Forgets all rentals (commited or reserved)</summary>
    public void Clear()
    {
        usedElements.Clear();
        usedElements.MaxRangeValue = CommittedCapacity;
        dirtyBytes.Clear();
    }

    public Range Rent(int request, bool fast = false)
    {
        if (!fast)
        {
            var bestRange = usedElements.AddBestFit(request);
            if (bestRange.HasValue)
                return bestRange.Value;
        }
        int missingElements = request - FreeCountAtEnd;
        if (missingElements > 0)
            ReservedCapacity += missingElements;

        var firstFree = LastUsedIndex + 1;
        var endRange = firstFree..(firstFree + request);
        usedElements.Add(endRange);
        return endRange;
    }

    public void Return(Range range) => usedElements.Remove(range);

    private void Commit()
    {
        if (sizePerElement == 0)
            throw new InvalidOperationException("Cannot commit dynamic graphics buffer without a size per element");

        int nextCapacity = ReservedCapacity;
        if (nextCapacity > CommittedCapacity)
        {
            nextCapacity = Math.Max(ReservedCapacity, (int)(CommittedCapacity * minGrowFactor + 0.5f));
            ReservedCapacity = Math.Max(ReservedCapacity, nextCapacity);
        }

        var nextCapacityInBytes = nextCapacity * sizePerElement;
        if (nextCapacityInBytes > (bytes?.Length ?? 0))
        {
            Array.Resize(ref bytes, nextCapacityInBytes);
            dirtyBytes.MaxRangeValue = nextCapacityInBytes;
        }
    }

    private Range AsByteRange(Range range)
    {
        var (offset, length) = range.GetOffsetAndLength(ReservedCapacity);
        return (offset * sizePerElement)..((offset + length) * sizePerElement);
    }

    public ReadOnlySpan<byte> Read(Range range)
    {
        Commit();
        return bytes!.AsSpan(AsByteRange(range));
    }

    public Span<byte> Write(Range range)
    {
        Commit();
        var byteRange = AsByteRange(range);
        dirtyBytes.Add(byteRange);
        return bytes!.AsSpan(byteRange);
    }

    public void Update(CommandList cl)
    {
        if (!dirtyBytes.Any() && (OptionalBuffer != null || CommittedCapacity == 0))
            return;
        int capacityInBytes = CommittedCapacity * SizePerElement;
        if ((uint)dirtyBytes.MaxValue > (OptionalBuffer?.SizeInBytes ?? 0))
        {
            OptionalBuffer?.Dispose();
            OptionalBuffer = device.ResourceFactory.CreateBuffer(new((uint)capacityInBytes, usage));
            OptionalBuffer.Name = bufferName;
            dirtyBytes.Clear();
            foreach (var usedElementRange in usedElements)
                dirtyBytes.Add(AsByteRange(usedElementRange));
        }

        dirtyBytes.MergeNearbyRanges(MaxUploadDistance);
        foreach (var range in dirtyBytes)
        {
            var offset = range.GetOffset(capacityInBytes);
            cl.UpdateBuffer(OptionalBuffer!, (uint)offset, bytes.AsSpan(range));
        }
        dirtyBytes.Clear();
    }
}
