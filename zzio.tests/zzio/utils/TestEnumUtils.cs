using System;
using NUnit.Framework;

namespace zzio.tests.utils;

[TestFixture]
public class TestEnumUtils
{
    private enum TestEnum
    {
        A = 0,
        B = 100,
        C = 300,
        Unknown = -1
    }

    [Flags]
    private enum TestFlags
    {
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 5
    }

    [Test]
    public void intToEnum()
    {
        Assert.That(EnumUtils.intToEnum<TestEnum>(0), Is.EqualTo(TestEnum.A));
        Assert.That(EnumUtils.intToEnum<TestEnum>(100), Is.EqualTo(TestEnum.B));
        Assert.That(EnumUtils.intToEnum<TestEnum>(200), Is.EqualTo(TestEnum.Unknown));
        Assert.That(EnumUtils.intToEnum<TestEnum>(-1), Is.EqualTo(TestEnum.Unknown));
    }

    [Test]
    public void intToFlags()
    {
        Assert.That(EnumUtils.intToFlags<TestFlags>(0).ToString(), Is.EqualTo("0"));
        Assert.That(EnumUtils.intToFlags<TestFlags>(1).ToString(), Is.EqualTo("A"));
        Assert.That(EnumUtils.intToFlags<TestFlags>(1 + 2 + 32).ToString(), Is.EqualTo("A, B, C"));
        Assert.That(EnumUtils.intToFlags<TestFlags>(1 + 2 + 4).ToString(), Is.EqualTo("A, B"));
        Assert.That(EnumUtils.intToFlags<TestFlags>(4 + 16).ToString(), Is.EqualTo("0"));
    }
}
