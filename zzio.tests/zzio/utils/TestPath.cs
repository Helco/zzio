using System;
using NUnit.Framework;
using zzio.utils;

namespace zzio.tests.utils
{
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
            FilePath path = new FilePath("a/b/c/d");
            Assert.AreEqual(true, path == new FilePath(path));
            Assert.AreEqual(true, path == "a/b/c/d");
            Assert.AreEqual(false, path.Equals("a/c/e/d"));
            Assert.AreEqual(Env(true, false), path == "A/B/c/d");
            Assert.AreEqual(false, path != "a/b/c/d");
            Assert.AreEqual(true, path != "a/c/e/d");

            Assert.AreEqual(true, path.Equals("A/b/C/d", false));
            Assert.AreEqual(false, path.Equals("a/b/C/D", true));

            Assert.AreEqual(true, path.Equals("a/../a/b/.//c///d\\e/.."));
            Assert.AreEqual(false, path.Equals("a/b/c/d/../e/"));

            FilePath nullPath = null;
            Assert.AreEqual(false, nullPath == "a/b");
            Assert.AreEqual(true, nullPath != "a/b");
            Assert.AreEqual(true, nullPath == nullPath);
            Assert.AreEqual(false, new FilePath("a/b") == nullPath);
            Assert.AreEqual(true, new FilePath("a/b") != nullPath);

            #pragma warning restore CS1718
        }

        [Test]
        public void root()
        {
            Assert.AreEqual("c:", new FilePath("c:/a/b").Root);
            Assert.AreEqual("/", new FilePath("/d/e/f/").Root);
            Assert.AreEqual("def:/", new FilePath("def:").Root);
            Assert.AreEqual("/", new FilePath("/").Root);
            Assert.AreEqual("pak:/", new FilePath("pak:/").Root);

            Assert.AreEqual(
                Env(Environment.CurrentDirectory.Substring(0, 1) + ":\\", "/"),
                new FilePath("./a\\b/c").Root
            );
        }

        [Test]
        public void combine()
        {
            Assert.AreEqual("a/b/c/d/", new FilePath("a/b").Combine(new FilePath("c/d/")));
            Assert.AreEqual("../b/c", new FilePath("a").Combine(new FilePath("../../b/c")));

            Assert.That(() => new FilePath("a/b").Combine("/c"), Throws.Exception);
            Assert.That(() => new FilePath("a/b").Combine("hello:\\"), Throws.Exception);
        }

        [Test]
        public void absolute()
        {
            Assert.AreEqual("c:/a/b/", new FilePath("c:/a/b/c/../").Absolute.ToPOSIXString());
            Assert.AreEqual("/b/c/d", new FilePath("/b/./././/c/d").Absolute.ToPOSIXString());

            string cur = Environment.CurrentDirectory;
            if (!cur.EndsWith(FilePath.Separator))
                cur += FilePath.Separator;
            Assert.AreEqual(cur + "a", new FilePath("a").Absolute);
            Assert.AreEqual(cur + "a", new FilePath("./b/..\\a").Absolute);
        }

        [Test]
        public void parts()
        {
            Assert.AreEqual(new string[] { "a", "b", "C" }, new FilePath("a/b\\C/").Parts);
            Assert.AreEqual(new string[] { "c:", "d" }, new FilePath("c:\\d").Parts);
            Assert.AreEqual(new string[] { "e", "f" }, new FilePath("/e/f").Parts);
        }

        [Test]
        public void staysinbound()
        {
            Assert.True(new FilePath("a/b/c").StaysInbound);
            Assert.True(new FilePath("a/b/../").StaysInbound);
            Assert.True(new FilePath("a/../").StaysInbound);
            Assert.True(new FilePath("c:/d/../").StaysInbound);
            Assert.True(new FilePath("/d/..").StaysInbound);
            Assert.False(new FilePath("a/../../").StaysInbound);
            Assert.False(new FilePath("..").StaysInbound);
            Assert.False(new FilePath("c:/..").StaysInbound);
            Assert.False(new FilePath("/../").StaysInbound);
        }

        [Test]
        public void parent()
        {
            FilePath path = new FilePath("a/b/c");
            Assert.AreEqual("a/b/", (path = path.Parent));
            Assert.AreEqual("a/", (path = path.Parent));
            Assert.AreEqual("./", (path = path.Parent));
            Assert.AreEqual("../", (path = path.Parent));
            Assert.AreEqual("../../", (path = path.Parent));

            Assert.AreEqual("a/", new FilePath("a/b/../c/.//").Parent);

            Assert.AreEqual("c:", new FilePath("c:/a").Parent);
            Assert.AreEqual(null, new FilePath("c:/a/").Parent.Parent);

            Assert.AreEqual("/d", new FilePath("/d/e").Parent);
            Assert.AreEqual(null, new FilePath("/d/e").Parent.Parent);
        }

        [Test]
        public void relativeto()
        {
            Assert.AreEqual("c/d", new FilePath("a/b/c/d").RelativeTo("a/b"));
            Assert.AreEqual("../../c/d/", new FilePath("a/b/c/d/").RelativeTo("a/b/e/f"));
            Assert.AreEqual("../../", new FilePath("a/b").RelativeTo("a/b/c/d"));
            Assert.AreEqual("", new FilePath("").RelativeTo(""));

            Assert.That(() => new FilePath("/a/b/c").RelativeTo("c:/d/e"), Throws.Exception);
            Assert.That(() => new FilePath("c:/d/e").RelativeTo("d:/e/f"), Throws.Exception);
            Assert.That(() => new FilePath("c:/e/f").RelativeTo("/a"), Throws.Exception);
        }

        [Test]
        public void tostring()
        {
            Assert.AreEqual("c:\\a\\b\\", new FilePath("c:/a\\b/").ToWin32String());
            Assert.AreEqual("a\\..\\b\\c", new FilePath("a/../b\\c").ToWin32String());

            Assert.AreEqual("/a/b/c/", new FilePath("\\a\\b\\c/").ToPOSIXString());
            Assert.AreEqual("c/d/e", new FilePath("c\\d/e").ToPOSIXString());

            Assert.AreEqual(Env("a\\b\\c", "a/b/c"), new FilePath("a\\b/c").ToString());
        }

        [Test]
        public void gethashcode()
        {
            Assert.AreEqual(new FilePath("").GetHashCode(), new FilePath("").GetHashCode());
            Assert.AreEqual(new FilePath("/a/b/c").GetHashCode(), new FilePath("/a/b/c").GetHashCode());
            Assert.AreNotEqual(new FilePath("c:/d/e").GetHashCode(), new FilePath("d\\b/c").GetHashCode());
        }
    }
}
