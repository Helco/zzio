using System.IO;
using NUnit.Framework;

namespace zzio.tests;

[TestFixture]
public class TestMapMarker
{
    private readonly byte[] sampleData = File.ReadAllBytes(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/mapmarker_sample.cfg")
    );

    private static void testMarkers(MapMarker[] mapMarkers)
    {
        Assert.That(mapMarkers, Is.Not.Null);
        Assert.That(mapMarkers.Length, Is.EqualTo(3));

        Assert.That(mapMarkers[0].posX, Is.EqualTo(1234));
        Assert.That(mapMarkers[0].posY, Is.EqualTo(5678));
        Assert.That(mapMarkers[0].section, Is.EqualTo(MapMarkerSection.EnchantedForest));
        Assert.That(mapMarkers[0].sceneId, Is.EqualTo(1337));

        Assert.That(mapMarkers[1].posX, Is.EqualTo(9876));
        Assert.That(mapMarkers[1].posY, Is.EqualTo(5432));
        Assert.That(mapMarkers[1].section, Is.EqualTo(MapMarkerSection.DarkSwamp));
        Assert.That(mapMarkers[1].sceneId, Is.EqualTo(42));

        Assert.That(mapMarkers[2].posX, Is.EqualTo(1357));
        Assert.That(mapMarkers[2].posY, Is.EqualTo(2468));
        Assert.That(mapMarkers[2].section, Is.EqualTo(MapMarkerSection.RealmOfClouds));
        Assert.That(mapMarkers[2].sceneId, Is.EqualTo(1037));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(sampleData, false);
        MapMarker[] mapMarkers = MapMarker.ReadFile(stream);
        testMarkers(mapMarkers);
    }

    [Test]
    public void write()
    {
        MemoryStream readStream = new(sampleData, false);
        MapMarker[] mapMarkers = MapMarker.ReadFile(readStream);

        MemoryStream writeStream = new();
        MapMarker.WriteFile(mapMarkers, writeStream);

        MemoryStream rereadStream = new(writeStream.ToArray(), false);
        MapMarker[] rereadMapMarkers = MapMarker.ReadFile(rereadStream);
        testMarkers(rereadMapMarkers);
    }
}
