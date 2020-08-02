using System;
using System.IO;
using NUnit.Framework;
using zzio;

namespace zzio.tests
{
    [TestFixture]
    public class TestMapMarker
    {
        private readonly byte[] sampleData = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/mapmarker_sample.cfg")
        );

        private void testMarkers(MapMarker[] mapMarkers)
        {
            Assert.NotNull(mapMarkers);
            Assert.AreEqual(3, mapMarkers.Length);

            Assert.AreEqual(1234, mapMarkers[0].posX);
            Assert.AreEqual(5678, mapMarkers[0].posY);
            Assert.AreEqual(MapMarkerSection.EnchantedForest, mapMarkers[0].section);
            Assert.AreEqual(1337, mapMarkers[0].sceneId);

            Assert.AreEqual(9876, mapMarkers[1].posX);
            Assert.AreEqual(5432, mapMarkers[1].posY);
            Assert.AreEqual(MapMarkerSection.DarkSwamp, mapMarkers[1].section);
            Assert.AreEqual(42, mapMarkers[1].sceneId);

            Assert.AreEqual(1357, mapMarkers[2].posX);
            Assert.AreEqual(2468, mapMarkers[2].posY);
            Assert.AreEqual(MapMarkerSection.RealmOfClouds, mapMarkers[2].section);
            Assert.AreEqual(1037, mapMarkers[2].sceneId);
        }

        [Test]
        public void read()
        {
            MemoryStream stream = new MemoryStream(sampleData, false);
            MapMarker[] mapMarkers = MapMarker.ReadFile(stream);
            testMarkers(mapMarkers);
        }

        [Test]
        public void write()
        {
            MemoryStream readStream = new MemoryStream(sampleData, false);
            MapMarker[] mapMarkers = MapMarker.ReadFile(readStream);

            MemoryStream writeStream = new MemoryStream();
            MapMarker.WriteFile(mapMarkers, writeStream);

            MemoryStream rereadStream = new MemoryStream(writeStream.ToArray(), false);
            MapMarker[] rereadMapMarkers = MapMarker.ReadFile(rereadStream);
            testMarkers(rereadMapMarkers);
        }
    }
}
