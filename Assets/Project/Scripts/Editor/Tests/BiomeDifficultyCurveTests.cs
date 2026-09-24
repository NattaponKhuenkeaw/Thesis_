#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.Core.ZoneState;

public class BiomeDifficultyCurveTests
{
    [Test]
    public void BiomeZeroIsBaseline()
    {
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(0), Is.EqualTo(1f));
    }

    [Test]
    public void EachBiomeAddsTwentyPercent()
    {
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(1), Is.EqualTo(1.20f).Within(0.0001f));
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(2), Is.EqualTo(1.40f).Within(0.0001f));
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(3), Is.EqualTo(1.60f).Within(0.0001f));
    }

    [Test]
    public void NegativeBiomeClampsToBaseline()
    {
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(-2), Is.EqualTo(1f));
    }
}
#endif
