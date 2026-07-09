using System;
using NUnit.Framework;

namespace zzre.tests;

public sealed class TestOnceAction
{
    [Test]
    public void TestEmpty()
    {
        var a1 = new OnceAction();
        Assert.That(a1.Invoke, Throws.Nothing);
        Assert.That(a1.Invoke, Throws.Nothing); // twice for good luck

        var a2 = new OnceAction<int>();
        Assert.That(() => a2.Invoke(42), Throws.Nothing);
        var a3 = new OnceAction<int, bool>();
        Assert.That(() => a3.Invoke(42, false), Throws.Nothing);
        var a4 = new OnceAction<int, bool, string>();
        Assert.That(() => a4.Invoke(42, false, "hello"), Throws.Nothing);
    }

    [Test]
    public void TestActionCalledOnce()
    {
        int callCount = 0;
        var a = new OnceAction();

        a.Next += () => callCount++;
        a.Invoke();
        Assert.That(callCount, Is.EqualTo(1));
        a.Invoke();
        Assert.That(callCount, Is.EqualTo(1));

        a.Next += () => callCount++;
        a.Invoke();
        Assert.That(callCount, Is.EqualTo(2));
        a.Invoke();
        Assert.That(callCount, Is.EqualTo(2));
    }

    [Test]
    public void TestMultipleActionsCalledInOrder()
    {
        int callCount1 = 0;
        int callCount2 = 0;
        var a = new OnceAction();

        a.Next += () =>
        {
            Assert.That(callCount1, Is.EqualTo(callCount2));
            callCount1++;
        };
        a.Next += () =>
        {
            Assert.That(callCount2 + 1, Is.EqualTo(callCount1));
            callCount2++;
        };
        a.Invoke();

        Assert.That(callCount1, Is.EqualTo(1));
        Assert.That(callCount2, Is.EqualTo(1));
        a.Invoke();
        Assert.That(callCount1, Is.EqualTo(1));
        Assert.That(callCount2, Is.EqualTo(1));
    }

    [Test]
    public void TestIsEmpty()
    {
        var a = new OnceAction();

        Assert.That(a.IsEmpty, Is.True);
        a.Next += () => { };
        Assert.That(a.IsEmpty, Is.False);
        a.Invoke();
        Assert.That(a.IsEmpty, Is.True);

        var a1 = new OnceAction<int>();
        Assert.That(a1.IsEmpty, Is.True);
        var a2 = new OnceAction<int, bool>();
        Assert.That(a2.IsEmpty, Is.True);
        var a3 = new OnceAction<int, bool, string>();
        Assert.That(a3.IsEmpty, Is.True);
    }

    [Test]
    public void TestParameters()
    {
        int callCount = 0;

        var a1 = new OnceAction<int>();
        a1.Next += i => { Assert.That(i, Is.EqualTo(42)); callCount++; };
        a1.Next += i => Assert.That(i, Is.EqualTo(42));
        a1.Invoke(42);
        a1.Invoke(1337);
        Assert.That(callCount, Is.EqualTo(1));

        var a2 = new OnceAction<int, bool>();
        a2.Next += (i, b) =>
        {
            Assert.That(i, Is.EqualTo(1337));
            Assert.That(b, Is.True);
            callCount++;
        };
        a2.Invoke(1337, true);
        Assert.That(callCount, Is.EqualTo(2));

        var a3 = new OnceAction<int, bool, string>();
        a3.Next += (i, b, s) =>
        {
            Assert.That(i, Is.EqualTo(1337));
            Assert.That(b, Is.True);
            Assert.That(s, Is.EqualTo("hello"));
            callCount++;
        };
        a3.Invoke(1337, true, "hello");
        Assert.That(callCount, Is.EqualTo(3));
    }
}
