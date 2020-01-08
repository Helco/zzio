using System;
using System.IO;
using NUnit.Framework;
using zzio.primitives;
using zzio.db;

namespace zzio.tests.db
{
    [TestFixture]
    public class TestCell
    {
        private Cell stringCell = new Cell("Hello", 13);
        private readonly byte[] stringCellBytes = new byte[]
        {
            0, 0, 0, 0, 13, 0, 0, 0, 6, 0, 0, 0,
            (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o', (byte)'\0'
        };

        private Cell integerCell = new Cell(123456, 13);
        private readonly byte[] integerCellBytes = new byte[]
        {
            1, 0, 0, 0, 13, 0, 0, 0, 4, 0, 0, 0, 0x40, 0xE2, 0x01, 0x00
        };

        private Cell byteCell = new Cell((byte)0xcd, 13);
        private readonly byte[] byteCellBytes = new byte[]
        {
            4, 0, 0, 0, 13, 0, 0, 0, 1, 0, 0, 0, 0xcd
        };

        private Cell foreignKeyCell = new Cell(new ForeignKey(new UID(0xc0fffeee), new UID(0xbadc0de)));
        private readonly byte[] foreignKeyCellBytes = new byte[]
        {
            3, 0, 0, 0, 255, 255, 255, 255, 8, 0, 0, 0,
            0xee, 0xfe, 0xff, 0xc0, 0xde, 0xc0, 0xad, 0xb
        };

        private Cell bufferCell = new Cell(new byte[] { 0x37, 0x53, 0x73 });
        private readonly byte[] bufferCellBytes = new byte[]
        {
            5, 0, 0, 0, 255, 255, 255, 255, 3, 0, 0, 0, 0x37, 0x53, 0x73
        };

        private void testCellType(CellDataType expected, Cell cell)
        {
            Assert.AreEqual(expected, cell.Type);
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
            Assert.AreEqual(13, stringCell.ColumnIndex);
            Assert.AreEqual("Hello", stringCell.String);

            testCellType(CellDataType.Integer, integerCell);
            Assert.AreEqual(13, integerCell.ColumnIndex);
            Assert.AreEqual(123456, integerCell.Integer);

            testCellType(CellDataType.Byte, byteCell);
            Assert.AreEqual(13, byteCell.ColumnIndex);
            Assert.AreEqual(0xcd, byteCell.Byte);

            testCellType(CellDataType.ForeignKey, foreignKeyCell);
            Assert.AreEqual(-1, foreignKeyCell.ColumnIndex);
            Assert.AreEqual(0xc0fffeee, foreignKeyCell.ForeignKey.uid.raw);
            Assert.AreEqual(0xbadc0de, foreignKeyCell.ForeignKey.type.raw);

            testCellType(CellDataType.Buffer, bufferCell);
            Assert.AreEqual(-1, bufferCell.ColumnIndex);
            Assert.AreEqual(new byte[] { 0x37, 0x53, 0x73 }, bufferCell.Buffer);
        }

        private void testCellEquality(bool expected, Cell compare, Cell actual)
        {
            Assert.AreEqual(expected, compare.Equals(actual));
            bool hashEquality = compare.GetHashCode() == actual.GetHashCode();
            Assert.AreEqual(expected, hashEquality);
        }

        [Test]
        public void equals()
        {
            Cell cell = new Cell(13, 15);
            testCellEquality(true, cell, cell);
            testCellEquality(true, cell, new Cell(13, 15));

            testCellEquality(false, cell, new Cell(15, 13));
            testCellEquality(false, cell, new Cell(13, 17));
            testCellEquality(false, cell, new Cell("abc", 15));
            testCellEquality(false, cell, new Cell((byte)13, 15));
            testCellEquality(false, cell, new Cell(new ForeignKey(), 15));
            testCellEquality(false, cell, new Cell(new byte[] { 13, 0, 0, 0 }, 15));
        }

        private void testCellRead(Cell expected, byte[] sourceBytes)
        {
            MemoryStream stream = new MemoryStream(sourceBytes, false);
            BinaryReader reader = new BinaryReader(stream);
            Cell readCell = Cell.ReadNew(reader);
            Assert.AreEqual(true, expected.Equals(readCell));
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

        private void testCellWrite(byte[] expected, Cell sourceCell)
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            sourceCell.Write(writer);
            Assert.AreEqual(expected, stream.ToArray());
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
}
