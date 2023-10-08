using System;
using NUnit.Framework;

namespace zzre.core.tests;

public class TestOptionalReference
{
    [Test]
    public void ConstructEmpty()
    {
        new OptionalReference<int>();
    }

    [Test]
    public void ConstructFull()
    {
        int memory = 42;
        new OptionalReference<int>(ref memory);
    }

    [Test]
    public void HasValueEmpty()
    {
        var empty = new OptionalReference<int>();
        Assert.That(empty.HasValue, Is.False);
    }

    [Test]
    public void HasValueFull()
    {
        int memory = 42;
        var full = new OptionalReference<int>(ref memory);
        Assert.That(full.HasValue, Is.True);
    }

    [Test]
    public void ValueEmpty()
    {
        Assert.That(() =>
        {
            var empty = new OptionalReference<int>();
            int value = empty.Value;
        }, Throws.InstanceOf<NullReferenceException>());
    }

    [Test]
    public void ValueFull()
    {
        int memory = 42;
        var full = new OptionalReference<int>(ref memory);
        Assert.That(full.Value, Is.EqualTo(42));

        full.Value = 1337;
        Assert.That(full.Value, Is.EqualTo(1337));
        Assert.That(memory, Is.EqualTo(1337));
    }

    [Test]
    public void GetValueOrDefaultEmpty()
    {
        var empty = new OptionalReference<int>();
        Assert.That(empty.GetValueOrDefault(), Is.EqualTo(0));
        Assert.That(empty.GetValueOrDefault(42), Is.EqualTo(42));
    }

    [Test]
    public void GetValueOrDefaultFull()
    {
        int memory = 1337;
        var full = new OptionalReference<int>(ref memory);
        Assert.That(full.GetValueOrDefault(), Is.EqualTo(1337));
        Assert.That(full.GetValueOrDefault(42), Is.EqualTo(1337));
    }

    [Test]
    public void TrySetValueEmpty()
    {
        var empty = new OptionalReference<int>();
        Assert.That(empty.TrySetValue(42), Is.False);
    }

    [Test]
    public void TrySetValueFull()
    {
        int memory = 42;
        var full = new OptionalReference<int>(ref memory);
        Assert.That(full.TrySetValue(1337), Is.True);
        Assert.That(full.Value, Is.EqualTo(1337));
        Assert.That(memory, Is.EqualTo(1337));
    }

    private struct A
    {
        public int V1, V2;
    }

    [Test]
    public void Struct()
    {
        A memory = new() { V1 = 1, V2 = 2 };
        var full = new OptionalReference<A>(ref memory);

        Assert.That(full.Value.V1, Is.EqualTo(1));
        Assert.That(full.Value.V2, Is.EqualTo(2));

        full.Value.V1 = 42;
        Assert.That(full.Value.V1, Is.EqualTo(42));
        Assert.That(full.Value.V2, Is.EqualTo(2));
    }

    private class B
    {
        public int V1, V2;
    }

    [Test]
    public void Class()
    {
        B memory = new() { V1 = 1, V2 = 2 };
        var full = new OptionalReference<B>(ref memory);

        Assert.That(full.Value.V1, Is.EqualTo(1));
        Assert.That(full.Value.V2, Is.EqualTo(2));

        full.Value.V1 = 42;
        Assert.That(full.Value.V1, Is.EqualTo(42));
        Assert.That(full.Value.V2, Is.EqualTo(2));
    }

    [Test]
    public void Comparisons()
    {
        int a1 = 42, a2 = 42, b = 1337;
        var empty = new OptionalReference<int>();
        var fullA1 = new OptionalReference<int>(ref a1);
        var fullA2 = new OptionalReference<int>(ref a2);
        var fullB = new OptionalReference<int>(ref b);
        
        void Compare(in OptionalReference<int> x, in OptionalReference<int> y, bool expected)
        {
            Assert.That(x == y, expected ? Is.True : Is.False);
            Assert.That(y == x, expected ? Is.True : Is.False);
            Assert.That(x != y, expected ? Is.False : Is.True);
            Assert.That(y != x, expected ? Is.False : Is.True);
        }
        void CompareInt(in OptionalReference<int> x, int y, bool expected)
        {
            Assert.That(x == y, expected ? Is.True : Is.False);
            Assert.That(y == x, expected ? Is.True : Is.False);
            Assert.That(x != y, expected ? Is.False : Is.True);
            Assert.That(y != x, expected ? Is.False : Is.True);
            Assert.That(x.Equals(y), expected ? Is.True : Is.False);
        }

        Compare(empty, empty, true);
        Compare(empty, fullA1, false);
        Compare(empty, fullA2, false);
        Compare(empty, fullB, false);

        Compare(fullA1, empty, false);
        Compare(fullA1, fullA1, true);
        Compare(fullA1, fullA2, true);
        Compare(fullA1, fullB, false);

        Compare(fullA2, empty, false);
        Compare(fullA2, fullA1, true);
        Compare(fullA2, fullA2, true);
        Compare(fullA2, fullB, false);

        Compare(fullB, empty, false);
        Compare(fullB, fullA1, false);
        Compare(fullB, fullA2, false);
        Compare(fullB, fullB, true);

        CompareInt(empty, 42, false);
        CompareInt(fullA1, 42, true);
        CompareInt(fullA2, 42, true);
        CompareInt(fullB, 42, false);

        CompareInt(empty, 1337, false);
        CompareInt(fullA1, 1337, false);
        CompareInt(fullA2, 1337, false);
        CompareInt(fullB, 1337, true);

        Assert.That(empty.Equals(null), Is.True);
        Assert.That(fullA1.Equals(null), Is.False);
        Assert.That(fullA2.Equals(null), Is.False);
        Assert.That(fullB.Equals(null), Is.False);

        Assert.That(empty.Equals("abc"), Is.False);
        Assert.That(fullA1.Equals("abc"), Is.False);
        Assert.That(fullA2.Equals("abc"), Is.False);
        Assert.That(fullB.Equals("abc"), Is.False);
    }

    [Test]
    public void HashEmpty()
    {
        var empty = new OptionalReference<int>();
        Assert.That(empty.GetHashCode(), Is.Zero);
    }

    [Test]
    public void HashFull()
    {
        int memory = 42;
        var full = new OptionalReference<int>(ref memory);
        Assert.That(full.GetHashCode(), Is.EqualTo(memory.GetHashCode()));
    }
}
