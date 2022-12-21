using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
        var intersections = FindIntersections(remove).ToArray();
        var removeStart = remove.Start.GetOffset(MaxRangeValue);
        var removeEnd = remove.End.GetOffset(MaxRangeValue);
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

    public void Clear() => ranges.Clear();
    public void CopyTo(Range[] array, int arrayIndex) => ranges.CopyTo(array, arrayIndex);
    public IEnumerator<Range> GetEnumerator() => ranges.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ranges.GetEnumerator();

    private class RangeComparer : IComparer<Range>
    {
        public int Compare(Range x, Range y) => x.Start.Value - y.Start.Value;
    }
}
