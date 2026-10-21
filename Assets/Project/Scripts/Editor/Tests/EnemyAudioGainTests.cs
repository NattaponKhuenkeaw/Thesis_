#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EnemyAudioGainTests
{
    [TestCase(-1f, 0f)]
    [TestCase(0f, 0f)]
    [TestCase(.5f, .5f)]
    [TestCase(1f, 1f)]
    [TestCase(1.5f, 1.5f)]
    [TestCase(2f, 2f)]
    [TestCase(3f, 2f)]
    public void AuthoredGainAllowsBoostButStaysBounded(float value, float expected)
    {
        Assert.That(GameAudioManager.ClampSFXGain(value), Is.EqualTo(expected));
    }

    [Test]
    public void InvalidGainIsMuted()
    {
        Assert.That(GameAudioManager.ClampSFXGain(float.NaN), Is.Zero);
    }

    [Test]
    public void SFXMixKeepsBoostAndStillRespectsAllVolumeControls()
    {
        Assert.That(GameAudioManager.CalculateSFXGain(2f, 1f, 1f, 1f), Is.EqualTo(2f));
        Assert.That(GameAudioManager.CalculateSFXGain(2f, .5f, 1f, 1f), Is.EqualTo(1f));
        Assert.That(GameAudioManager.CalculateSFXGain(2f, 1f, .5f, 1f), Is.EqualTo(1f));
        Assert.That(GameAudioManager.CalculateSFXGain(2f, 1f, 1f, .5f), Is.EqualTo(1f));
        Assert.That(GameAudioManager.CalculateSFXGain(2f, 0f, 1f, 1f), Is.Zero);
        Assert.That(GameAudioManager.CalculateSFXGain(2f, 1f, 0f, 1f), Is.Zero);
        Assert.That(GameAudioManager.CalculateSFXGain(2f, 1f, 1f, 0f), Is.Zero);
        Assert.That(GameAudioManager.CalculateSFXGain(.5f, .5f, .5f, .5f), Is.EqualTo(.0625f));
    }

    [Test]
    public void EnemyKitExposesBoostForWarningAttackAndBlock()
    {
        var kit = ScriptableObject.CreateInstance<EnemyKit>();
        try
        {
            using (var data = new SerializedObject(kit))
            {
                data.FindProperty("attackWarningSfxVolume").floatValue = 1.5f;
                data.FindProperty("attackSfxVolume").floatValue = 1.75f;
                data.FindProperty("blockSfxVolume").floatValue = 2f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.That(kit.AttackWarningSfxVolume, Is.EqualTo(1.5f));
            Assert.That(kit.AttackSfxVolume, Is.EqualTo(1.75f));
            Assert.That(kit.BlockSfxVolume, Is.EqualTo(2f));
        }
        finally { Object.DestroyImmediate(kit); }
    }
}
#endif
