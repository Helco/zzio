using System.IO;
using NUnit.Framework;

namespace zzio.tests;

[TestFixture]
public class TestGameConfig
{
    private readonly byte[] sampleData = File.ReadAllBytes(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/game.cfg")
    );

    private void testConfig(GameConfig config)
    {
        Assert.That(config.isFullscreen, Is.False);
        Assert.That(config.bindings[(int)BindingId.NumPad0], Is.EqualTo(new GameConfig.Binding(DirectInputKey.Numpad0)));
        Assert.That(config.soundQuality, Is.EqualTo(GameConfig.SoundQuality.High));
    }

    [Test]
    public void read()
    {
        using MemoryStream stream = new(sampleData, false);
        var config = GameConfig.ReadNew(stream);
        testConfig(config);
    }

    [Test]
    public void write()
    {
        using MemoryStream readStream = new(sampleData, false);
        GameConfig config = GameConfig.ReadNew(readStream);

        using MemoryStream writeStream = new();
        config.Write(writeStream);

        using MemoryStream rereadStream = new(writeStream.ToArray(), false);
        GameConfig rereadConfig = GameConfig.ReadNew(rereadStream);
        testConfig(rereadConfig);
    }
}
