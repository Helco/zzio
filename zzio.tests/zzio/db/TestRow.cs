using System.IO;
using NUnit.Framework;
using zzio.db;

namespace zzio.tests.db
{
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
            Assert.NotNull(row);
            Assert.AreEqual(new UID(0xdeadbeef), row.uid);
            Assert.AreEqual(3, row.cells.Length);

            Assert.AreEqual(new Cell("zzio", 1), row.cells[0]);
            Assert.AreEqual(new Cell((1 << 16) - 1, 2), row.cells[1]);
            Assert.AreEqual(new Cell(new byte[] { 0xc0, 0xff, 0xee }, 3), row.cells[2]);
        }

        [Test]
        public void read()
        {
            MemoryStream stream = new MemoryStream(rowBytes, false);
            using BinaryReader reader = new BinaryReader(stream);
            Row row = new Row();
            row.Read(reader);
            testRow(row);
        }

        [Test]
        public void write()
        {
            Row row = new Row();
            row.uid = new UID(0xdeadbeef);
            row.cells = new Cell[]
            {
                new Cell("zzio", 1),
                new Cell((1 << 16) - 1, 2),
                new Cell(new byte[] { 0xc0, 0xff, 0xee }, 3)
            };
            MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            row.Write(writer);
            Assert.AreEqual(rowBytes, stream.ToArray());
        }
    }
}
