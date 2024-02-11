using System.IO;
using NUnit.Framework;
using zzio.db;

namespace zzio.tests.db;

[TestFixture]
public class TestRow
{
    private static readonly byte[] rowBytes = new byte[]
    {
        0xef, 0xbe, 0xad, 0xde, 3, 0, 0, 0,
        // string cell
        0, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, (byte)'z', (byte)'z', (byte)'i', (byte)'o', (byte)'\0',
        // integer cell
        1, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0, 0xff, 0xff, 0x00, 0x00,
        // buffer cell
        5, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 0xc0, 0xff, 0xee
    };

    private void testRow(Row row)
    {
        Assert.That(row, Is.Not.Null);
        Assert.That(row.uid, Is.EqualTo(new UID(0xdeadbeef)));
        Assert.That(row.cells.Length, Is.EqualTo(3));

        Assert.That(row.cells[0], Is.EqualTo(new Cell("zzio", 1)));
        Assert.That(row.cells[1], Is.EqualTo(new Cell((1 << 16) - 1, 2)));
        Assert.That(row.cells[2], Is.EqualTo(new Cell(new byte[] { 0xc0, 0xff, 0xee }, 3)));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(rowBytes, false);
        using BinaryReader reader = new(stream);
        Row row = new();
        row.Read(reader);
        testRow(row);
    }

    [Test]
    public void write()
    {
        Row row = new()
        {
            uid = new UID(0xdeadbeef),
            cells = new Cell[]
        {
            new Cell("zzio", 1),
            new Cell((1 << 16) - 1, 2),
            new Cell(new byte[] { 0xc0, 0xff, 0xee }, 3)
        }
        };
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        row.Write(writer);
        Assert.That(stream.ToArray(), Is.EqualTo(rowBytes));
    }
}
