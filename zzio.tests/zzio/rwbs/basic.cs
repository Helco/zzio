using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using zzio.rwbs;

namespace zzio.tests.rwbs 
{
    [TestFixture]
    public class TestRWBSBasic
    {
        private readonly byte[] sampleData = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/rwbs_sample.dff")
        );

        private void testSection(Section section)
        {
            Assert.IsInstanceOf(typeof(RWClump), section);
            RWClump clump = (RWClump)section;
            Assert.AreEqual(SectionId.Clump, section.sectionId);
            Assert.AreEqual(null, section.parent);
            Assert.AreEqual(0, clump.atomicCount);
            Assert.AreEqual(0, clump.camCount);
            Assert.AreEqual(0, clump.lightCount);
            Assert.AreEqual(3, clump.children.Count);
            
            
            Assert.IsInstanceOf(typeof(RWFrameList), clump.children[0]);
            RWFrameList frameList = (RWFrameList)clump.children[0];
            Assert.AreSame(clump, frameList.parent);
            Assert.AreEqual(SectionId.FrameList, frameList.sectionId);
            Assert.AreEqual(0, frameList.children.Count);
            Assert.AreEqual(1, frameList.frames.Length);
            Frame frame = frameList.frames[0];
            Assert.AreEqual(0, frame.creationFlags);
            Assert.AreEqual(0xffffffff, frame.frameIndex);
            Assert.AreEqual(new float[] {
                 1.0f, 0.0f, 0.0f,
                 0.0f, 1.0f, 0.0f,
                 0.0f, 0.0f, 1.0f
            }, frame.rotMatrix);
            Assert.AreEqual(0.0f, frame.position.x);
            Assert.AreEqual(0.0f, frame.position.y);
            Assert.AreEqual(0.0f, frame.position.z);

            Assert.IsInstanceOf(typeof(RWExtension), clump.children[1]);
            RWExtension extension = (RWExtension)clump.children[1];
            Assert.AreSame(clump, extension.parent);
            Assert.AreEqual(0, extension.children.Count);

            Assert.IsInstanceOf(typeof(RWGeometryList), clump.children[2]);
            RWGeometryList geoList = (RWGeometryList)clump.children[2];
            Assert.AreSame(clump, geoList.parent);
            Assert.AreEqual(SectionId.GeometryList, geoList.sectionId);
            Assert.AreEqual(0, geoList.children.Count);
            Assert.AreEqual(0, geoList.geometryCount);
        }

        [Test]
        public void read()
        {
            MemoryStream stream = new MemoryStream(sampleData, false);
            Section section = Section.ReadNew(stream);
            testSection(section);
        }

        [Test]
        public void write()
        {
            MemoryStream readStream = new MemoryStream(sampleData, false);
            Section section = Section.ReadNew(readStream);

            MemoryStream writeStream = new MemoryStream();
            section.Write(writeStream);

            MemoryStream rereadStream = new MemoryStream(writeStream.ToArray(), false);
            Section rereadSection = Section.ReadNew(rereadStream);
            testSection(rereadSection);
        }
    }
}