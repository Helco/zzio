using System.IO;
using NUnit.Framework;
using zzio.db;

namespace zzio.tests.db
{
    [TestFixture]
    public class TestIndexTable
    {
        private readonly byte[] sampleData = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/indextable.fbs")
        );

        private void testIndexTable(IndexTable table)
        {
            Assert.NotNull(table);
            Assert.AreEqual(3, table.ColumnCount);
            Assert.AreEqual(3, table.columnNames.Length);
            Assert.AreEqual(3, table.columnNumbers.Length);
            
            Assert.AreEqual(new string[] { "Mesh", "Name", "CardId" }, table.columnNames);
            Assert.AreEqual(new uint[] { 1, 2, 3 }, table.columnNumbers);
        }

        [Test]
        public void basic()
        {
            IndexTable table;

            table = new IndexTable();
            table.Read(new MemoryStream(sampleData, false));
            testIndexTable(table);

            // write, reread and test again
            MemoryStream stream = new MemoryStream();
            table.Write(stream);
            table = new IndexTable();
            table.Read(new MemoryStream(stream.ToArray(), false));
            testIndexTable(table);
        }
    }
}
