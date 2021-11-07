using System.IO;
using System.Linq;
using NUnit.Framework;
using zzio.utils;

namespace zzio.tests.utils
{
    [TestFixture]
    public class TestRangeStream
    {
        private readonly byte[] testData = new byte[] {
            0xde, 0xad, 0xbe, 0xef,
            0x01, 0x02, 0x03, 0x04,
            0x00, 0xc0, 0xff, 0xee
        };
        private readonly int testDataLength = 4;
        private readonly int testDataOffset = 4;

        [Test]
        public void read()
        {
            MemoryStream memStream = new MemoryStream(testData, false);
            memStream.Seek(testDataOffset, SeekOrigin.Current);
            RangeStream rangeStream = new RangeStream(memStream, testDataLength);

            byte[] part1 = new byte[3];
            int part1Len = rangeStream.Read(part1, 0, 3);
            Assert.AreEqual(3, part1Len);
            Assert.AreEqual(0x01, part1[0]);
            Assert.AreEqual(0x02, part1[1]);
            Assert.AreEqual(0x03, part1[2]);
            
            byte[] part2 = new byte[4];
            int part2Len = rangeStream.Read(part2, 1, 3);
            Assert.AreEqual(1, part2Len);
            Assert.AreEqual(0x04, part2[1]);
        }

        [Test]
        public void write()
        {
            byte[] actual = testData.ToArray();
            MemoryStream memStream = new MemoryStream(actual, true);
            memStream.Seek(testDataOffset, SeekOrigin.Current);
            RangeStream rangeStream = new RangeStream(memStream, 4, true);
            
            byte[] expected = testData.ToArray();
            expected[testDataOffset + 0] = 0x10;
            expected[testDataOffset + 1] = 0x20;
            expected[testDataOffset + 2] = 0x30;
            expected[testDataOffset + 3] = 0x40;
            rangeStream.Write(expected, testDataOffset, testDataLength);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void seek()
        {
            MemoryStream memStream = new MemoryStream(testData, false);
            memStream.Seek(testDataOffset, SeekOrigin.Current);
            RangeStream rangeStream = new RangeStream(memStream, testDataLength);

            Assert.AreEqual(testData[testDataOffset + 0], rangeStream.ReadByte());
            rangeStream.Position += 2;
            Assert.AreEqual(testData[testDataOffset + 3], rangeStream.ReadByte());
            Assert.AreEqual(0, rangeStream.Seek(0, SeekOrigin.Begin));
            Assert.AreEqual(testData[testDataOffset + 0], rangeStream.ReadByte());
            Assert.AreEqual(2, rangeStream.Seek(1, SeekOrigin.Current));
            Assert.AreEqual(testData[testDataOffset + 2], rangeStream.ReadByte());
            Assert.AreEqual(1, rangeStream.Seek(-3, SeekOrigin.End));
            Assert.AreEqual(testData[testDataOffset + 1], rangeStream.ReadByte());
        }
    }
}
