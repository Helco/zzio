using System.IO;
using System.Numerics;
using NUnit.Framework;
using zzio.rwbs;

namespace zzio.tests.rwbs;

[TestFixture]
public class TestRWBSBasic
{
    private readonly byte[] sampleData = File.ReadAllBytes(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/rwbs_sample.dff")
    );

    private void testSection(Section section)
    {
        Assert.That(section, Is.InstanceOf(typeof(RWClump)));
        RWClump clump = (RWClump)section;
        Assert.That(section.sectionId, Is.EqualTo(SectionId.Clump));
        Assert.That(section.parent, Is.EqualTo(null));
        Assert.That(clump.atomicCount, Is.EqualTo(0));
        Assert.That(clump.camCount, Is.EqualTo(0));
        Assert.That(clump.lightCount, Is.EqualTo(0));
        Assert.That(clump.children.Count, Is.EqualTo(3));


        Assert.That(clump.children[0], Is.InstanceOf(typeof(RWFrameList)));
        RWFrameList frameList = (RWFrameList)clump.children[0];
        Assert.That(frameList.parent, Is.SameAs(clump));
        Assert.That(frameList.sectionId, Is.EqualTo(SectionId.FrameList));
        Assert.That(frameList.children.Count, Is.EqualTo(1));
        Assert.That(frameList.frames.Length, Is.EqualTo(1));
        Frame frame = frameList.frames[0];
        Assert.That(frame.creationFlags, Is.EqualTo(0));
        Assert.That(frame.frameIndex, Is.EqualTo(0xffffffff));
        Assert.That(frame.rotMatrix0, Is.EqualTo(Vector3.UnitX));
        Assert.That(frame.rotMatrix1, Is.EqualTo(Vector3.UnitY));
        Assert.That(frame.rotMatrix2, Is.EqualTo(Vector3.UnitZ));
        Assert.That(frame.position.X, Is.EqualTo(0.0f));
        Assert.That(frame.position.Y, Is.EqualTo(0.0f));
        Assert.That(frame.position.Z, Is.EqualTo(0.0f));

        Assert.That(frameList.children[0], Is.InstanceOf(typeof(RWExtension)));

        Assert.That(clump.children[1], Is.InstanceOf(typeof(RWGeometryList)));
        RWGeometryList geoList = (RWGeometryList)clump.children[1];
        Assert.That(geoList.parent, Is.SameAs(clump));
        Assert.That(geoList.sectionId, Is.EqualTo(SectionId.GeometryList));
        Assert.That(geoList.children.Count, Is.EqualTo(0));
        Assert.That(geoList.geometryCount, Is.EqualTo(0));

        Assert.That(clump.children[2], Is.InstanceOf(typeof(RWExtension)));
        RWExtension extension = (RWExtension)clump.children[2];
        Assert.That(extension.parent, Is.SameAs(clump));
        Assert.That(extension.children.Count, Is.EqualTo(0));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(sampleData, false);
        Section section = Section.ReadNew(stream);
        testSection(section);
    }

    [Test]
    public void write()
    {
        MemoryStream readStream = new(sampleData, false);
        Section section = Section.ReadNew(readStream);

        MemoryStream writeStream = new();
        section.Write(writeStream);

        MemoryStream rereadStream = new(writeStream.ToArray(), false);
        Section rereadSection = Section.ReadNew(rereadStream);
        testSection(rereadSection);
    }
}