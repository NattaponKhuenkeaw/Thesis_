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
    public void EachBiomeAddsTwelvePercent()
    {
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(1), Is.EqualTo(1.12f).Within(0.0001f));
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(2), Is.EqualTo(1.24f).Within(0.0001f));
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(3), Is.EqualTo(1.36f).Within(0.0001f));
    }

    [Test]
    public void NegativeBiomeClampsToBaseline()
    {
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(-2), Is.EqualTo(1f));
    }
}
#endif
