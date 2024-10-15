using System;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzre.tests;

public class TestStackOverSpan
{
    [Test]
    public void Ctor_Default()
    {
        StackOverSpan<int> stack = default;
        Assert.That(stack.Count, Is.Zero);
        Assert.That(stack.Capacity, Is.Zero);
    }

    [Test]
    public void Ctor_Empty()
    {
        StackOverSpan<int> stack = new(Span<int>.Empty);
        Assert.That(stack.Count, Is.Zero);
        Assert.That(stack.Capacity, Is.Zero);
    }

    [Test]
    public void Ctor_Array()
    {
        StackOverSpan<int> stack = new(new int[42]);
        Assert.That(stack.Count, Is.Zero);
        Assert.That(stack.Capacity, Is.EqualTo(42));
    }

    [Test]
    public void Push_Copy()
    {
        StackOverSpan<int> stack = new(new int[16]);
        Assert.That(stack.Count, Is.EqualTo(0));
        stack.Push(42);
        Assert.That(stack.Count, Is.EqualTo(1));
        Assert.That(stack.Peek(), Is.EqualTo(42));
    }

    [Test]
    public void Push_Ref()
    {
        StackOverSpan<int> stack = new(new int[16]);
        Assert.That(stack.Count, Is.EqualTo(0));
        stack.Push() = 42;
        Assert.That(stack.Count, Is.EqualTo(1));
        Assert.That(stack.Peek(), Is.EqualTo(42));
    }

    [Test]
    public void Push_OverCapacity()
    {
        Assert.That(() => 
        {
            StackOverSpan<int> stack = new(new int[2]);
            stack.Push();
            stack.Push();
            stack.Push();
        }, Throws.InvalidOperationException);
    }

    [Test]
    public void Peek_Default()
    {
        Assert.That(() =>
        {
            StackOverSpan<int> stack = default;
            _ = stack.Peek();
        }, Throws.InvalidOperationException);
    }

    [Test]
    public void Peek_Empty()
    {
        Assert.That(() =>
        {
            StackOverSpan<int> stack = new(new int[16]);
            stack.Push();
            stack.Pop();
            _ = stack.Peek();
        }, Throws.InvalidOperationException);
    }

    [Test]
    public void Peek_Order()
    {
        StackOverSpan<int> stack = new(new int[16]);
        stack.Push(42);
        Assert.That(stack.Peek(), Is.EqualTo(42));
        stack.Push() = 1337;
        Assert.That(stack.Peek(), Is.EqualTo(1337));
        Assert.That(stack.Count, Is.EqualTo(2));
    }

    [Test]
    public void Pop_Empty()
    {
        Assert.That(() =>
        {
            StackOverSpan<int> stack = new(new int[16]);
            stack.Pop();
        }, Throws.InvalidOperationException);
    }

    [Test]
    public void TryPop_Empty()
    {
        StackOverSpan<int> stack = default;
        Assert.That(stack.TryPop(out _), Is.False);
    }

    [Test]
    public void Pop_Single()
    {
        StackOverSpan<int> stack = new(new int[16]);
        Assert.That(stack.Count, Is.EqualTo(0));
        stack.Push(42);
        Assert.That(stack.Count, Is.EqualTo(1));
        Assert.That(stack.Pop(), Is.EqualTo(42));
        Assert.That(stack.Count, Is.EqualTo(0));
    }

    [Test]
    public void TryPop_Single()
    {
        StackOverSpan<int> stack = new(new int[16]);
        stack.Push(42);
        Assert.That(stack.TryPop(out var val1), Is.True);
        Assert.That(val1, Is.EqualTo(42));
        Assert.That(stack.TryPop(out _), Is.False);
    }

    [Test]
    public void Pop_Order()
    {
        StackOverSpan<int> stack = new(new int[16]);
        Assert.That(stack.Count, Is.EqualTo(0));
        stack.Push(42);
        Assert.That(stack.Count, Is.EqualTo(1));
        stack.Push(1337);
        Assert.That(stack.Count, Is.EqualTo(2));
        Assert.That(stack.Pop(), Is.EqualTo(1337));
        Assert.That(stack.Count, Is.EqualTo(1));
        Assert.That(stack.Pop(), Is.EqualTo(42));
        Assert.That(stack.Count, Is.EqualTo(0));
    }

    [Test]
    public void TryPop_Order()
    {
        StackOverSpan<int> stack = new(new int[16]);
        stack.Push(42);
        stack.Push(1337);
        Assert.That(stack.TryPop(out var val1), Is.True);
        Assert.That(val1, Is.EqualTo(1337));
        Assert.That(stack.TryPop(out var val2), Is.True);
        Assert.That(val2, Is.EqualTo(42));
        Assert.That(stack.TryPop(out _), Is.False);
    }

    [Test]
    public void Clear_Default()
    {
        StackOverSpan<int> stack = new(new int[16]);
        stack.Clear();
        Assert.That(stack.Count, Is.Zero);
    }

    [Test]
    public void Clear()
    {
        StackOverSpan<int> stack = new(new int[16]);
        stack.Push();
        stack.Push();
        stack.Push();
        Assert.That(stack.Count, Is.EqualTo(3));
        stack.Clear();
        Assert.That(stack.Count, Is.EqualTo(0));
    }
}
