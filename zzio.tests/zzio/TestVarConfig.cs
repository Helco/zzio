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
        Assert.AreEqual(new byte[] { 0xc0, 0xff, 0xee }, cfg.header);
        Assert.AreEqual(1.0f, cfg.firstValue.floatValue, TOLERANCE);
        Assert.AreEqual("", cfg.firstValue.stringValue);
        Assert.AreEqual(3, cfg.variables.Count);

        Assert.True(cfg.variables.ContainsKey("MY_FLOAT_VAR"));
        Assert.AreEqual(2.0f, cfg.variables["MY_FLOAT_VAR"].floatValue, TOLERANCE);
        Assert.AreEqual("", cfg.variables["MY_FLOAT_VAR"].stringValue);

        Assert.True(cfg.variables.ContainsKey("MY_STRING_VAR"));
        Assert.AreEqual(0.0f, cfg.variables["MY_STRING_VAR"].floatValue, TOLERANCE);
        Assert.AreEqual("Zanzarah", cfg.variables["MY_STRING_VAR"].stringValue);

        Assert.True(cfg.variables.ContainsKey("MY_BOTH_VAR"));
        Assert.AreEqual(3.0f, cfg.variables["MY_BOTH_VAR"].floatValue);
        Assert.AreEqual("Hello", cfg.variables["MY_BOTH_VAR"].stringValue);
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
