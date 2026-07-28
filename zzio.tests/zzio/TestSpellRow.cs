using NUnit.Framework;
using zzio.db;

namespace zzio.tests;

[TestFixture]
public class TestSpellRow
{
    // The spell database stores damage and loadup as small enum values; the
    // engine looks the real numbers up in tables (FUN_0044b4d6 for damage,
    // FUN_0043addc for the loadup rate).
    [TestCase(0, 50)]
    [TestCase(1, 65)]
    [TestCase(2, 70)]
    [TestCase(3, 95)]
    [TestCase(4, 110)]
    public void BaseDamageOfMapsTheDamageStep(int damage, int expected)
    {
        Assert.That(SpellRow.BaseDamageOf(damage), Is.EqualTo(expected));
    }

    [TestCase(0, 0.4)]
    [TestCase(1, 0.7)]
    [TestCase(2, 1.0)]
    [TestCase(3, 1.3)]
    [TestCase(4, 1.7)]
    public void BaseLoadupRateOfMapsTheLoadupStep(int loadup, double expected)
    {
        Assert.That(SpellRow.BaseLoadupRateOf(loadup), Is.EqualTo(expected).Within(1e-6));
    }

    // Support spells carry a damage step of 0 in the database, which would map
    // to a base damage of 50. That value is never used - support spells deal no
    // damage - but the lookup itself must stay total, so out-of-range steps
    // fall back to the first entry rather than throwing.
    [Test]
    public void LookupsAreTotal()
    {
        Assert.That(SpellRow.BaseDamageOf(99), Is.EqualTo(50));
        Assert.That(SpellRow.BaseLoadupRateOf(-1), Is.EqualTo(0.4).Within(1e-6));
    }
}
