using System;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzre.tests;

public class TestRangeCollection
{
    [Test]
    public void Add_Simple()
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
    public void Add_Merge()
    {
        var coll = new RangeCollection
        {
            0..3,
            3..7
        };
        Assert.That(coll, Is.EqualTo(new[] { 0..7 }));
    }

    [Test]
    public void Add_OutOfOrder()
    {
        var coll = new RangeCollection
        {
            0..3,
            10..15,
            5..8,
        };
        Assert.That(coll, Is.EqualTo(new[] { 0..3, 5..8, 10..15 }));
    }

    [Test]
    public void Add_MergeOverlapping()
    {
        var coll = new RangeCollection
        {
            5..10,
            3..7
        };
        Assert.That(coll, Is.EqualTo(new[] { 3..10 }));
    }

    [Test]
    public void Add_MergeExactFit()
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
    public void Add_MergeComplex()
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
    public void AddBestFit_Simple()
    {
        var coll = new RangeCollection(10) { 0..3 };
        Assert.That(coll.AddBestFit(4), Is.EqualTo(3..7));
        Assert.That(coll, Is.EqualTo(new[] { 0..7 }));
    }

    [Test]
    public void AddBestFit_HoleInTheMiddle()
    {
        var coll = new RangeCollection { 0..3, 7..10 };
        Assert.That(coll.AddBestFit(2), Is.EqualTo(5..7));
        Assert.That(coll, Is.EqualTo(new[] { 0..3, 5..10 }));
    }

    [Test]
    public void AddBestFit_HoleAtStart()
    {
        var coll = new RangeCollection { 7..10 };
        Assert.That(coll.AddBestFit(3), Is.EqualTo(4..7));
        Assert.That(coll, Is.EqualTo(new[] { 4..10 }));
    }

    [Test]
    public void AddBestFit_TooSmallHoleAtStart()
    {
        var coll = new RangeCollection(10) { 3..5 };
        Assert.That(coll.AddBestFit(5), Is.EqualTo(5..10));
        Assert.That(coll, Is.EqualTo(new[] { 3..10 }));
    }

    [Test]
    public void AddBestFit_IgnoresHolesTooSmall()
    {
        var coll = new RangeCollection
        {
            2..5,
            7..10,
            15..20
        };
        Assert.That(coll.AddBestFit(4), Is.EqualTo(11..15));
        Assert.That(coll, Is.EqualTo(new[] { 2..5, 7..10, 11..20 }));
    }

    [Test]
    public void AddBestFit_PrefersBetterFittingHoles()
    {
        var coll = new RangeCollection { 5..10, 13..15 };
        Assert.That(coll.AddBestFit(2), Is.EqualTo(11..13));
        Assert.That(coll, Is.EqualTo(new[] { 5..10, 11..15 }));
    }

    [Test]
    public void AddBestFit_ReturnsNullOnEmptyAndTooSmall()
    {
        var coll = new RangeCollection(5);
        Assert.That(coll.AddBestFit(10), Is.Null);
        Assert.That(coll, Is.Empty);
    }

    [Test]
    public void AddBestFit_ReturnsNullOnTooSmall()
    {
        var coll = new RangeCollection(10) { 3..8 };
        Assert.That(coll.AddBestFit(9), Is.Null);
        Assert.That(coll, Is.EqualTo(new[] { 3..8 }));
    }

    [Test]
    public void Remove_NonExistant()
    {
        var coll = new RangeCollection();
        Assert.That(coll.Remove(0..5), Is.False);
        coll.Add(0..5);
        Assert.That(coll.Remove(10..15), Is.False);
    }

    [Test]
    public void Remove_Complex()
    {
        var coll = new RangeCollection
        {
            0..5,
            7..9,
            11..20
        };

        Assert.That(coll.Remove(2..18));
        Assert.That(coll, Is.EqualTo(new[] { 0..2, 18..20 }));
    }

    [Test]
    public void Remove_SingleExact()
    {
        var coll = new RangeCollection(10)
        {
            0..2,
            4..6,
            8..10
        };
        Assert.That(coll.Remove(4..6));
        Assert.That(coll, Is.EqualTo(new[] { 0..2, 8..10 }));
    }

    [Test]
    public void Remove_Partial()
    {
        var coll = new RangeCollection(10)
        {
            0..2,
            4..7,
            8..10
        };
        Assert.That(coll.Remove(5..6));
        Assert.That(coll, Is.EqualTo(new[] { 0..2, 4..5, 6..7, 8..10 }));
    }

    [Test]
    public void Remove_PartialAtStart()
    {
        var coll = new RangeCollection(10)
        {
            0..10
        };
        Assert.That(coll.Remove(0..5));
        Assert.That(coll, Is.EqualTo(new[] { 5..10 }));
    }

    [Test]
    public void Remove_PartialAtEnd()
    {
        var coll = new RangeCollection(10)
        {
            0..10
        };
        Assert.That(coll.Remove(5..10));
        Assert.That(coll, Is.EqualTo(new[] { 0..5 }));
    }

    [Test]
    public void Remove_Nothing()
    {
        var coll = new RangeCollection(10) { 2..7 };
        Assert.That(coll.Remove(5..5));
        Assert.That(coll, Is.EqualTo(new[] { 2..7 }));
    }

