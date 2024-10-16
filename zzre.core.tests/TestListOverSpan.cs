using System;
using System.Buffers;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzre.tests;

public class TestListOverSpan
{

    [Test]
    public void Ctor_Default()
    {
        ListOverSpan<int> list = default;
    }

    [Test]
    public void Ctor_Array([Values(0, 1, 42)] int arrayLength)
    {
        var array = new int[arrayLength];
        ListOverSpan<int> list = new(array);
    }

    [Test]
    public void Ctor_Count()
    {
        var array = new int[4];
        ListOverSpan<int> list = new(array, 0);
        Assert.That(list.Count, Is.EqualTo(0));
        list = new(array.AsSpan(), 2);
        Assert.That(list.Count, Is.EqualTo(2));
        list = new(array.AsSpan(), 4);
        Assert.That(list.Count, Is.EqualTo(4));

        Assert.That(() => new ListOverSpan<int>(array, -1),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => new ListOverSpan<int>(array, 5),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Add_Copy()
    {
        ListOverSpan<int> list = new(new int[16]);
        Assert.That(list.Count, Is.Zero);
        list.Add(42);
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0], Is.EqualTo(42));
    }

    [Test]
    public void Add_Ref()
    {
        ListOverSpan<int> list = new(new int[16]);
        Assert.That(list.Count, Is.Zero);
        list.Add() = 42;
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0], Is.EqualTo(42));
    }

    [Test]
    public void Add_Order()
    {
        ListOverSpan<int> list = new(new int[16]);
        list.Add() = 1337;
        list.Add() = 42;
        Assert.That(list[1], Is.EqualTo(42));
        Assert.That(list[0], Is.EqualTo(1337));
    }

    [Test]
    public void Add_CountIncreases()
    {
        ListOverSpan<int> list = new(new int[2]);
        Assert.That(list.Count, Is.EqualTo(0));
        list.Add();
        list.Add();
        Assert.That(list.Count, Is.EqualTo(2));
    }

    [Test]
    public void Add_OverCapacity()
    {
        Assert.That(() =>
        {
            ListOverSpan<int> list = new(new int[2]);
            list.Add();
            list.Add();
            list.Add();
        }, Throws.InvalidOperationException);
    }

    [Test]
    public void Clear_Empty()
    {
        ListOverSpan<int> list = new(new int[16]);
        Assert.That(list.Count, Is.Zero);
        list.Clear();
        Assert.That(list.Count, Is.Zero);
    }

    [Test]
    public void Clear()
    {
        ListOverSpan<int> list = new(new int[16]);
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
        Assert.That(() => new ListOverSpan<int>(new int[16])[0], Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => new ListOverSpan<int>(new int[16])[-1], Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => 
        {
            var list = new ListOverSpan<int>(new int[16]);
            list.Add();
            _ = list[0];
        }, Throws.Nothing);
        Assert.That(() => 
        {
            var list = new ListOverSpan<int>(new int[16]);
            list.Add();
            _ = list[1];
        }, Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => 
        {
            var list = new ListOverSpan<int>(new int[16]);
            list.Add();
            list.Add();
            _ = list[1];
        }, Throws.Nothing);
        Assert.That(() => 
        {
            var list = new ListOverSpan<int>(new int[16]);
            list.Add();
            list.Add();
            _ = list[2];
        }, Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => 
        {
            var list = new ListOverSpan<int>(new int[16]);
            list.Add();
            list.Add();
            _ = list[-1];
        }, Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Enumerator_Empty()
    {
        ListOverSpan<int> list = new(new int[16]);
        var e = list.GetEnumerator();
        Assert.That(e.MoveNext(), Is.False);
        Assert.That(e.MoveNext(), Is.False);
    }

    [Test]
    public void Enumerator_Once()
    {
        ListOverSpan<int> list = new(new int[16]);
        list.Add() = 42;
        var e = list.GetEnumerator();
        Assert.That(e.MoveNext(), Is.True);
        Assert.That(e.Current, Is.EqualTo(42));
        Assert.That(e.MoveNext(), Is.False);
        Assert.That(e.MoveNext(), Is.False);
    }

    [Test]
    public void Enumerator_Twice()
    {
        ListOverSpan<int> list = new(new int[16]);
        list.Add() = 42;
        list.Add() = 1337;
        var e = list.GetEnumerator();
        Assert.That(e.MoveNext(), Is.True);
        Assert.That(e.Current, Is.EqualTo(42));
        Assert.That(e.MoveNext(), Is.True);
        Assert.That(e.Current, Is.EqualTo(1337));
        Assert.That(e.MoveNext(), Is.False);
    }

    [Test]
    public void Enumerator_Interleaved()
    {
        ListOverSpan<int> list = new(new int[16]);
        list.Add() = 42;
        list.Add() = 1337;
        var e0 = list.GetEnumerator();
        var e1 = list.GetEnumerator();
        Assert.That(e1.MoveNext(), Is.True);
        var e2 = list.GetEnumerator();
        Assert.That(e2.MoveNext(), Is.True);
        Assert.That(e2.MoveNext(), Is.True);
        var e3 = list.GetEnumerator();
        Assert.That(e3.MoveNext(), Is.True);
        Assert.That(e3.MoveNext(), Is.True);
        Assert.That(e3.MoveNext(), Is.False);

        Assert.That(e2.Current, Is.EqualTo(1337));
        Assert.That(e1.Current, Is.EqualTo(42));
    }
}
