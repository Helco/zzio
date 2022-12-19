using System.IO;
using NUnit.Framework;

namespace zzio.tests
{
    [TestFixture]
    public class TestActorExDescription
    {
        private readonly byte[] sampleData = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/actorex_sample.aed")
        );

        private void testInstance(ActorExDescription aed)
        {
            Assert.NotNull(aed);
            Assert.AreEqual(1337, aed.headBoneID);
            Assert.AreEqual("hello.dff", aed.body.model);
            Assert.AreEqual("wings.dff", aed.wings.model);

            Assert.AreEqual(2, aed.body.animations.Length);
            Assert.AreEqual("first.ani", aed.body.animations[0].filename);
            Assert.AreEqual(AnimationType.Jump, aed.body.animations[0].type);
            Assert.AreEqual("second.ani", aed.body.animations[1].filename);
            Assert.AreEqual(AnimationType.Run, aed.body.animations[1].type);

            Assert.AreEqual(1, aed.wings.animations.Length);
            Assert.AreEqual("third.ani", aed.wings.animations[0].filename);
            Assert.AreEqual(AnimationType.RunForwardLeft, aed.wings.animations[0].type);
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
}
