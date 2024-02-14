using System;
using NUnit.Framework;
using zzio.script;

namespace zzio.tests.script;

[TestFixture]
public class TestRawInstruction
{
    [Test]
    public void syntax()
    {
        Assert.DoesNotThrow(() => new RawInstruction("a.bcd.123.wef"));
        Assert.DoesNotThrow(() => new RawInstruction("a"));
        Assert.DoesNotThrow(() => new RawInstruction("  a.\"abc\""));
        Assert.DoesNotThrow(() => new RawInstruction("a.\"\""));
        Assert.DoesNotThrow(() => new RawInstruction("a.\"abc\".def"));
        Assert.DoesNotThrow(() => new RawInstruction("a.\"abc.def\""));
        Assert.DoesNotThrow(() => new RawInstruction("a.\"abc.def.\".ghi"));
        Assert.DoesNotThrow(() => new RawInstruction("a.\"\\\"\\\"a\\bc\\\"\""));
        Assert.DoesNotThrow(() => new RawInstruction("\".abc"));
        Assert.DoesNotThrow(() => new RawInstruction("\".\"abc\""));
        Assert.DoesNotThrow(() => new RawInstruction("."));
        Assert.DoesNotThrow(() => new RawInstruction("..adef"));
        Assert.DoesNotThrow(() => new RawInstruction("..\"sdf\""));
        Assert.DoesNotThrow(() => new RawInstruction("\\.-1.-1.0"));
        Assert.DoesNotThrow(() => new RawInstruction("$.0"));
        Assert.DoesNotThrow(() => new RawInstruction("=.19"));

        Assert.Throws<ArgumentException>(() => new RawInstruction("a."));
        Assert.Throws<ArgumentException>(() => new RawInstruction(" a.bcd."));
        Assert.Throws<ArgumentException>(() => new RawInstruction("a.\"asdasd"));
        Assert.Throws<ArgumentException>(() => new RawInstruction("a.bcd.\"asd\"\""));
        Assert.Throws<ArgumentException>(() => new RawInstruction("\".\""));
        Assert.Throws<ArgumentException>(() => new RawInstruction(".."));
        Assert.Throws<ArgumentException>(() => new RawInstruction("..\"asd"));
    }

    [Test]
    public void command()
    {
        Assert.That(new RawInstruction("a").Command, Is.EqualTo("a"));
        Assert.That(new RawInstruction("a.bcd").Command, Is.EqualTo("a"));
        Assert.That(new RawInstruction("a.bcd.\"def\"").Command, Is.EqualTo("a"));
        Assert.That(new RawInstruction("setModel.f000w000").Command, Is.EqualTo("setModel"));
        Assert.That(new RawInstruction("  .").Command, Is.EqualTo("."));
        Assert.That(new RawInstruction("  ..def").Command, Is.EqualTo("."));
        Assert.That(new RawInstruction("\"").Command, Is.EqualTo("\""));
        Assert.That(new RawInstruction("\".ghi").Command, Is.EqualTo("\""));
    }

    [Test]
    public void arguments()
    {
        Assert.That(new RawInstruction("a").Arguments.Length, Is.EqualTo(0));

        var args1 = new RawInstruction("a.abc").Arguments;
        Assert.That(args1.Length, Is.EqualTo(1));
        Assert.That(args1[0], Is.EqualTo("abc"));

        var args2 = new RawInstruction("a.\"def\\\"\\\\\\n\".abc").Arguments;
        Assert.That(args2.Length, Is.EqualTo(2));
        Assert.That(args2[0], Is.EqualTo("def\"\\\n"));
        Assert.That(args2[1], Is.EqualTo("abc"));
    }
}
