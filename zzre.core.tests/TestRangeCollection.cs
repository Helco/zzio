using System;
using NUnit.Framework;

namespace zzre.tests
{
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
}
