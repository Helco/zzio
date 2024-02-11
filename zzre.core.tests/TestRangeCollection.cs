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
        Assert.That(coll, Is.EqualTo(Array.Empty<Range>()));
        coll.Add(0..1);
        coll.Add(6..9);
        Assert.That(coll, Is.EqualTo(new[] { 0..1, 6..9 }));
        Assert.That(coll.Count, Is.EqualTo(2));
        Assert.That(coll.Total, Is.EqualTo(0..9));
    }

    [Test]
    public void AddMerge()
    {
        var coll = new RangeCollection
        {
            0..3,
            3..7
        };
        Assert.That(coll, Is.EqualTo(new[] { 0..7 }));
    }

    [Test]
    public void AddMergeOverlapping()
    {
        var coll = new RangeCollection
        {
            5..10,
            3..7
        };
        Assert.That(coll, Is.EqualTo(new[] { 3..10 }));
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
        Assert.That(coll, Is.EqualTo(new[] { 0..10 }));
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
        Assert.That(coll, Is.EqualTo(new[] { 0..17 }));
    }

    [Test]
    public void AddBestFit()
    {
        // Finds hole in the middle
        var coll = new RangeCollection { 0..3, 7..10 };
        Assert.That(coll.AddBestFit(2), Is.EqualTo(3..5));
        Assert.That(coll, Is.EqualTo(new[] { 0..5, 7..10 }));

        // Finds hole at the start
        coll = new RangeCollection { 7..10 };
        Assert.That(coll.AddBestFit(3), Is.EqualTo(0..3));
        Assert.That(coll, Is.EqualTo(new[] { 0..3, 7..10 }));

        // Ignores holes that are too small
        coll = new RangeCollection { 2..5, 7..10, 15..20 };
        Assert.That(coll.AddBestFit(4), Is.EqualTo(10..14));
        Assert.That(coll, Is.EqualTo(new[] { 2..5, 7..14, 15..20 }));

        // Preferes better fitting holes
        coll = new RangeCollection { 5..10, 13..15 };
        Assert.That(coll.AddBestFit(2), Is.EqualTo(10..12));
        Assert.That(coll, Is.EqualTo(new[] { 5..12, 13..15 }));

        // Returns null on empty and too small
        coll = new RangeCollection(5);
        Assert.IsNull(coll.AddBestFit(10));
        Assert.IsEmpty(coll);

        // Returns null on too small
        coll = new RangeCollection(10) { 3..8 };
        Assert.IsNull(coll.AddBestFit(9));
        Assert.That(coll, Is.EqualTo(new[] { 3..8 }));
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
        Assert.That(coll, Is.EqualTo(new[] { 0..2, 18..20 }));
    }
}
