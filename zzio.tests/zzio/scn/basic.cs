using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using zzio.scn;

namespace zzio.tests.scn
{
    [TestFixture]
    public class TestSceneBasic
    {
        private readonly byte[] sampleData = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../resources/scene_sample.scn")
        );

        private void testScene(Scene scn)
        {
            Assert.AreEqual(12, scn.ambientSoundCount);

            Assert.AreEqual("Helco", scn.version.author);
            Assert.AreEqual(4, scn.version.buildVersion);
            Assert.AreEqual(VersionBuildCountry.Germany, scn.version.country);
            Assert.AreEqual("22.05.2018", scn.version.date);
            Assert.AreEqual("15:56", scn.version.time);
            Assert.AreEqual(VersionBuildType.Debug, scn.version.type);
            Assert.AreEqual(0, scn.version.v3);
            Assert.AreEqual(2, scn.version.vv2);
            Assert.AreEqual(2018, scn.version.year);

            Assert.AreEqual(2, scn.sceneItems.Length);
            Assert.AreEqual(2, scn.sceneItems[0].i1);
            Assert.AreEqual(4, scn.sceneItems[0].i2);
            Assert.AreEqual("", scn.sceneItems[0].s);
            Assert.AreEqual(3, scn.sceneItems[1].i1);
            Assert.AreEqual(4, scn.sceneItems[1].i2);
            Assert.AreEqual("", scn.sceneItems[1].s);
        }

        [Test]
        public void read()
        {
            Scene scene = Scene.read(sampleData.ToArray());
            testScene(scene);
        }
    }
}
