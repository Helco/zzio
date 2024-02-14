using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using zzio;

namespace zzre;

public class RangeCollection : ICollection<Range>, IReadOnlyCollection<Range>
{
    private readonly List<Range> ranges = [];

    public bool IsReadOnly => false;
    public int Count => ranges.Count;
    public Range Total => ranges.Any()
        ? ranges.First().Start..ranges.Last().End
        : 0..0;
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
        var itemEndOffset = itemOffset + itemLength;
        if (ranges.Count == 0 || (itemOffset <= MinValue && itemEndOffset >= MaxValue))
        {
            ranges.Clear();
            ranges.Add(itemOffset..itemEndOffset);
            return;
        }

        var nearItem = Math.Max(0, itemOffset - 1)..Math.Min(MaxRangeValue, itemEndOffset + 1);
        var (firstRangeI, endRangeI) = FindIntersections(nearItem);
        if (firstRangeI >= 0)
        {
            var mergedStart = Math.Min(itemOffset, ranges[firstRangeI].Start.GetOffset(MaxRangeValue));
            var mergedEnd = Math.Max(itemEndOffset, ranges[endRangeI - 1].End.GetOffset(MaxRangeValue));
            if (firstRangeI + 1 < ranges.Count)
                ranges.RemoveRange(firstRangeI + 1, endRangeI - firstRangeI - 1);
            ranges[firstRangeI] = mergedStart..mergedEnd;
        }
        else
            ranges.Insert(~firstRangeI, itemOffset..itemEndOffset);
    }

    public bool Remove(Range remove)
    {
        var removeStart = remove.Start.GetOffset(MaxRangeValue);
        var removeEnd = remove.End.GetOffset(MaxRangeValue);
        if (ranges.Count == 0)
            return false;
        if (removeStart <= MinValue && removeEnd >= MaxValue)
        {
            Clear();
            return true;
        }

        var (firstRangeI, endRangeI) = FindIntersections(removeStart..removeEnd);
        if (firstRangeI < 0)
            return false;
        var lastRangeI = endRangeI - 1;

        int startValue = ranges[firstRangeI].Start.Value;
        int endValue = ranges[lastRangeI].End.Value;
        int firstFullI = startValue >= removeStart ? firstRangeI : firstRangeI + 1;
        int lastFullI = endValue <= removeEnd ? lastRangeI : lastRangeI - 1;
        if (firstFullI <= lastFullI)
            ranges.RemoveRange(firstFullI, lastFullI - firstFullI + 1);

        // Modify the partial removed ranges
        if (firstFullI != firstRangeI)
            ranges[firstRangeI] = startValue..removeStart;
        if (lastFullI != lastRangeI)
        {
            if (firstRangeI == lastRangeI)
                ranges.Insert(firstRangeI + 1, removeEnd..endValue);
            else
                ranges[firstRangeI + 1] = removeEnd..endValue;
        }

        return true;
    }

    public Range? AddBestFit(int length)
    {
        // This will only add a new range if empty, otherwise it will only
        // mutate a single range and maybe remove another one

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (!ranges.Any())
        {
            if (MaxRangeValue < length)
                return null;
            ranges.Add(0..length);
            return 0..length;
        }

        var lastEnd = 0;
        int bestI = -1;
        int bestLength = int.MaxValue;
        for (int rangeI = 0; rangeI < ranges.Count; rangeI++)
        {
            var curStart = ranges[rangeI].Start.GetOffset(MaxRangeValue);
            var curEnd = ranges[rangeI].End.GetOffset(MaxRangeValue);

            int curHoleLength = curStart - lastEnd;
            if (curHoleLength >= length && curHoleLength < bestLength)
            {
                bestI = rangeI;
                bestLength = curHoleLength;
            }
            if (bestLength == length)
                break; // it does not get better than optimal

            lastEnd = curEnd;
        }

        if (bestI < 0) // no space *before* another range found
        {
            if (MaxValue + length <= MaxRangeValue)
            {
                var newRangeAtEnd = ranges[^1].End..(ranges[^1].End.Value + length);
                ranges[^1] = ranges[^1].Start..(MaxValue + length + 1);
                return newRangeAtEnd;
            }
            return null;
        }

        var newRange = (ranges[bestI].Start.Value - length)..ranges[bestI].Start;
        if (bestI > 0 && ranges[bestI - 1].End.Value == newRange.Start.Value)
        {
            // fully merge two existing ranges
            ranges[bestI - 1] = ranges[bestI - 1].Start..ranges[bestI].End;
            ranges.RemoveAt(bestI);
        }
        else
            ranges[bestI] = newRange.Start..ranges[bestI].End;
        return newRange;
    }

    public bool Contains(Range item)
    {
        if (ranges.Count == 0)
            return false;
        var rangeI = FindRangeIndexContaining(item.Start.GetOffset(MaxRangeValue));
        return rangeI >= 0 && Contains(inner: item, outer: ranges[rangeI]);
    }

    public bool Intersects(Range item) =>
        ranges.Count > 0 && FindIntersections(item).firstI >= 0;

    private int FindRangeIndexContaining(int point, int startRangeI = 0) =>
        FindFirstRangeIndexIntersecting(point, point + 1, startRangeI);

    private int FindFirstRangeIndexIntersecting(Range range, int startRangeI = 0) =>
        FindFirstRangeIndexIntersecting(range.Start.GetOffset(MaxRangeValue), range.End.GetOffset(MaxRangeValue), startRangeI);

    private int FindFirstRangeIndexIntersecting(int startOffset, int endOffset, int startRangeI = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startRangeI, ranges.Count);
        int left = startRangeI;
        int right = ranges.Count - 1;
        int middle;
        do
        {
            middle = (left + right) / 2;
            int comparison = Compare(startOffset, endOffset, ranges[middle]);
            if (comparison == 0)
            {
                if (startOffset + 1 == endOffset)
                    return middle;
                else
                    right = middle; // now there could be an earlier range that already intersects 
            }
            else
            {
                left = comparison > 0 ? middle + 1 : left;
                right = comparison < 0 ? middle - 1 : right;
            }
        } while (left < right);
        middle = (left + right) / 2;
        return Compare(startOffset, endOffset, ranges[middle]) switch
        {
            0 => middle,
            < 0 => ~middle, // Like .NET we return the bit-inverted index of the next greater index
            > 0 => ~(middle + 1)
        };
    }

    private (int firstI, int endI) FindIntersections(Range search)
    {
        int firstI = FindFirstRangeIndexIntersecting(search);
        int lastI = FindRangeIndexContaining(search.End.GetOffset(MaxRangeValue), Math.Max(0, firstI));
        if (firstI >= 0)
            // the search end might land into empty land, but we have at least a single intersection
            return (firstI, lastI < 0 ? ~lastI : lastI + 1);

        // lastI has to be < 0 otherwise we would be intersecting and we would not get here
        return (lastI, lastI);
    }

    private int Compare(int startOffset, int endOffset, Range r2) =>
        endOffset <= r2.Start.GetOffset(MaxRangeValue) ? -1
        : startOffset >= r2.End.GetOffset(MaxRangeValue) ? 1
        : 0;

    private bool Contains(Range inner, Range outer) =>
        inner.Start.GetOffset(MaxRangeValue) >= outer.Start.GetOffset(MaxRangeValue) &&
        inner.End.GetOffset(MaxRangeValue) <= outer.End.GetOffset(MaxRangeValue);

    public void MergeNearbyRanges(int maxDistance)
    {
        // this method is only ever compacting the ranges, so we can work in-place
        if (maxDistance < 1 || ranges.Count < 2)
            return;

        int readI = 1, writeI = 0;
        int mergeStart = -1;
        for (; readI < ranges.Count; readI++)
        {
            int distance = ranges[readI].Start.GetOffset(MaxRangeValue) - ranges[readI - 1].End.GetOffset(MaxRangeValue);
            if (distance > maxDistance)
                ApplyMerge();
            else if (mergeStart < 0)
                mergeStart = readI - 1;
        }
        ApplyMerge();
        ranges.RemoveRange(writeI, ranges.Count - writeI);

        void ApplyMerge()
        {
            if (mergeStart < 0)
                return;
            ranges[writeI++] = ranges[mergeStart].Start..ranges[readI - 1].End;
            mergeStart = -1;
        }
    }

    public void Clear() => ranges.Clear();
    public void CopyTo(Range[] array, int arrayIndex) => ranges.CopyTo(array, arrayIndex);
    public IEnumerator<Range> GetEnumerator() => ranges.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ranges.GetEnumerator();

    private sealed class RangeComparer : IComparer<Range>
    {
        public int Compare(Range x, Range y) => x.Start.Value - y.Start.Value;
    }
}
