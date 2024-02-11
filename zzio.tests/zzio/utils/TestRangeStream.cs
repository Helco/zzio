using System.IO;
using System.Linq;
using NUnit.Framework;

namespace zzio.tests.utils;

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
        MemoryStream memStream = new(testData, false);
        memStream.Seek(testDataOffset, SeekOrigin.Current);
        RangeStream rangeStream = new(memStream, testDataLength);

        byte[] part1 = new byte[3];
        int part1Len = rangeStream.Read(part1, 0, 3);
        Assert.That(part1Len, Is.EqualTo(3));
        Assert.That(part1[0], Is.EqualTo(0x01));
        Assert.That(part1[1], Is.EqualTo(0x02));
        Assert.That(part1[2], Is.EqualTo(0x03));

        byte[] part2 = new byte[4];
        int part2Len = rangeStream.Read(part2, 1, 3);
        Assert.That(part2Len, Is.EqualTo(1));
        Assert.That(part2[1], Is.EqualTo(0x04));
    }

    [Test]
    public void write()
    {
        byte[] actual = testData.ToArray();
        MemoryStream memStream = new(actual, true);
        memStream.Seek(testDataOffset, SeekOrigin.Current);
        RangeStream rangeStream = new(memStream, 4, true);

        byte[] expected = testData.ToArray();
        expected[testDataOffset + 0] = 0x10;
        expected[testDataOffset + 1] = 0x20;
        expected[testDataOffset + 2] = 0x30;
        expected[testDataOffset + 3] = 0x40;
        rangeStream.Write(expected, testDataOffset, testDataLength);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void seek()
    {
        MemoryStream memStream = new(testData, false);
        memStream.Seek(testDataOffset, SeekOrigin.Current);
        RangeStream rangeStream = new(memStream, testDataLength);

        Assert.That(rangeStream.ReadByte(), Is.EqualTo(testData[testDataOffset + 0]));
        rangeStream.Position += 2;
        Assert.That(rangeStream.ReadByte(), Is.EqualTo(testData[testDataOffset + 3]));
        Assert.That(rangeStream.Seek(0, SeekOrigin.Begin), Is.EqualTo(0));
        Assert.That(rangeStream.ReadByte(), Is.EqualTo(testData[testDataOffset + 0]));
        Assert.That(rangeStream.Seek(1, SeekOrigin.Current), Is.EqualTo(2));
        Assert.That(rangeStream.ReadByte(), Is.EqualTo(testData[testDataOffset + 2]));
        Assert.That(rangeStream.Seek(-3, SeekOrigin.End), Is.EqualTo(1));
        Assert.That(rangeStream.ReadByte(), Is.EqualTo(testData[testDataOffset + 1]));
    }
}
