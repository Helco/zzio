using System.IO;
using NUnit.Framework;

namespace zzio.tests
{
    [TestFixture]
    public class TestSkeletalAnimation
    {
        private readonly byte[] sampleData = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/animation_sample.ani")
        );

        private readonly float TOLERANCE = 0.0001f;

        private void testNullKeyFrame(AnimationKeyFrame frame)
        {
            Assert.AreEqual(0.0f, frame.rot.X, TOLERANCE);
            Assert.AreEqual(0.0f, frame.rot.Y, TOLERANCE);
            Assert.AreEqual(0.0f, frame.rot.Z, TOLERANCE);
            Assert.AreEqual(0.0f, frame.rot.W, TOLERANCE);
            Assert.AreEqual(0.0f, frame.pos.X, TOLERANCE);
            Assert.AreEqual(0.0f, frame.pos.Y, TOLERANCE);
            Assert.AreEqual(0.0f, frame.pos.Z, TOLERANCE);
        }

        private void testAnimation(SkeletalAnimation ani)
        {
            Assert.NotNull(ani);
            Assert.AreEqual(4, ani.flags);
            Assert.AreEqual(3.0f, ani.duration, TOLERANCE);
            Assert.AreEqual(3, ani.BoneCount);
            Assert.AreEqual(3, ani.boneFrames.Length);

            Assert.AreEqual(3, ani.boneFrames[0].Length);
            Assert.AreEqual(1.0f, ani.boneFrames[0][0].rot.X, TOLERANCE);
            Assert.AreEqual(2.0f, ani.boneFrames[0][0].rot.Y, TOLERANCE);
            Assert.AreEqual(3.0f, ani.boneFrames[0][0].rot.Z, TOLERANCE);
            Assert.AreEqual(4.0f, ani.boneFrames[0][0].rot.W, TOLERANCE);
            Assert.AreEqual(5.0f, ani.boneFrames[0][0].pos.X, TOLERANCE);
            Assert.AreEqual(6.0f, ani.boneFrames[0][0].pos.Y, TOLERANCE);
            Assert.AreEqual(7.0f, ani.boneFrames[0][0].pos.Z, TOLERANCE);
            Assert.AreEqual(0.0f, ani.boneFrames[0][0].time, TOLERANCE);
            testNullKeyFrame(ani.boneFrames[0][1]);
            Assert.AreEqual(1.0f, ani.boneFrames[0][1].time, TOLERANCE);
            testNullKeyFrame(ani.boneFrames[0][2]);
            Assert.AreEqual(2.0f, ani.boneFrames[0][2].time, TOLERANCE);

            Assert.AreEqual(2, ani.boneFrames[1].Length);
            testNullKeyFrame(ani.boneFrames[1][0]);
            Assert.AreEqual(0.0f, ani.boneFrames[1][0].time, TOLERANCE);
            Assert.AreEqual(8.0f, ani.boneFrames[1][1].rot.X, TOLERANCE);
            Assert.AreEqual(9.0f, ani.boneFrames[1][1].rot.Y, TOLERANCE);
            Assert.AreEqual(10.0f, ani.boneFrames[1][1].rot.Z, TOLERANCE);
            Assert.AreEqual(11.0f, ani.boneFrames[1][1].rot.W, TOLERANCE);
            Assert.AreEqual(12.0f, ani.boneFrames[1][1].pos.X, TOLERANCE);
            Assert.AreEqual(13.0f, ani.boneFrames[1][1].pos.Y, TOLERANCE);
            Assert.AreEqual(14.0f, ani.boneFrames[1][1].pos.Z, TOLERANCE);
            Assert.AreEqual(1.5, ani.boneFrames[1][1].time);

            Assert.AreEqual(1, ani.boneFrames[2].Length);
            testNullKeyFrame(ani.boneFrames[2][0]);
            Assert.AreEqual(0.0f, ani.boneFrames[2][0].time, TOLERANCE);
        }

        [Test]
        public void read()
        {
            MemoryStream stream = new MemoryStream(sampleData, false);
            SkeletalAnimation ani = SkeletalAnimation.ReadNew(stream);
            testAnimation(ani);
        }

        [Test]
        public void write()
        {
            MemoryStream readStream = new MemoryStream(sampleData, false);
            SkeletalAnimation ani = SkeletalAnimation.ReadNew(readStream);

            MemoryStream writeStream = new MemoryStream();
            ani.Write(writeStream);

            MemoryStream rereadStream = new MemoryStream(writeStream.ToArray(), false);
            SkeletalAnimation rereadAni = SkeletalAnimation.ReadNew(rereadStream);
            testAnimation(rereadAni);
        }
    }
}
