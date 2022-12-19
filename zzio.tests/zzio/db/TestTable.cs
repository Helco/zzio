using System.IO;
using NUnit.Framework;
using zzio.db;

namespace zzio.tests.db
{
    [TestFixture]
    public class TestTable
    {
        private readonly byte[] sampleData = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/table.fbs")
        );

        private void testTable(Table table)
        {
            Row row;
            Assert.NotNull(table);
            Assert.AreEqual(3, table.rows.Count);

            Assert.True(table.rows.ContainsKey(new UID(0xdeadbeef)));
            row = table.rows[new UID(0xdeadbeef)];
            Assert.AreEqual(2, row.cells.Length);
            Assert.AreEqual(new Cell("Zan", 1), row.cells[0]);
            Assert.AreEqual(new Cell((byte)0x10, 2), row.cells[1]);

            Assert.True(table.rows.ContainsKey(new UID(0xdabbad00)));
            row = table.rows[new UID(0xdabbad00)];
            Assert.AreEqual(2, row.cells.Length);
            Assert.AreEqual(new Cell("za", 1), row.cells[0]);
            Assert.AreEqual(new Cell((byte)0x20, 2), row.cells[1]);

            Assert.True(table.rows.ContainsKey(new UID(0x00bada2a)));
            row = table.rows[new UID(0x00bada2a)];
            Assert.AreEqual(2, row.cells.Length);
            Assert.AreEqual(new Cell("rah", 1), row.cells[0]);
            Assert.AreEqual(new Cell((byte)0x30, 2), row.cells[1]);
        }

        [Test]
        public void basic()
        {
            Table table;

            table = new Table();
            table.Read(new MemoryStream(sampleData, false));
            testTable(table);

            // write, reread and test again
            MemoryStream stream = new();
            table.Write(stream);
            table = new Table();
            table.Read(new MemoryStream(stream.ToArray(), false));
            testTable(table);
        }
    }
}
