using System;
using System.Buffers;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzre.tests;

public class TestPooledList
{
    private class MockedArrayPool : ArrayPool<int>
    {
        public int[]? array;
        public bool wasReturned;

        public override int[] Rent(int minimumLength)
        {
            if (array != null)
                throw new AssertionException("Attempted to rent more than one array");
            return array = new int[minimumLength];
        }

        public override void Return(int[] array, bool clearArray = false)
        {
            if (this.array == null)
                throw new AssertionException("Attempted to return array before renting");
            if (!ReferenceEquals(array, this.array))
                throw new AssertionException("Attempted to return different array than rented");
            if (wasReturned)
                throw new AssertionException("Attempted to return array more than once");
            wasReturned = true;
        }
    }

    [Test]
    public void Ctor_RentsAndReturnsOnce()
    {
        var pool = new MockedArrayPool();
        using (var list = new PooledList<int>(16, pool))
            list.Dispose();
        Assert.That(pool.array, Is.Not.Null, "Array was never rented");
        Assert.That(pool.wasReturned, "Array was not returned");
    }

    [Test]
    public void Ctor_SharedPool()
    {
        using PooledList<int> list = new(16);
    }

    [Test]
    public void Ctor_CapacityIsNeverLess([Values(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 32, 33, 1024, 1025)] int minCapacity)
    {
        using PooledList<int> list = new(minCapacity);
        Assert.That(list.Capacity, Is.AtLeast(minCapacity));
    }

    [Test]
    public void Ctor_Default()
    {
        using PooledList<int> list = default;
    }

    [Test]
    public void Ctor_CustomArray([Values(0, 1, 42)] int arrayLength)
    {
        using PooledList<int> list = new(new int[arrayLength]);
    }

    [Test]
    public void Add_Copy()
    {
        using PooledList<int> list = new(16);
        Assert.That(list.Count, Is.Zero);
        list.Add(42);
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0], Is.EqualTo(42));
    }

    [Test]
    public void Add_Ref()
    {
        using PooledList<int> list = new(16);
        Assert.That(list.Count, Is.Zero);
        list.Add() = 42;
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0], Is.EqualTo(42));
    }

    [Test]
    public void Add_Order()
    {
        using PooledList<int> list = new(16);
        list.Add() = 1337;
        list.Add() = 42;
        Assert.That(list[1], Is.EqualTo(42));
        Assert.That(list[0], Is.EqualTo(1337));
    }

    [Test]
    public void Add_OverCapacity()
    {
        using PooledList<int> list = new(new int[2]);
        Assert.That(list.Count, Is.EqualTo(0));
        list.Add();
        list.Add();
        Assert.That(list.Count, Is.EqualTo(2));
        Assert.That(() => list.Add(), Throws.InvalidOperationException);
    }

    [Test]
    public void Clear_Empty()
    {
        using PooledList<int> list = new(16);
        Assert.That(list.Count, Is.Zero);
        list.Clear();
        Assert.That(list.Count, Is.Zero);
    }

    [Test]
    public void Clear()
    {
        using PooledList<int> list = new(16);
        list.Add(42);
        Assert.That(list.Count, Is.EqualTo(1));
        list.Clear();
        Assert.That(list.Count, Is.Zero);
        list.Add(1337);
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0], Is.EqualTo(1337));
    }

    [Test]
    public void Index_OutOfBounds()
    {
        using PooledList<int> list = new(16);
        Assert.That(() => list[0], Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => list[-1], Throws.InstanceOf<ArgumentOutOfRangeException>());
        list.Add();
        Assert.That(() => list[0], Throws.Nothing);
        Assert.That(() => list[1], Throws.InstanceOf<ArgumentOutOfRangeException>());
        list.Add();
        Assert.That(() => list[0], Throws.Nothing);
        Assert.That(() => list[1], Throws.Nothing);
        Assert.That(() => list[2], Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => list[-1], Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Enumerator_Empty()
    {
        using PooledList<int> list = new(16);
        using var e = list.GetEnumerator();
        Assert.That(e.MoveNext(), Is.False);
        Assert.That(e.MoveNext(), Is.False);
    }

    [Test]
    public void Enumerator_Once()
    {
        using PooledList<int> list = new(16);
        list.Add() = 42;
        using var e = list.GetEnumerator();
        Assert.That(e.MoveNext(), Is.True);
        Assert.That(e.Current, Is.EqualTo(42));
        Assert.That(e.MoveNext(), Is.False);
        Assert.That(e.MoveNext(), Is.False);
    }

    [Test]
    public void Enumerator_Twice()
    {
        using PooledList<int> list = new(16);
        list.Add() = 42;
        list.Add() = 1337;
        using var e = list.GetEnumerator();
        Assert.That(e.MoveNext(), Is.True);
        Assert.That(e.Current, Is.EqualTo(42));
        Assert.That(e.MoveNext(), Is.True);
        Assert.That(e.Current, Is.EqualTo(1337));
        Assert.That(e.MoveNext(), Is.False);
    }

    [Test]
    public void Enumerator_Interleaved()
    {
        using PooledList<int> list = new(16);
        list.Add() = 42;
        list.Add() = 1337;
        using var e0 = list.GetEnumerator();
        using var e1 = list.GetEnumerator();
        Assert.That(e1.MoveNext(), Is.True);
        using var e2 = list.GetEnumerator();
        Assert.That(e2.MoveNext(), Is.True);
        Assert.That(e2.MoveNext(), Is.True);
        using var e3 = list.GetEnumerator();
        Assert.That(e3.MoveNext(), Is.True);
        Assert.That(e3.MoveNext(), Is.True);
        Assert.That(e3.MoveNext(), Is.False);

        Assert.That(e2.Current, Is.EqualTo(1337));
        Assert.That(e1.Current, Is.EqualTo(42));
    }
}
