using System;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzre.tests;

public class TestRangeCollection
{
    [Test]
    public void AddSimple()
    {
        var coll = new RangeCollection();
        Assert.AreEqual(Array.Empty<Range>(), coll);
        coll.Add(0..1);
        coll.Add(6..9);
        Assert.AreEqual(new[] { 0..1, 6..9 }, coll);
        Assert.AreEqual(2, coll.Count);
        Assert.AreEqual(0..9, coll.Total);
    }

    [Test]
    public void AddMerge()
    {
        var coll = new RangeCollection
        {
            0..3,
            3..7
        };
        Assert.AreEqual(new[] { 0..7 }, coll);
    }

    [Test]
    public void AddMergeOverlapping()
    {
        var coll = new RangeCollection
        {
            5..10,
            3..7
        };
        Assert.AreEqual(new[] { 3..10 }, coll);
    }

    [Test]
    public void AddMergeExactFit()
    {
        var coll = new RangeCollection
        {
            0..3,
            7..10,
            3..7
        };
        Assert.AreEqual(new[] { 0..10 }, coll);
    }

    [Test]
    public void AddMergeComplex()
    {
        var coll = new RangeCollection
        {
            0..3,
            5..8,
            10..15,

            2..17
        };
        Assert.AreEqual(new[] { 0..17 }, coll);
    }

    [Test]
    public void AddBestFit()
    {
        // Finds hole in the middle
        var coll = new RangeCollection { 0..3, 7..10 };
        Assert.AreEqual(3..5, coll.AddBestFit(2));
        Assert.AreEqual(new[] { 0..5, 7..10 }, coll);

        // Finds hole at the start
        coll = new RangeCollection { 7..10 };
        Assert.AreEqual(0..3, coll.AddBestFit(3));
        Assert.AreEqual(new[] { 0..3, 7..10 }, coll);

        // Ignores holes that are too small
        coll = new RangeCollection { 2..5, 7..10, 15..20 };
        Assert.AreEqual(10..14, coll.AddBestFit(4));
        Assert.AreEqual(new[] { 2..5, 7..14, 15..20 }, coll);

        // Preferes better fitting holes
        coll = new RangeCollection { 5..10, 13..15 };
        Assert.AreEqual(10..12, coll.AddBestFit(2));
        Assert.AreEqual(new[] { 5..12, 13..15 }, coll);

        // Returns null on empty and too small
        coll = new RangeCollection(5);
        Assert.IsNull(coll.AddBestFit(10));
        Assert.IsEmpty(coll);

        // Returns null on too small
        coll = new RangeCollection(10) { 3..8 };
        Assert.IsNull(coll.AddBestFit(9));
        Assert.AreEqual(new[] { 3..8 }, coll);
    }

    [Test]
    public void RemoveNonExistant()
    {
        var coll = new RangeCollection();
        Assert.False(coll.Remove(0..5));
        coll.Add(0..5);
        Assert.False(coll.Remove(10..15));
    }

    [Test]
    public void RemoveComplex()
    {
        var coll = new RangeCollection
        {
            0..5,
            7..9,
            11..20
        };

        Assert.True(coll.Remove(2..18));
        Assert.AreEqual(new[] { 0..2, 18..20 }, coll);
    }
}
