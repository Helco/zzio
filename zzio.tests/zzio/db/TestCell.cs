using System.IO;
using NUnit.Framework;
using zzio.db;

namespace zzio.tests.db;

[TestFixture]
public class TestCell
{
    private readonly Cell stringCell = new("Hello", 13);
    private readonly byte[] stringCellBytes = new byte[]
    {
        0, 0, 0, 0, 13, 0, 0, 0, 6, 0, 0, 0,
        (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o', (byte)'\0'
    };

    private readonly Cell integerCell = new(123456, 13);
    private readonly byte[] integerCellBytes = new byte[]
    {
        1, 0, 0, 0, 13, 0, 0, 0, 4, 0, 0, 0, 0x40, 0xE2, 0x01, 0x00
    };

    private readonly Cell byteCell = new((byte)0xcd, 13);
    private readonly byte[] byteCellBytes = new byte[]
    {
        4, 0, 0, 0, 13, 0, 0, 0, 1, 0, 0, 0, 0xcd
    };

    private readonly Cell foreignKeyCell = new(new ForeignKey(new UID(0xc0fffeee), new UID(0xbadc0de)));
    private readonly byte[] foreignKeyCellBytes = new byte[]
    {
        3, 0, 0, 0, 255, 255, 255, 255, 8, 0, 0, 0,
        0xee, 0xfe, 0xff, 0xc0, 0xde, 0xc0, 0xad, 0xb
    };

    private readonly Cell bufferCell = new(new byte[] { 0x37, 0x53, 0x73 });
    private readonly byte[] bufferCellBytes = new byte[]
    {
        5, 0, 0, 0, 255, 255, 255, 255, 3, 0, 0, 0, 0x37, 0x53, 0x73
    };

    private static void testCellType(CellDataType expected, Cell cell)
    {
        Assert.That(cell.Type, Is.EqualTo(expected));
        if (expected != CellDataType.String)
            Assert.That(() => cell.String, Throws.Exception);
        if (expected != CellDataType.Integer)
            Assert.That(() => cell.Integer, Throws.Exception);
        if (expected != CellDataType.Byte)
            Assert.That(() => cell.Byte, Throws.Exception);
        if (expected != CellDataType.ForeignKey)
            Assert.That(() => cell.ForeignKey, Throws.Exception);
        if (expected != CellDataType.Buffer)
            Assert.That(() => cell.Buffer, Throws.Exception);
    }

    [Test]
    public void create()
    {
        testCellType(CellDataType.String, stringCell);
        Assert.That(stringCell.ColumnIndex, Is.EqualTo(13));
        Assert.That(stringCell.String, Is.EqualTo("Hello"));

        testCellType(CellDataType.Integer, integerCell);
        Assert.That(integerCell.ColumnIndex, Is.EqualTo(13));
        Assert.That(integerCell.Integer, Is.EqualTo(123456));

        testCellType(CellDataType.Byte, byteCell);
        Assert.That(byteCell.ColumnIndex, Is.EqualTo(13));
        Assert.That(byteCell.Byte, Is.EqualTo(0xcd));

        testCellType(CellDataType.ForeignKey, foreignKeyCell);
        Assert.That(foreignKeyCell.ColumnIndex, Is.EqualTo(-1));
        Assert.That(foreignKeyCell.ForeignKey.uid.raw, Is.EqualTo(0xc0fffeee));
        Assert.That(foreignKeyCell.ForeignKey.type.raw, Is.EqualTo(0xbadc0de));

        testCellType(CellDataType.Buffer, bufferCell);
        Assert.That(bufferCell.ColumnIndex, Is.EqualTo(-1));
        Assert.That(bufferCell.Buffer, Is.EqualTo(new byte[] { 0x37, 0x53, 0x73 }));
    }

    private static void testCellEquality(bool expected, Cell compare, Cell actual)
    {
        Assert.That(compare.Equals(actual), Is.EqualTo(expected));
        bool hashEquality = compare.GetHashCode() == actual.GetHashCode();
        Assert.That(hashEquality, Is.EqualTo(expected));
    }

    [Test]
    public void equals()
    {
        Cell cell = new(13, 15);
        testCellEquality(true, cell, cell);
        testCellEquality(true, cell, new Cell(13, 15));

        testCellEquality(false, cell, new Cell(15, 13));
        testCellEquality(false, cell, new Cell(13, 17));
        testCellEquality(false, cell, new Cell("abc", 15));
        testCellEquality(false, cell, new Cell((byte)13, 15));
        testCellEquality(false, cell, new Cell(new ForeignKey(), 15));
        testCellEquality(false, cell, new Cell(new byte[] { 13, 0, 0, 0 }, 15));
    }

    private static void testCellRead(Cell expected, byte[] sourceBytes)
    {
        MemoryStream stream = new(sourceBytes, false);
        using BinaryReader reader = new(stream);
        Cell readCell = Cell.ReadNew(reader);
        Assert.That(expected.Equals(readCell), Is.EqualTo(true));
    }

    [Test]
    public void read()
    {
        testCellRead(integerCell, integerCellBytes);
        testCellRead(stringCell, stringCellBytes);
        testCellRead(byteCell, byteCellBytes);
        testCellRead(foreignKeyCell, foreignKeyCellBytes);
        testCellRead(bufferCell, bufferCellBytes);
    }

    private static void testCellWrite(byte[] expected, Cell sourceCell)
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        sourceCell.Write(writer);
        Assert.That(stream.ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public void write()
    {
        testCellWrite(integerCellBytes, integerCell);
        testCellWrite(stringCellBytes, stringCell);
        testCellWrite(byteCellBytes, byteCell);
        testCellWrite(foreignKeyCellBytes, foreignKeyCell);
        testCellWrite(bufferCellBytes, bufferCell);
    }
}
