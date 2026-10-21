#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using Project.AIBehaviour.Boss;
using UnityEditor;
using UnityEngine;

public class BossCleaveAudioTests
{
    [Test]
    public void BossWarningLayersWithoutReplacingExistingPatternOrDeathSounds()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab");
        Assert.That(prefab, Is.Not.Null);
        var boss = prefab.GetComponent<BossPatternController>();
        Assert.That(boss, Is.Not.Null);
        Assert.That(boss.AttackWarningSfx, Is.Not.Null);
        Assert.That(boss.AttackWarningSfx.name, Does.StartWith("Boss_AttackWarning"));
        Assert.That(boss.AttackWarningSfxVolume, Is.InRange(0f, GameAudioManager.MaxSFXGain));
        Assert.That(AssetDatabase.AssetPathToGUID("Assets/Project/Audio/SFX/Boss_AttackWarning.wav"),
            Is.EqualTo("c6e85dca8c101094aa2201316615848a"));
        var patternSounds = new[] { boss.CleaveSfx, boss.ChargeHitSfx, boss.DeathSfx, boss.JumpTakeoffSfx, boss.JumpLandSfx };
        foreach (var clip in patternSounds)
        {
            Assert.That(clip, Is.Not.Null);
            Assert.That(boss.AttackWarningSfx, Is.Not.SameAs(clip));
        }
    }

    [Test]
    public void ClearingBossHazardsStopsOnlyItsOwnedWarningSource()
    {
        var host = new GameObject("BossWarningCleanupTest");
        host.SetActive(false);
        var clip = AudioClip.Create("BossWarningTest", 128, 1, 44100, false);
        try
        {
            var boss = host.AddComponent<BossPatternController>();
            var warning = host.AddComponent<AudioSource>();
            var other = new GameObject("OtherSound");
            other.transform.SetParent(host.transform);
            var otherSource = other.AddComponent<AudioSource>();
            warning.clip = clip;
            otherSource.clip = clip;
            typeof(BossPatternController).GetField("attackWarningAudioSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(boss, warning);
            boss.ForceClearCombatHazards();
            Assert.That(warning.clip, Is.Null);
            Assert.That(otherSource.clip, Is.SameAs(clip));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void BossPrefabHasNamedCleaveSoundAndEditableGainAndOffset()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab");
        Assert.That(prefab, Is.Not.Null);
        var boss = prefab.GetComponent<BossPatternController>();
        Assert.That(boss, Is.Not.Null);
        using (var data = new SerializedObject(boss))
        {
            var clip = data.FindProperty("cleaveSfx").objectReferenceValue as AudioClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.name, Does.StartWith("Boss_Cleave"));
            Assert.That(data.FindProperty("cleaveSfxVolume").floatValue, Is.InRange(0f, GameAudioManager.MaxSFXGain));
            Assert.That(data.FindProperty("cleaveSfxOffset").floatValue, Is.InRange(-2f, 2f));
        }
    }

    [Test]
    public void ClearingBossHazardsCancelsDelayedCleaveAudio()
    {
        var host = new GameObject("BossCleaveAudioCleanupTest");
        host.SetActive(false);
        try
        {
            var boss = host.AddComponent<BossPatternController>();
            var cue = (EnemyAttackAudioCue)typeof(BossPatternController).GetField("cleaveAudioCue",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(boss);
            cue.Begin(1f, .5f);
            Assert.That(cue.Confirm(.5f), Is.False);
            boss.ForceClearCombatHazards();
            Assert.That(cue.Tick(10f), Is.False);
            Assert.That(cue.Confirm(.5f), Is.False);
        }
        finally { Object.DestroyImmediate(host); }
    }
}
#endif