    [Test]
    public void Contains_Full()
    {
        var coll = new RangeCollection(5)
        {
            0..5
        };
        Assert.That(coll.Contains(0..5));
    }
    
    [Test]
    public void Contains_FullyContained()
    {
        var coll = new RangeCollection(10)
        {
            2..7
        };
        Assert.That(coll.Contains(3..5));
    }

    [Test]
    public void Contains_PartiallyContainedFront()
    {
        var coll = new RangeCollection(10)
        {
            2..7
        };
        Assert.That(coll.Contains(0..5), Is.False);
    }

    [Test]
    public void Contains_PartiallyContainedBack()
    {
        var coll = new RangeCollection(10)
        {
            2..7
        };
        Assert.That(coll.Contains(5..9), Is.False);
    }

    [Test]
    public void Contains_PartiallyContainedMiddle()
    {
        var coll = new RangeCollection(10)
        {
            2..7
        };
        Assert.That(coll.Contains(1..9), Is.False);
    }

    [Test]
    public void Contains_HoleInMiddle()
    {
        var coll = new RangeCollection(10)
        {
            2..4,
            6..8
        };
        Assert.That(coll.Contains(3..7), Is.False);
    }

    [Test]
    public void Contains_FullOut()
    {
        var coll = new RangeCollection(10)
        {
            2..4
        };
        Assert.That(coll.Contains(6..8), Is.False);
    }
    

    [Test]
    public void Contains_Empty()
    {
        var coll = new RangeCollection(10);
        Assert.That(coll.Contains(6..8), Is.False);
    }

    [Test]
    public void Intersects_Full()
    {
        var coll = new RangeCollection(5)
        {
            0..5
        };
        Assert.That(coll.Intersects(0..5));
    }

    [Test]
    public void Intersects_FullyContained()
    {
        var coll = new RangeCollection(10)
        {
            2..7
        };
        Assert.That(coll.Intersects(3..5));
    }

    [Test]
    public void Intersects_PartiallyContainedFront()
    {
        var coll = new RangeCollection(10)
        {
            2..7
        };
        Assert.That(coll.Intersects(0..5));
    }

    [Test]
    public void Intersects_PartiallyContainedBack()
    {
        var coll = new RangeCollection(10)
        {
            2..7
        };
        Assert.That(coll.Intersects(5..9));
    }

    [Test]
    public void Intersects_PartiallyContainedMiddle()
    {
        var coll = new RangeCollection(10)
        {
            2..7
        };
        Assert.That(coll.Intersects(1..9));
    }

    [Test]
    public void Intersects_HoleInMiddle()
    {
        var coll = new RangeCollection(10)
        {
            2..4,
            6..8
        };
        Assert.That(coll.Intersects(3..7));
    }

    [Test]
    public void Intersects_FullOut()
    {
        var coll = new RangeCollection(10)
        {
            2..4
        };
        Assert.That(coll.Intersects(6..8), Is.False);
    }


    [Test]
    public void Intersects_Empty()
    {
        var coll = new RangeCollection(10);
        Assert.That(coll.Intersects(6..8), Is.False);
    }

    [Test]
    public void MergeNearbyRanges_Empty()
    {
        var coll = new RangeCollection(10);
        coll.MergeNearbyRanges(5);
        Assert.That(coll, Is.Empty);
        coll.MergeNearbyRanges(20);
        Assert.That(coll, Is.Empty);
    }

    [Test]
    public void MergeNearbyRanges_Full()
    {
        var coll = new RangeCollection(10) { .. };
        coll.MergeNearbyRanges(5);
        Assert.That(coll, Is.EqualTo(new[] { 0..10 }));
        coll.MergeNearbyRanges(20);
        Assert.That(coll, Is.EqualTo(new[] { 0..10 }));
    }

    [Test]
    public void MergeNearbyRanges_SingleHoleInMiddle()
    {
        var coll = new RangeCollection(10)
        {
            0..4,
            6..10
        };
        coll.MergeNearbyRanges(5);
        Assert.That(coll, Is.EqualTo(new[] { 0..10 }));
    }

    [Test]
    public void MergeNearbyRanges_IgnoresFrontAndBack()
    {
        var coll = new RangeCollection(10)
        {
            3..4,
            6..8
        };
        coll.MergeNearbyRanges(5);
        Assert.That(coll, Is.EqualTo(new[] { 3..8 }));
    }

    [Test]
    public void MergeNearbyRanges_MultipleHoles()
    {
        var coll = new RangeCollection(10)
        {
            0..2,
            4..6,
            7..8,
            9..10
        };
        coll.MergeNearbyRanges(5);
        Assert.That(coll, Is.EqualTo(new[] { 0..10 }));
    }

    [Test]
    public void MergeNearbyRanges_MergeNothing()
    {
        var coll = new RangeCollection(10)
        {
            0..2,
            5..7,
            9..10
        };
        coll.MergeNearbyRanges(1);
        Assert.That(coll, Is.EqualTo(new[] { 0..2, 5..7, 9..10}));
    }

    [Test]
    public void MergeNearbyRanges_DoesNotMergeTooFarApart()
    {
        var coll = new RangeCollection(10)
        {
            0..1,
            3..4,
            // so hole 4..7
            7..8,
            9..10
        };
        coll.MergeNearbyRanges(2);
        Assert.That(coll, Is.EqualTo(new[] { 0..4, 7..10 }));
    }
}
