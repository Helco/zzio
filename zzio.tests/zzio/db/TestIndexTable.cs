using System.IO;
using NUnit.Framework;
using zzio.db;

namespace zzio.tests.db;

[TestFixture]
public class TestIndexTable
{
    private readonly byte[] sampleData = File.ReadAllBytes(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/indextable.fbs")
    );

    private static void testIndexTable(IndexTable table)
    {
        Assert.That(table, Is.Not.Null);
        Assert.That(table.ColumnCount, Is.EqualTo(3));
        Assert.That(table.columnNames.Length, Is.EqualTo(3));
        Assert.That(table.columnNumbers.Length, Is.EqualTo(3));

        Assert.That(table.columnNames, Is.EqualTo(new string[] { "Mesh", "Name", "CardId" }));
        Assert.That(table.columnNumbers, Is.EqualTo(new uint[] { 1, 2, 3 }));
    }

    [Test]
    public void basic()
    {
        IndexTable table;

        table = new IndexTable();
        table.Read(new MemoryStream(sampleData, false));
        testIndexTable(table);

        // write, reread and test again
        MemoryStream stream = new();
        table.Write(stream);
        table = new IndexTable();
        table.Read(new MemoryStream(stream.ToArray(), false));
        testIndexTable(table);
    }
}
