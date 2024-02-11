using System.IO;
using NUnit.Framework;

namespace zzio.tests;

[TestFixture]
public class TestActorExDescription
{
    private readonly byte[] sampleData = File.ReadAllBytes(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/actorex_sample.aed")
    );

    private void testInstance(ActorExDescription aed)
    {
        Assert.NotNull(aed);
        Assert.That(aed.headBoneID, Is.EqualTo(1337));
        Assert.That(aed.body.model, Is.EqualTo("hello.dff"));
        Assert.That(aed.wings.model, Is.EqualTo("wings.dff"));

        Assert.That(aed.body.animations.Length, Is.EqualTo(2));
        Assert.That(aed.body.animations[0].filename, Is.EqualTo("first.ani"));
        Assert.That(aed.body.animations[0].type, Is.EqualTo(AnimationType.Jump));
        Assert.That(aed.body.animations[1].filename, Is.EqualTo("second.ani"));
        Assert.That(aed.body.animations[1].type, Is.EqualTo(AnimationType.Run));

        Assert.That(aed.wings.animations.Length, Is.EqualTo(1));
        Assert.That(aed.wings.animations[0].filename, Is.EqualTo("third.ani"));
        Assert.That(aed.wings.animations[0].type, Is.EqualTo(AnimationType.RunForwardLeft));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(sampleData, false);
        ActorExDescription aed = ActorExDescription.ReadNew(stream);
        testInstance(aed);
    }

    [Test]
    public void write()
    {
        MemoryStream readStream = new(sampleData, false);
        ActorExDescription aed = ActorExDescription.ReadNew(readStream);

        MemoryStream writeStream = new();
        aed.Write(writeStream);

        MemoryStream rereadStream = new(writeStream.ToArray(), false);
        ActorExDescription rereadAed = ActorExDescription.ReadNew(rereadStream);

        testInstance(rereadAed);
    }
}
