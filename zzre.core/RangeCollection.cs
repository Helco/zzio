using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using zzio;

namespace zzre;

public class RangeCollection : ICollection<Range>, IReadOnlyCollection<Range>
{
    private readonly SortedSet<Range> ranges = new(new RangeComparer());

    public bool IsReadOnly => false;
    public int Count => ranges.Count;
    public Range Total => ranges.Any()
        ? new Range(ranges.First().Start, ranges.Last().End)
        : new Range(Index.Start, Index.Start);
    public int Area => ranges.Sum(r => r.GetOffsetAndLength(MaxRangeValue).Length);

    public int MinValue => ranges.Any()
        ? ranges.First().Start.GetOffset(MaxRangeValue)
        : -1;

    public int MaxValue => ranges.Any()
        ? ranges.Last().End.GetOffset(MaxRangeValue) - 1
        : -1;

    private int _maxRangeValue;
    public int MaxRangeValue
    {
        get => _maxRangeValue;
        set
        {
            if (_maxRangeValue > value)
                Remove(value..);
            _maxRangeValue = value;
        }
    }

    public RangeCollection(int maxRangeValue = int.MaxValue) =>
        MaxRangeValue = maxRangeValue;

    public void Add(Range item)
    {
        var (itemOffset, itemLength) = item.GetOffsetAndLength(MaxRangeValue);
        if (itemOffset == 0 && itemLength >= MaxRangeValue)
        {
            ranges.Clear();
            ranges.Add(..MaxRangeValue);
            return;
        }

        var nearItem = new Range(
            Math.Max(0, itemOffset - 1),
            itemOffset + itemLength + (itemOffset + itemLength < MaxRangeValue ? 1 : 0));
        var intersections = FindIntersections(nearItem).Prepend(item).ToArray();
        if (intersections.Any())
        {
            foreach (var i in intersections.Skip(1))
                ranges.Remove(i);
            ranges.Add(new Range(
                intersections.Min(r => r.Start.GetOffset(MaxRangeValue)),
                intersections.Max(r => r.End.GetOffset(MaxRangeValue))));
        }
        else
            ranges.Add(item);
    }

    public bool Remove(Range remove)
    {
        var removeStart = remove.Start.GetOffset(MaxRangeValue);
        var removeEnd = remove.End.GetOffset(MaxRangeValue);
        if (removeStart == 0 && removeEnd == MaxRangeValue)
        {
            var wasNotEmpty = ranges.Any();
            Clear();
            return wasNotEmpty;
        }

        var intersections = FindIntersections(remove).ToArray();
        var result = false;
        foreach (var i in intersections)
        {
            result = true;
            ranges.Remove(i);
            if (i.Start.GetOffset(MaxRangeValue) < removeStart)
                ranges.Add(new Range(i.Start, remove.Start));
            else if (i.End.GetOffset(MaxRangeValue) > removeEnd)
                ranges.Add(new Range(remove.End, i.End));
            // else fully contained
        }
        return result;
    }

    public Range? AddBestFit(int length)
    {
        if (!ranges.Any())
        {
            if (MaxRangeValue < length)
                return null;
            Add(0..length);
            return 0..length;
        }

        var lastEnd = 0;
        int bestStart = -1;
        int bestLength = int.MaxValue;
        foreach (var curRange in ranges)
        {
            var (curStart, curEnd) = curRange.GetOffsetAndLength(MaxRangeValue);
            curEnd += curStart;

            int curHoleLength = curStart - lastEnd;
            if (curHoleLength >= length && curHoleLength < bestLength)
            {
                bestStart = lastEnd;
                bestLength = curHoleLength;
            }
            if (bestLength == length)
                break; // it does not get better than optimal

            lastEnd = curEnd;
        }
        if (bestStart < 0)
            return null;
        var newRange = bestStart..(bestStart + length);
        Add(newRange);
        return newRange;
    }

    public Range? RemoveBestFit(int length)
    {
        var range = ranges
            .Where(r => r.GetLength(MaxRangeValue) >= length)
            .OrderBy(r => r.GetLength(MaxRangeValue))
            .FirstOrDefault();
        if (range.Equals(default))
            return null;
        ranges.Remove(range);
        range = range.Start..range.Start.Offset(length);
        ranges.Add(range);
        return range;
    }

    public bool Contains(Range item) =>
        FindIntersections(item)
        .Any(i => Contains(item, i));

    public bool Intersects(Range item) => FindIntersections(item).Any();

    public IEnumerable<Range> FindIntersections(Range search) => ranges
        .Where(r => Intersects(r, search));

    private bool Intersects(Range r1, Range r2) =>
        r1.End.GetOffset(MaxRangeValue) > r2.Start.GetOffset(MaxRangeValue) &&
        r2.End.GetOffset(MaxRangeValue) > r1.Start.GetOffset(MaxRangeValue);

    private bool Contains(Range inner, Range outer) =>
        inner.Start.GetOffset(MaxRangeValue) >= outer.Start.GetOffset(MaxRangeValue) &&
        inner.End.GetOffset(MaxRangeValue) <= outer.End.GetOffset(MaxRangeValue);

    public void MergeNearbyRanges(int maxDistance)
    {
        if (maxDistance < 1 || ranges.Count < 1)
            return;
        var rangeCount = ranges.Count;
        var rangeArray = ArrayPool<Range>.Shared.Rent(rangeCount);
        ranges.CopyTo(rangeArray, 0);
        int mergeStart = -1;
        for (int i = 1; i < rangeCount; i++)
        {
            int distance = rangeArray[i].Start.GetOffset(MaxRangeValue) - rangeArray[i].End.GetOffset(MaxRangeValue);
            if (distance > maxDistance)
            {
                ApplyMergeUpTo(i);
                continue;
            }
            if (mergeStart < 0)
                mergeStart = i - 1;
        }
        ApplyMergeUpTo(rangeCount);
        ArrayPool<Range>.Shared.Return(rangeArray);

        void ApplyMergeUpTo(int endI)
        {
            if (mergeStart < 0)
                return;
            for (int i = mergeStart; i < endI; i++)
                ranges.Remove(rangeArray[i]);
            var mergedRange = rangeArray[mergeStart].Start..rangeArray[endI - 1].End;
            ranges.Add(mergedRange);
        }
    }

    public void Clear() => ranges.Clear();
    public void CopyTo(Range[] array, int arrayIndex) => ranges.CopyTo(array, arrayIndex);
    public IEnumerator<Range> GetEnumerator() => ranges.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ranges.GetEnumerator();

    private class RangeComparer : IComparer<Range>
    {
        public int Compare(Range x, Range y) => x.Start.Value - y.Start.Value;
    }
}
