using NUnit.Framework;

namespace zzio.tests.utils;

[TestFixture]
public class TestStringUtils
{
    [Test]
    public void escape()
    {
        Assert.AreEqual("abcdef", StringUtils.Escape("abcdef"));
        Assert.AreEqual("abc\\ndef", StringUtils.Escape("abc\ndef"));
        Assert.AreEqual("abc\\xF6def", StringUtils.Escape("abcödef"));
        Assert.AreEqual("a\\x16\\n\\r\\\\\\'\\\"", StringUtils.Escape("a\x16\n\r\\\'\""));
    }

    [Test]
    public void unescape()
    {
        Assert.AreEqual("abcdef", StringUtils.Unescape("abcdef"));
        Assert.AreEqual("abc\ndef", StringUtils.Unescape("abc\\ndef"));
        Assert.AreEqual("abcödef", StringUtils.Unescape("abc\\xF6def"));
        Assert.AreEqual("a\x23\n\r\\\'\"", StringUtils.Unescape("a\\x23\\n\\r\\\\\\'\\\""));
    }
}
