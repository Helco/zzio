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
            Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../resources/rwbs_sample.dff")
        );

        private void testSection(BaseSection section)
        {
            Assert.IsInstanceOf(typeof(RWClump), section);
            RWClump clump = (RWClump)section;
            Assert.AreEqual(SectionId.Clump, section.sectionId);
            Assert.AreEqual(null, section.parent);
            Assert.AreEqual(0, clump.atomicCount);
            Assert.AreEqual(0, clump.camCount);
            Assert.AreEqual(0, clump.lightCount);
            Assert.AreEqual(2, clump.childs.Length);
            
            
            Assert.IsInstanceOf(typeof(RWFrameList), clump.childs[0]);
            RWFrameList frameList = (RWFrameList)clump.childs[0];
            Assert.AreSame(clump, frameList.parent);
            Assert.AreEqual(SectionId.FrameList, frameList.sectionId);
            Assert.AreEqual(0, frameList.childs.Length);
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

            Assert.IsInstanceOf(typeof(RWGeometryList), clump.childs[1]);
            RWGeometryList geoList = (RWGeometryList)clump.childs[1];
            Assert.AreSame(clump, geoList.parent);
            Assert.AreEqual(SectionId.GeometryList, geoList.sectionId);
            Assert.AreEqual(0, geoList.childs.Length);
            Assert.AreEqual(0, geoList.geometryCount);
        }

        [Test]
        public void read()
        {
            Reader reader = new Reader(sampleData.ToArray());
            BaseSection section = reader.readSection();
            testSection(section);
        }
    }
}