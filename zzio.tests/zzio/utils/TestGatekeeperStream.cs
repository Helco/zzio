using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzio.tests.utils
{
    [TestFixture]
    public class TestGatekeeperStream
    {
        private void assertStreamClosed(bool expected, Stream stream)
        {
            byte[] buffer = new byte[] { 1 };
            bool exceptionWasThrown = false;
            try
            {
                stream.Write(buffer, 0, 1);
                Assert.AreEqual(1, stream.Read(buffer, 0, 1));
            }
            catch (Exception)
            {
                exceptionWasThrown = true;
            }
            Assert.AreEqual(expected, exceptionWasThrown);
        }

        [Test]
        public void selftest()
        {
            MemoryStream stream = new(new byte[] { 1, 2, 3, 4 });
            assertStreamClosed(false, stream);
            stream.Close();
            assertStreamClosed(true, stream);
        }

        private void testStreamAccess(Stream stream, Func<IResolveConstraint> expectedFact)
        {
            Assert.That(() => stream.Write(new byte[] { 3, 4, 5 }, 0, 3), expectedFact());
            Assert.That(() => stream.Read(new byte[3], 0, 3), expectedFact());
            Assert.That(() => stream.Seek(3, SeekOrigin.Begin), expectedFact());
            Assert.That(() => { stream.Position = 5; }, expectedFact());
        }

        [Test]
        public void keepsopen()
        {
            MemoryStream memStream = new(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });
            GatekeeperStream gateStream = new(memStream);
            testStreamAccess(gateStream, () => Throws.Nothing);
            assertStreamClosed(false, gateStream);

            gateStream.Close();
            testStreamAccess(gateStream, () => Throws.Exception);
            testStreamAccess(memStream, () => Throws.Nothing);
            assertStreamClosed(true, gateStream);
            assertStreamClosed(false, memStream);
        }
    }
}
