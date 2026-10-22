#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using Project.AIBehaviour.Boss;
using UnityEditor;
using UnityEngine;

public class BossChargeHitAudioTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void BossPrefabHasASeparateNamedChargeRushSound()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab");
        Assert.That(prefab, Is.Not.Null);
        var boss = prefab.GetComponent<BossPatternController>();
        var rush = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Audio/SFX/Boss_ChargeRush.mp3");
        Assert.That(rush, Is.Not.Null);
        Assert.That(boss.ChargeRushSfx, Is.SameAs(rush));
        Assert.That(boss.ChargeRushSfx, Is.Not.SameAs(boss.ChargeHitSfx));
        Assert.That(boss.ChargeRushSfx, Is.Not.SameAs(boss.AttackWarningSfx));
        Assert.That(boss.ChargeRushSfxVolume, Is.GreaterThan(0f).And.LessThanOrEqualTo(GameAudioManager.MaxSFXGain));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ClearingOrDisablingBossStopsItsChargeWhooshWithoutStoppingOtherAudio(bool disableBoss)
    {
        var host = new GameObject("BossRushAudioCleanupTest");
        host.SetActive(false);
        var clip = AudioClip.Create("RushAudioTest", 128, 1, 44100, false);
        try
        {
            var boss = host.AddComponent<BossPatternController>();
            var rush = host.AddComponent<AudioSource>();
            var otherHost = new GameObject("OtherAudio");
            otherHost.transform.SetParent(host.transform, false);
            var other = otherHost.AddComponent<AudioSource>();
            rush.clip = clip;
            other.clip = clip;
            typeof(BossPatternController).GetField("chargeRushAudioSource", Flags).SetValue(boss, rush);
            if (disableBoss)
                typeof(BossPatternController).GetMethod("OnDisable", Flags).Invoke(boss, null);
            else
                boss.ForceClearCombatHazards();
            Assert.That(rush.clip, Is.Null);
            Assert.That(rush.isPlaying, Is.False);
            Assert.That(other.clip, Is.SameAs(clip));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void BossPrefabHasASeparateNamedChargeImpactSound()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab");
        Assert.That(prefab, Is.Not.Null);
        using (var data = new SerializedObject(prefab.GetComponent<BossPatternController>()))
        {
            var impact = data.FindProperty("chargeHitSfx").objectReferenceValue as AudioClip;
            Assert.That(impact, Is.Not.Null);
            Assert.That(impact.name, Does.StartWith("Boss_ChargeHit"));
            Assert.That(impact, Is.Not.SameAs(data.FindProperty("cleaveSfx").objectReferenceValue));
            Assert.That(data.FindProperty("chargeHitSfxVolume").floatValue, Is.InRange(0f, GameAudioManager.MaxSFXGain));
        }
    }

    [Test]
    public void ImpactSoundBelongsToOnlyOneAttackWindow()
    {
        var host = new GameObject("ChargeImpactWindowTest");
        var clip = AudioClip.Create("ChargeImpactTest", 128, 1, 44100, false);
        try
        {
            var hitbox = host.AddComponent<EnemyAttackHitbox>();
            FieldInfo sound = typeof(EnemyAttackHitbox).GetField("windowImpactSfx", Flags);
            FieldInfo volume = typeof(EnemyAttackHitbox).GetField("windowImpactSfxVolume", Flags);
            hitbox.SetWindowImpactAudio(clip, 3f);
            hitbox.SetHitboxWindowActive(true);
            Assert.That(sound.GetValue(hitbox), Is.SameAs(clip));
            Assert.That(volume.GetValue(hitbox), Is.EqualTo(2f));
            hitbox.SetHitboxWindowActive(false);
            Assert.That(sound.GetValue(hitbox), Is.Null);
            hitbox.SetHitboxWindowActive(true);
            Assert.That(sound.GetValue(hitbox), Is.Null, "A later cleave must not inherit charge impact audio.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void DisablingHitboxClearsItsPendingImpactSound()
    {
        var host = new GameObject("ChargeImpactDisableTest");
        var clip = AudioClip.Create("ChargeImpactTest", 128, 1, 44100, false);
        try
        {
            var hitbox = host.AddComponent<EnemyAttackHitbox>();
            hitbox.SetWindowImpactAudio(clip);
            hitbox.SetHitboxWindowActive(true);
            typeof(EnemyAttackHitbox).GetMethod("OnDisable", Flags).Invoke(hitbox, null);
            Assert.That(typeof(EnemyAttackHitbox).GetField("windowImpactSfx", Flags).GetValue(hitbox), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(clip);
        }
    }
}
#endif
