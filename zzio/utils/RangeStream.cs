using System;
using System.IO;

namespace zzio;

/// <summary>A Stream decorator limiting access to a fixed range</summary>
/// <remarks>Modifying the parent stream while using RangeStream causes undefined behavior</remarks>
public class RangeStream : Stream
{
    private readonly Stream parent;
    private readonly long length;
    private readonly bool canWrite;
    private readonly bool shouldClose;
    private long left;

    /// <summary>Constructs a new RangeStream</summary>
    /// <param name="parent">The parent stream to read from/write to</param>
    /// <param name="length">The length of the range (starting from the current position) to be accessable</param>
    /// <param name="canWrite">Whether write access should be allowed</param>
    /// <param name="shouldClose">Whether the parent stream should be closed by closing the RangeStream</param>
    public RangeStream(Stream parent, long length, bool canWrite = true, bool shouldClose = true)
    {
        if (parent == null)
            throw new InvalidDataException("Parent stream is null");
        long parentLength, parentStart;
        try
        {
            parentStart = parent.Position;
            parentLength = parent.Length;
        }
        catch (NotSupportedException)
        {
            parentStart = 0;
            parentLength = 0;
        }

        this.parent = parent;
        this.length = left = Math.Min(parentLength - parentStart, length);
        this.canWrite = canWrite && parent.CanWrite;
        this.shouldClose = shouldClose;
    }

    public override void Close()
    {
        if (shouldClose)
            parent.Close();
    }

    public override bool CanRead => parent.CanRead;
    public override bool CanWrite => canWrite;
    public override bool CanSeek => parent.CanSeek;
    public override bool CanTimeout => parent.CanTimeout;
    public override long Length => length;

    public override long Position
    {
        get => length - left;
        set
        {
            long clamped = Math.Max(0, Math.Min(value, length));
            long newLeft = length - clamped;
            if (left != newLeft)
            {
                parent.Seek(left - newLeft, SeekOrigin.Current);
                left = newLeft;
            }
        }
    }

    public override int WriteTimeout
    {
        get => parent.WriteTimeout;
        set => parent.WriteTimeout = value;
    }

    public override void Flush()
    {
        parent.Flush();
    }

    public override void SetLength(long length)
    {
        throw new NotSupportedException("Setting the length is not supported in RangeStream");
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition;
        switch (origin)
        {
            case SeekOrigin.Begin: newPosition = offset; break;
            case SeekOrigin.End: newPosition = length + offset; break;
            case SeekOrigin.Current: newPosition = Position + offset; break;
            default: throw new NotSupportedException("SeekOrigin \"" + origin + "\" is not supported");
        }
        Position = newPosition;
        return newPosition;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int maxReadCount = (int)Math.Min(count, left);
        int readCount = parent.Read(buffer, offset, maxReadCount);
        left -= readCount;
        return readCount;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        int writeCount = (int)Math.Min(count, left);
        parent.Write(buffer, offset, writeCount);
        left -= writeCount;
    }
}
