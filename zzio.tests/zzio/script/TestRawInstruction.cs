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

        Assert.Throws<InvalidInstructionException>(() => new RawInstruction("a."));
        Assert.Throws<InvalidInstructionException>(() => new RawInstruction(" a.bcd."));
        Assert.Throws<InvalidInstructionException>(() => new RawInstruction("a.\"asdasd"));
        Assert.Throws<InvalidInstructionException>(() => new RawInstruction("a.bcd.\"asd\"\""));
        Assert.Throws<InvalidInstructionException>(() => new RawInstruction("\".\""));
        Assert.Throws<InvalidInstructionException>(() => new RawInstruction(".."));
        Assert.Throws<InvalidInstructionException>(() => new RawInstruction("..\"asd"));
    }

    [Test]
    public void command()
    {
        Assert.AreEqual("a", new RawInstruction("a").Command);
        Assert.AreEqual("a", new RawInstruction("a.bcd").Command);
        Assert.AreEqual("a", new RawInstruction("a.bcd.\"def\"").Command);
        Assert.AreEqual("setModel", new RawInstruction("setModel.f000w000").Command);
        Assert.AreEqual(".", new RawInstruction("  .").Command);
        Assert.AreEqual(".", new RawInstruction("  ..def").Command);
        Assert.AreEqual("\"", new RawInstruction("\"").Command);
        Assert.AreEqual("\"", new RawInstruction("\".ghi").Command);
    }

    [Test]
    public void arguments()
    {
        Assert.AreEqual(0, new RawInstruction("a").Arguments.Length);

        var args1 = new RawInstruction("a.abc").Arguments;
        Assert.AreEqual(1, args1.Length);
        Assert.AreEqual("abc", args1[0]);

        var args2 = new RawInstruction("a.\"def\\\"\\\\\\n\".abc").Arguments;
        Assert.AreEqual(2, args2.Length);
        Assert.AreEqual("def\"\\\n", args2[0]);
        Assert.AreEqual("abc", args2[1]);
    }
}
