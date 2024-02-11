using System.IO;
using NUnit.Framework;
using zzio.scn;

namespace zzio.tests.scn;

[TestFixture]
public class TestSceneBasic
{
    private readonly byte[] sampleData = File.ReadAllBytes(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/scene_sample.scn")
    );

    private void testScene(Scene scn)
    {
        Assert.That(scn.ambientSound, Is.EqualTo(12));

        Assert.That(scn.version.author, Is.EqualTo("Helco"));
        Assert.That(scn.version.buildVersion, Is.EqualTo(4));
        Assert.That(scn.version.country, Is.EqualTo(VersionBuildCountry.Germany));
        Assert.That(scn.version.date, Is.EqualTo("22.05.2018"));
        Assert.That(scn.version.time, Is.EqualTo("15:56"));
        Assert.That(scn.version.type, Is.EqualTo(VersionBuildType.Debug));
        Assert.That(scn.version.v3, Is.EqualTo(0));
        Assert.That(scn.version.vv2, Is.EqualTo(2));
        Assert.That(scn.version.year, Is.EqualTo(2018));

        Assert.That(scn.sceneItems.Length, Is.EqualTo(2));
        Assert.That(scn.sceneItems[0].index, Is.EqualTo(2));
        Assert.That(scn.sceneItems[0].type, Is.EqualTo(4));
        Assert.That(scn.sceneItems[0].name, Is.EqualTo(""));
        Assert.That(scn.sceneItems[1].index, Is.EqualTo(3));
        Assert.That(scn.sceneItems[1].type, Is.EqualTo(4));
        Assert.That(scn.sceneItems[1].name, Is.EqualTo(""));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(sampleData, false);
        Scene scene = new();
        scene.Read(stream);
        testScene(scene);
    }

    [Test]
    public void write()
    {
        MemoryStream readStream = new(sampleData, false);
        Scene scene = new();
        scene.Read(readStream);

        MemoryStream writeStream = new();
        scene.Write(writeStream);

        MemoryStream rereadStream = new(writeStream.ToArray(), false);
        Scene rereadScene = new();
        rereadScene.Read(rereadStream);
        testScene(rereadScene);
    }
}
