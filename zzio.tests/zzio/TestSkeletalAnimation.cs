using System.IO;
using NUnit.Framework;

namespace zzio.tests;

[TestFixture]
public class TestSkeletalAnimation
{
    private readonly byte[] sampleData = File.ReadAllBytes(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/animation_sample.ani")
    );

    private readonly float TOLERANCE = 0.0001f;

    private void testNullKeyFrame(AnimationKeyFrame frame)
    {
        Assert.That(frame.rot.X, Is.EqualTo(0.0f).Within(TOLERANCE));
        Assert.That(frame.rot.Y, Is.EqualTo(0.0f).Within(TOLERANCE));
        Assert.That(frame.rot.Z, Is.EqualTo(0.0f).Within(TOLERANCE));
        Assert.That(frame.rot.W, Is.EqualTo(0.0f).Within(TOLERANCE));
        Assert.That(frame.pos.X, Is.EqualTo(0.0f).Within(TOLERANCE));
        Assert.That(frame.pos.Y, Is.EqualTo(0.0f).Within(TOLERANCE));
        Assert.That(frame.pos.Z, Is.EqualTo(0.0f).Within(TOLERANCE));
    }

    private void testAnimation(SkeletalAnimation ani)
    {
        Assert.That(ani, Is.Not.Null);
        Assert.That(ani.flags, Is.EqualTo(4));
        Assert.That(ani.duration, Is.EqualTo(3.0f).Within(TOLERANCE));
        Assert.That(ani.BoneCount, Is.EqualTo(3));
        Assert.That(ani.boneFrames.Length, Is.EqualTo(3));

        Assert.That(ani.boneFrames[0].Length, Is.EqualTo(3));
        Assert.That(ani.boneFrames[0][0].rot.X, Is.EqualTo(1.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[0][0].rot.Y, Is.EqualTo(2.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[0][0].rot.Z, Is.EqualTo(3.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[0][0].rot.W, Is.EqualTo(4.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[0][0].pos.X, Is.EqualTo(5.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[0][0].pos.Y, Is.EqualTo(6.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[0][0].pos.Z, Is.EqualTo(7.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[0][0].time, Is.EqualTo(0.0f).Within(TOLERANCE));
        testNullKeyFrame(ani.boneFrames[0][1]);
        Assert.That(ani.boneFrames[0][1].time, Is.EqualTo(1.0f).Within(TOLERANCE));
        testNullKeyFrame(ani.boneFrames[0][2]);
        Assert.That(ani.boneFrames[0][2].time, Is.EqualTo(2.0f).Within(TOLERANCE));

        Assert.That(ani.boneFrames[1].Length, Is.EqualTo(2));
        testNullKeyFrame(ani.boneFrames[1][0]);
        Assert.That(ani.boneFrames[1][0].time, Is.EqualTo(0.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[1][1].rot.X, Is.EqualTo(8.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[1][1].rot.Y, Is.EqualTo(9.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[1][1].rot.Z, Is.EqualTo(10.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[1][1].rot.W, Is.EqualTo(11.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[1][1].pos.X, Is.EqualTo(12.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[1][1].pos.Y, Is.EqualTo(13.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[1][1].pos.Z, Is.EqualTo(14.0f).Within(TOLERANCE));
        Assert.That(ani.boneFrames[1][1].time, Is.EqualTo(1.5));

        Assert.That(ani.boneFrames[2].Length, Is.EqualTo(1));
        testNullKeyFrame(ani.boneFrames[2][0]);
        Assert.That(ani.boneFrames[2][0].time, Is.EqualTo(0.0f).Within(TOLERANCE));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(sampleData, false);
        SkeletalAnimation ani = SkeletalAnimation.ReadNew(stream);
        testAnimation(ani);
    }

    [Test]
    public void write()
    {
        MemoryStream readStream = new(sampleData, false);
        SkeletalAnimation ani = SkeletalAnimation.ReadNew(readStream);

        MemoryStream writeStream = new();
        ani.Write(writeStream);

        MemoryStream rereadStream = new(writeStream.ToArray(), false);
        SkeletalAnimation rereadAni = SkeletalAnimation.ReadNew(rereadStream);
        testAnimation(rereadAni);
    }
}
