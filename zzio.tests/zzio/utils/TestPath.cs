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
            Path path = new Path("a/b/c/d");
            Assert.AreEqual(true, path == new Path(path));
            Assert.AreEqual(true, path == "a/b/c/d");
            Assert.AreEqual(false, path.Equals("a/c/e/d"));
            Assert.AreEqual(Env(true, false), path == "A/B/c/d");
            Assert.AreEqual(false, path != "a/b/c/d");
            Assert.AreEqual(true, path != "a/c/e/d");

            Assert.AreEqual(true, path.Equals("A/b/C/d", false));
            Assert.AreEqual(false, path.Equals("a/b/C/D", true));

            Assert.AreEqual(true, path.Equals("a/../a/b/.//c///d\\e/.."));
            Assert.AreEqual(false, path.Equals("a/b/c/d/../e/"));
        }

        [Test]
        public void root()
        {
            Assert.AreEqual("c:", new Path("c:/a/b").Root);
            Assert.AreEqual("/", new Path("/d/e/f/").Root);
            Assert.AreEqual("def:/", new Path("def:").Root);
            Assert.AreEqual("/", new Path("/").Root);
            Assert.AreEqual("pak:/", new Path("pak:/").Root);

            Assert.AreEqual(
                Env(Environment.CurrentDirectory.Substring(0, 1) + ":\\", "/"),
                new Path("./a\\b/c").Root
            );
        }

        [Test]
        public void combine()
        {
            Assert.AreEqual("a/b/c/d/", new Path("a/b").Combine(new Path("c/d/")));
            Assert.AreEqual("../b/c", new Path("a").Combine(new Path("../../b/c")));

            Assert.That(() => new Path("a/b").Combine("/c"), Throws.Exception);
            Assert.That(() => new Path("a/b").Combine("hello:\\"), Throws.Exception);
        }

        [Test]
        public void absolute()
        {
            Assert.AreEqual("c:/a/b/", new Path("c:/a/b/c/../").Absolute().ToPOSIXString());
            Assert.AreEqual("/b/c/d", new Path("/b/./././/c/d").Absolute().ToPOSIXString());

            string cur = Environment.CurrentDirectory;
            if (!cur.EndsWith(Path.Separator))
                cur += Path.Separator;
            Assert.AreEqual(cur + "a", new Path("a").Absolute());
            Assert.AreEqual(cur + "a", new Path("./b/..\\a").Absolute());
        }

        [Test]
        public void relativeto()
        {
            Assert.AreEqual("c/d", new Path("a/b/c/d").RelativeTo("a/b"));
            Assert.AreEqual("../../c/d/", new Path("a/b/c/d/").RelativeTo("a/b/e/f"));
            Assert.AreEqual("../../", new Path("a/b").RelativeTo("a/b/c/d"));
            Assert.AreEqual("", new Path("").RelativeTo(""));

            Assert.That(() => new Path("/a/b/c").RelativeTo("c:/d/e"), Throws.Exception);
            Assert.That(() => new Path("c:/d/e").RelativeTo("d:/e/f"), Throws.Exception);
            Assert.That(() => new Path("c:/e/f").RelativeTo("/a"), Throws.Exception);
        }

        [Test]
        public void tostring()
        {
            Assert.AreEqual("c:\\a\\b\\", new Path("c:/a\\b/").ToWin32String());
            Assert.AreEqual("a\\..\\b\\c", new Path("a/../b\\c").ToWin32String());

            Assert.AreEqual("/a/b/c/", new Path("\\a\\b\\c/").ToPOSIXString());
            Assert.AreEqual("c/d/e", new Path("c\\d/e").ToPOSIXString());

            Assert.AreEqual(Env("a\\b\\c", "a/b/c"), new Path("a\\b/c").ToString());
        }

        [Test]
        public void gethashcode()
        {
            Assert.AreEqual(new Path("").GetHashCode(), new Path("").GetHashCode());
            Assert.AreEqual(new Path("/a/b/c").GetHashCode(), new Path("/a/b/c").GetHashCode());
            Assert.AreNotEqual(new Path("c:/d/e").GetHashCode(), new Path("d\\b/c").GetHashCode());
        }
    }
}
