using System.IO;
using NUnit.Framework;
using zzio.db;

namespace zzio.tests.db;

[TestFixture]
public class TestTable
{
    private readonly byte[] sampleData = File.ReadAllBytes(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/table.fbs")
    );

    private static void testTable(Table table)
    {
        Row row;
        Assert.That(table, Is.Not.Null);
        Assert.That(table.rows.Count, Is.EqualTo(3));

        Assert.That(table.rows.ContainsKey(new UID(0xdeadbeef)), Is.True);
        row = table.rows[new UID(0xdeadbeef)];
        Assert.That(row.cells.Length, Is.EqualTo(2));
        Assert.That(row.cells[0], Is.EqualTo(new Cell("Zan", 1)));
        Assert.That(row.cells[1], Is.EqualTo(new Cell((byte)0x10, 2)));

        Assert.That(table.rows.ContainsKey(new UID(0xdabbad00)), Is.True);
        row = table.rows[new UID(0xdabbad00)];
        Assert.That(row.cells.Length, Is.EqualTo(2));
        Assert.That(row.cells[0], Is.EqualTo(new Cell("za", 1)));
        Assert.That(row.cells[1], Is.EqualTo(new Cell((byte)0x20, 2)));

        Assert.That(table.rows.ContainsKey(new UID(0x00bada2a)), Is.True);
        row = table.rows[new UID(0x00bada2a)];
        Assert.That(row.cells.Length, Is.EqualTo(2));
        Assert.That(row.cells[0], Is.EqualTo(new Cell("rah", 1)));
        Assert.That(row.cells[1], Is.EqualTo(new Cell((byte)0x30, 2)));
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
