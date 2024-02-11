using System;
using NUnit.Framework;

namespace zzio.tests.utils;

[TestFixture]
public class TestPath
{
    private T Env<T>(T windows, T posix)
    {
        return Environment.OSVersion.Platform == PlatformID.Win32NT
            ? windows : posix;
    }

    [Test]
    public void equals()
    {
#pragma warning disable CS1718
        FilePath path = new("a/b/c/d");
        Assert.That(path == new FilePath(path), Is.EqualTo(true));
        Assert.That(path == "a/b/c/d", Is.EqualTo(true));
        Assert.That(path.Equals("a/c/e/d"), Is.EqualTo(false));
        Assert.That(path == "A/B/c/d", Is.EqualTo(true));
        Assert.That(path != "a/b/c/d", Is.EqualTo(false));
        Assert.That(path != "a/c/e/d", Is.EqualTo(true));

        Assert.That(path.Equals("A/b/C/d", false), Is.EqualTo(true));
        Assert.That(path.Equals("a/b/C/D", true), Is.EqualTo(false));

        Assert.That(path.Equals("a/../a/b/.//c///d\\e/.."), Is.EqualTo(true));
        Assert.That(path.Equals("a/b/c/d/../e/"), Is.EqualTo(false));

        FilePath? nullPath = null;
        Assert.That(nullPath == "a/b", Is.EqualTo(false));
        Assert.That(nullPath != "a/b", Is.EqualTo(true));
        Assert.That(nullPath == nullPath, Is.EqualTo(true));
        Assert.That(new FilePath("a/b") == nullPath, Is.EqualTo(false));
        Assert.That(new FilePath("a/b") != nullPath, Is.EqualTo(true));

#pragma warning restore CS1718
    }

    [Test]
    public void root()
    {
        Assert.That(new FilePath("c:/a/b").Root, Is.EqualTo("c:"));
        Assert.That(new FilePath("/d/e/f/").Root, Is.EqualTo("/"));
        Assert.That(new FilePath("def:").Root, Is.EqualTo("def:/"));
        Assert.That(new FilePath("/").Root, Is.EqualTo("/"));
        Assert.That(new FilePath("pak:/").Root, Is.EqualTo("pak:/"));

        Assert.That(
            new FilePath("./a\\b/c").Root
, Is.EqualTo(Env(Environment.CurrentDirectory.Substring(0, 1) + ":\\", "/")));
    }

    [Test]
    public void combine()
    {
        Assert.That(new FilePath("a/b").Combine(new FilePath("c/d/")), Is.EqualTo("a/b/c/d/"));
        Assert.That(new FilePath("a").Combine(new FilePath("../../b/c")), Is.EqualTo("../b/c"));

        Assert.That(() => new FilePath("a/b").Combine("/c"), Throws.Exception);
        Assert.That(() => new FilePath("a/b").Combine("hello:\\"), Throws.Exception);
    }

    [Test]
    public void absolute()
    {
        Assert.That(new FilePath("c:/a/b/c/../").Absolute.ToPOSIXString(), Is.EqualTo("c:/a/b/"));
        Assert.That(new FilePath("/b/./././/c/d").Absolute.ToPOSIXString(), Is.EqualTo("/b/c/d"));

        string cur = Environment.CurrentDirectory;
        if (!cur.EndsWith(FilePath.Separator))
            cur += FilePath.Separator;
        Assert.That(new FilePath("a").Absolute, Is.EqualTo(cur + "a"));
        Assert.That(new FilePath("./b/..\\a").Absolute, Is.EqualTo(cur + "a"));
    }

    [Test]
    public void parts()
    {
        Assert.That(new FilePath("a/b\\C/").Parts, Is.EqualTo(new string[] { "a", "b", "C" }));
        Assert.That(new FilePath("c:\\d").Parts, Is.EqualTo(new string[] { "c:", "d" }));
        Assert.That(new FilePath("/e/f").Parts, Is.EqualTo(new string[] { "e", "f" }));
    }

    [Test]
    public void staysinbound()
    {
        Assert.That(new FilePath("a/b/c").StaysInbound, Is.True);
        Assert.That(new FilePath("a/b/../").StaysInbound, Is.True);
        Assert.That(new FilePath("a/../").StaysInbound, Is.True);
        Assert.That(new FilePath("c:/d/../").StaysInbound, Is.True);
        Assert.That(new FilePath("/d/..").StaysInbound, Is.True);
        Assert.That(new FilePath("a/../../").StaysInbound, Is.False);
        Assert.That(new FilePath("..").StaysInbound, Is.False);
        Assert.That(new FilePath("c:/..").StaysInbound, Is.False);
        Assert.That(new FilePath("/../").StaysInbound, Is.False);
    }

