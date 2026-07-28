using NUnit.Framework;
using zzio.db;

namespace zzio.tests;

[TestFixture]
public class TestFairyRow
{
    // The original engine computes the maximum hitpoints of a fairy in
    // FUN_00439a3d as
    //     base  = MHP * 0.1f
    //     maxHP = (int)((float)(int)((MHP - base) * (level / 60f)) + base)
    // Note the two separate truncations: the level-scaled part is truncated
    // before the base is added, and the sum is truncated again. Adding the
    // base before a single truncation - as a straightforward reading of the
    // formula suggests - is off by one for many combinations.
    [TestCase(175, 1, 19)]
    [TestCase(465, 1, 52)]
    [TestCase(480, 35, 299)]
    public void MaxHPAtLevelTruncatesTwice(int mhp, int level, int expected)
    {
        Assert.That(FairyRow.MaxHPAtLevel(mhp, level), Is.EqualTo(expected));
    }

    // Values where both the correct and the naive formula agree - these guard
    // against regressions in the general shape of the curve.
    [TestCase(300, 0, 30)]
    [TestCase(300, 30, 165)]
    [TestCase(300, 60, 300)]
    public void MaxHPAtLevelFollowsTheCurve(int mhp, int level, int expected)
    {
        Assert.That(FairyRow.MaxHPAtLevel(mhp, level), Is.EqualTo(expected));
    }

    // Jump power 2 is the one index where the original assigns the base factor
    // directly instead of multiplying by a per-step constant (FUN_00439d61:
    // "if (index == 2) field = base;" with no fmul, unlike every other branch).
    [Test]
    public void JumpPowerStepTwoUsesTheBaseFactorUnscaled()
    {
        Assert.That(FairyRow.BaseJumpPowerOf(2), Is.EqualTo(1.2).Within(1e-9));
        Assert.That(FairyRow.BaseJumpPowerOf(1), Is.EqualTo(0.8 * 1.2).Within(1e-9));
        Assert.That(FairyRow.BaseJumpPowerOf(3), Is.EqualTo(1.3 * 1.2).Within(1e-9));
    }
}
