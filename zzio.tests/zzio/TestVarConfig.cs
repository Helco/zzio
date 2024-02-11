using System.IO;
using NUnit.Framework;

namespace zzio.tests;

[TestFixture]
public class TestVarConfig
{
    private readonly byte[] sampleData = File.ReadAllBytes(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/varconfig_sample.cfg")
    );

    private readonly float TOLERANCE = 0.0001f;

    private void testConfig(VarConfig cfg)
    {
        Assert.That(cfg.header, Is.EqualTo(new byte[] { 0xc0, 0xff, 0xee }));
        Assert.That(cfg.firstValue.floatValue, Is.EqualTo(1.0f).Within(TOLERANCE));
        Assert.That(cfg.firstValue.stringValue, Is.EqualTo(""));
        Assert.That(cfg.variables.Count, Is.EqualTo(3));

        Assert.That(cfg.variables.ContainsKey("MY_FLOAT_VAR"), Is.True);
        Assert.That(cfg.variables["MY_FLOAT_VAR"].floatValue, Is.EqualTo(2.0f).Within(TOLERANCE));
        Assert.That(cfg.variables["MY_FLOAT_VAR"].stringValue, Is.EqualTo(""));

        Assert.That(cfg.variables.ContainsKey("MY_STRING_VAR"), Is.True);
        Assert.That(cfg.variables["MY_STRING_VAR"].floatValue, Is.EqualTo(0.0f).Within(TOLERANCE));
        Assert.That(cfg.variables["MY_STRING_VAR"].stringValue, Is.EqualTo("Zanzarah"));

        Assert.That(cfg.variables.ContainsKey("MY_BOTH_VAR"), Is.True);
        Assert.That(cfg.variables["MY_BOTH_VAR"].floatValue, Is.EqualTo(3.0f));
        Assert.That(cfg.variables["MY_BOTH_VAR"].stringValue, Is.EqualTo("Hello"));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(sampleData, false);
        VarConfig cfg = VarConfig.ReadNew(stream);
        testConfig(cfg);
    }

    [Test]
    public void write()
    {
        MemoryStream readStream = new(sampleData, false);
        VarConfig cfg = VarConfig.ReadNew(readStream);

        MemoryStream writeStream = new();
        cfg.Write(writeStream);

        MemoryStream rereadStream = new(writeStream.ToArray(), false);
        VarConfig rereadCfg = VarConfig.ReadNew(rereadStream);
        testConfig(rereadCfg);
    }
}