    [Test]
    public void parent()
    {
        FilePath? path = new("a/b/c");
        Assert.That((path = path?.Parent), Is.EqualTo("a/b/"));
        Assert.That((path = path?.Parent), Is.EqualTo("a/"));
        Assert.That((path = path?.Parent), Is.EqualTo("./"));
        Assert.That((path = path?.Parent), Is.EqualTo("../"));
        Assert.That((path = path?.Parent), Is.EqualTo("../../"));

        Assert.That(new FilePath("a/b/../c/.//").Parent, Is.EqualTo("a/"));

        Assert.That(new FilePath("c:/a").Parent, Is.EqualTo("c:"));
        Assert.That(new FilePath("c:/a/").Parent?.Parent, Is.EqualTo(null));

        Assert.That(new FilePath("/d/e").Parent, Is.EqualTo("/d"));
        Assert.That(new FilePath("/d/e").Parent?.Parent, Is.EqualTo(null));
    }

    [Test]
    public void relativeto()
    {
        Assert.That(new FilePath("a/b/c/d").RelativeTo("a/b"), Is.EqualTo("c/d"));
        Assert.That(new FilePath("a/b/c/d/").RelativeTo("a/b/e/f"), Is.EqualTo("../../c/d/"));
        Assert.That(new FilePath("a/b").RelativeTo("a/b/c/d"), Is.EqualTo("../../"));
        Assert.That(new FilePath("").RelativeTo(""), Is.EqualTo(""));

        Assert.That(() => new FilePath("/a/b/c").RelativeTo("c:/d/e"), Throws.Exception);
        Assert.That(() => new FilePath("c:/d/e").RelativeTo("d:/e/f"), Throws.Exception);
        Assert.That(() => new FilePath("c:/e/f").RelativeTo("/a"), Throws.Exception);
    }

    [Test]
    public void tostring()
    {
        Assert.That(new FilePath("c:/a\\b/").ToWin32String(), Is.EqualTo("c:\\a\\b\\"));
        Assert.That(new FilePath("a/../b\\c").ToWin32String(), Is.EqualTo("a\\..\\b\\c"));

        Assert.That(new FilePath("\\a\\b\\c/").ToPOSIXString(), Is.EqualTo("/a/b/c/"));
        Assert.That(new FilePath("c\\d/e").ToPOSIXString(), Is.EqualTo("c/d/e"));

        Assert.That(new FilePath("a\\b/c").ToString(), Is.EqualTo(Env("a\\b\\c", "a/b/c")));
    }

    [Test]
    public void gethashcode()
    {
        Assert.That(new FilePath("").GetHashCode(), Is.EqualTo(new FilePath("").GetHashCode()));
        Assert.That(new FilePath("/a/b/c").GetHashCode(), Is.EqualTo(new FilePath("/a/b/c").GetHashCode()));
        Assert.That(new FilePath("d\\b/c").GetHashCode(), Is.Not.EqualTo(new FilePath("c:/d/e").GetHashCode()));
        Assert.That(new FilePath(".").GetHashCode(), Is.EqualTo(new FilePath("").GetHashCode()));
        Assert.That(new FilePath("a").GetHashCode(), Is.EqualTo(new FilePath("a/").GetHashCode()));
        Assert.That(new FilePath("a/b/./c/../././../").GetHashCode(), Is.EqualTo(new FilePath("a").GetHashCode()));
    }

    [Test]
    public void extension()
    {
        Assert.That(new FilePath("").Extension, Is.Null);
        Assert.That(new FilePath("/").Extension, Is.Null);
        Assert.That(new FilePath("resources/").Extension, Is.Null);
        Assert.That(new FilePath("a/b.cdef/g").Extension, Is.Null);
        Assert.That(new FilePath("a/b.").Extension, Is.Null);

        Assert.That(new FilePath("a.txt").Extension, Is.EqualTo("txt"));
        Assert.That(new FilePath("c:/b.cedf/asd/a.txt").Extension, Is.EqualTo("txt"));
        Assert.That(new FilePath("c:/b.cedf/asd/a.qwe.rtz.yxc.txt").Extension, Is.EqualTo("txt"));
        Assert.That(new FilePath("c:/b.cedf/asd/a.qwe.rtz.yxc.t").Extension, Is.EqualTo("t"));
        Assert.That(new FilePath("c:/b.cedf/asd/a.qwe.rtz.yxc.abcdefghijkl").Extension, Is.EqualTo("abcdefghijkl"));
    }
}
