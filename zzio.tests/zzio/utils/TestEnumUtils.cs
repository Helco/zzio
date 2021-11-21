using System;
using NUnit.Framework;

namespace zzio.tests.utils
{
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
            Assert.AreEqual(TestEnum.A, EnumUtils.intToEnum<TestEnum>(0));
            Assert.AreEqual(TestEnum.B, EnumUtils.intToEnum<TestEnum>(100));
            Assert.AreEqual(TestEnum.Unknown, EnumUtils.intToEnum<TestEnum>(200));
            Assert.AreEqual(TestEnum.Unknown, EnumUtils.intToEnum<TestEnum>(-1));
        }

        [Test]
        public void intToFlags()
        {
            Assert.AreEqual("0", EnumUtils.intToFlags<TestFlags>(0).ToString());
            Assert.AreEqual("A", EnumUtils.intToFlags<TestFlags>(1).ToString());
            Assert.AreEqual("A, B, C", EnumUtils.intToFlags<TestFlags>(1 + 2 + 32).ToString());
            Assert.AreEqual("A, B", EnumUtils.intToFlags<TestFlags>(1 + 2 + 4).ToString());
            Assert.AreEqual("0", EnumUtils.intToFlags<TestFlags>(4 + 16).ToString());
        }
    }
}
