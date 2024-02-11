using NUnit.Framework;

namespace zzio.tests.utils;

[TestFixture]
public class TestStringUtils
{
    [Test]
    public void escape()
    {
        Assert.That(StringUtils.Escape("abcdef"), Is.EqualTo("abcdef"));
        Assert.That(StringUtils.Escape("abc\ndef"), Is.EqualTo("abc\\ndef"));
        Assert.That(StringUtils.Escape("abcödef"), Is.EqualTo("abc\\xF6def"));
        Assert.That(StringUtils.Escape("a\x16\n\r\\\'\""), Is.EqualTo("a\\x16\\n\\r\\\\\\'\\\""));
    }

    [Test]
    public void unescape()
    {
        Assert.That(StringUtils.Unescape("abcdef"), Is.EqualTo("abcdef"));
        Assert.That(StringUtils.Unescape("abc\\ndef"), Is.EqualTo("abc\ndef"));
        Assert.That(StringUtils.Unescape("abc\\xF6def"), Is.EqualTo("abcödef"));
        Assert.That(StringUtils.Unescape("a\\x23\\n\\r\\\\\\'\\\""), Is.EqualTo("a\x23\n\r\\\'\""));
    }
}
