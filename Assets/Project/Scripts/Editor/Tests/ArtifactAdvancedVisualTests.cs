#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ArtifactAdvancedVisualTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject _host;
    private GameObject _parent;
    private StatRuntimeEffectDefinition _formula;

    [TearDown]
    public void TearDown()
    {
        if (_host != null) Object.DestroyImmediate(_host);
        if (_parent != null) Object.DestroyImmediate(_parent);
        if (_formula != null) Object.DestroyImmediate(_formula);
    }

    [TestCase(RuntimeEffectDurationType.Forever, 0f)]
    [TestCase(RuntimeEffectDurationType.Seconds, 0f)]
    [TestCase(RuntimeEffectDurationType.Instant, 0f)]
    public void UntimedStatsRemainAppliedWithoutFieldOrStatusEntries(RuntimeEffectDurationType duration, float seconds)
    {
        _formula = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();
        RuntimeEffectRecipe recipe = new RuntimeEffectRecipe
        {
            definition = _formula,
            recipeId = "untimed-test",
            policy = new RuntimeEffectApplicationPolicy { kind = RuntimeEffectKind.Buff, durationType = duration, durationSeconds = seconds },
            presentation = new RuntimeEffectPresentation
            {
                icon = Resources.Load<Sprite>("FieldEffects/Chevron"),
                fieldVisualPrefab = Resources.Load<GameObject>("FieldEffects/FieldPowerUp")
            }
        };
        RuntimeEffectInstance effect = new RuntimeEffectInstance();
        effect.Initialize(recipe, null, null);
        _host = new GameObject("NoDurationPresentation");
        RuntimeEffectReceiver receiver = _host.AddComponent<RuntimeEffectReceiver>();
        receiver.SyncVisuals(new[] { effect });
        Assert.That(_host.GetComponentsInChildren<FieldEffectVisual>(true), Is.Empty);
        _parent = new GameObject("HUD", typeof(RectTransform));
        _parent.SetActive(false);
        PlayerHUD hud = _parent.AddComponent<PlayerHUD>();
        typeof(PlayerHUD).GetMethod("BuildDisplayEntries", Private).Invoke(hud, new object[] { new[] { effect } });
        IList entries = (IList)typeof(PlayerHUD).GetField("runtimeEffectDisplayEntries", Private).GetValue(hud);
        Assert.That(entries.Count, Is.Zero);
        recipe.policy.durationType = RuntimeEffectDurationType.Seconds;
        recipe.policy.durationSeconds = 3f;
        RuntimeEffectInstance timed = new RuntimeEffectInstance();
        timed.Initialize(recipe, null, null);
        typeof(PlayerHUD).GetMethod("BuildDisplayEntries", Private).Invoke(hud, new object[] { new[] { effect, timed } });
        Assert.That(entries.Count, Is.EqualTo(1), "Timed buffs must still have status feedback.");
    }

    [Test]
    public void UrnBenefitIsAnUntimedEffectAndKeepsItsCritModifiers()
    {
        ArtifactPassiveSetDefinition set = Resources.Load<ArtifactPassiveSetDefinition>("ItemSO/Artifacts/Phase1/P1_08_MemoryDrainingUrn_PassiveSet");
        RuntimeEffectRecipe recipe = set.rules.SelectMany(rule => rule.responses)
            .OfType<ApplyRuntimeEffectResponseDefinition>().First(response => response.recipe.recipeId == "P1-A08-BENEFIT").recipe;
        Assert.That(recipe.policy.kind, Is.EqualTo(RuntimeEffectKind.Effect));
        Assert.That(recipe.policy.durationType, Is.EqualTo(RuntimeEffectDurationType.Forever));
        Assert.That(recipe.parameters.statModifiers.Any(stat => stat.target == RuntimeEffectStatTarget.CritRate && stat.value == 17f), Is.True);
        Assert.That(recipe.parameters.statModifiers.Any(stat => stat.target == RuntimeEffectStatTarget.CritDamage && stat.value == 26f), Is.True);
    }

    [Test]
    public void ShieldHasSubtleLoopAndExpandsToTheExactWorldRadiusUnderScaledParents()
    {
        ArtifactAreaVisual shield = Area("ScalesHexShield");
        _parent = new GameObject("ScaledOwner");
        _parent.transform.localScale = new Vector3(-2f, 3f, 1f);
        _host.transform.SetParent(_parent.transform, false);
        Vector2 center = new Vector2(9f, 5f);
        shield.Begin(center, 3.7f);
        Sample(shield, 0.5f);
        Assert.That(shield.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Loop));
        Assert.That(shield.shell.startColor.a, Is.LessThanOrEqualTo(0.18f));
        Assert.That(shield.attackRing.startColor.a, Is.Zero);
        shield.Pulse(center, 3.7f);
        Sample(shield, shield.expansionSeconds);
        Assert.That(shield.CurrentAttackRadius, Is.EqualTo(3.7f).Within(0.001f));
        Assert.That(shield.attackRing.transform.position, Is.EqualTo((Vector3)center));
        Assert.That(Vector3.Distance(shield.attackRing.transform.TransformPoint(shield.attackRing.GetPosition(0)), center),
            Is.EqualTo(3.7f).Within(0.001f));
        shield.Pulse(center, 3.7f, 2f);
        Sample(shield, shield.expansionSeconds);
        Assert.That(shield.CurrentAttackRadii, Is.EqualTo(new Vector2(3.7f, 7.4f)));
        Vector3 top = shield.attackRing.transform.TransformPoint(shield.attackRing.GetPosition(16));
        Assert.That(top.y - center.y, Is.EqualTo(7.4f).Within(0.001f));
        Sample(shield, shield.attackFadeSeconds + 0.01f);
        Assert.That(shield.attackRing.startColor.a, Is.Zero);
        shield.End();
        Assert.That(shield.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Ending));
        Sample(shield, shield.exitSeconds);
        Assert.That(shield.IsFinished, Is.True);
        shield.Begin(center, 3.7f);
        Assert.That(shield.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Enter));
        Assert.That(shield.attackRing.startColor.a, Is.Zero, "Pool reuse must not replay an old attack.");
    }

    [Test]
    public void BloodBurstIsBoundedAndFinishesForReuse()
    {
        ArtifactAreaVisual blood = Area("ChronoBloodExplosion");
        Vector2 center = new Vector2(4f, 2f);
        blood.Begin(center, 2.4f, verticalMultiplier: 2f);
        Assert.That(blood.droplets.particleCount, Is.InRange(1, 48));
        Sample(blood, blood.expansionSeconds);
        Assert.That(blood.CurrentAttackRadius, Is.EqualTo(2.4f));
        blood.droplets.Simulate(0.31f, true, false, false);
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[48];
        int count = blood.droplets.GetParticles(particles);
        for (int i = 0; i < count; i++)
        {
            Vector2 p = (Vector2)particles[i].position - center;
            Assert.That(p.x * p.x / (2.4f * 2.4f) + p.y * p.y / (4.8f * 4.8f), Is.LessThanOrEqualTo(1f));
        }
        Sample(blood, (blood.expansionSeconds + blood.attackFadeSeconds) * blood.shockwaveDurationMultiplier);
        Assert.That(blood.IsFinished, Is.True);
        Assert.That(blood.droplets.particleCount, Is.Zero);
    }

    [Test]
    public void ExpiredMarkCannotBeConsumedByARuleWithoutItsOwnDuration()
    {
        ArtifactRuleRuntimeState state = new ArtifactRuleRuntimeState
        { armed = true, expiresAt = 10f, snapshottedSkillDamage = 100f, storedActionId = "expired-skill" };
        bool allowed = (bool)typeof(ArtifactPassiveRuntimeSystem).GetMethod("CanActivate", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { state, new ArtifactPassiveConditionSet { requireArmed = true, durationSeconds = 0f }, 10f });
        Assert.That(allowed, Is.False);
        Assert.That(state.armed, Is.False);
        Assert.That(state.snapshottedSkillDamage, Is.Zero);
        Assert.That(state.storedActionId, Is.Null);
    }

    [Test]
    public void CircularShieldHasDepthArcsAndMotesWhichStopOnExit()
    {
        ArtifactAreaVisual shield = Area("ScalesHexShield");
        shield.Begin(Vector2.zero, 3f);
        Assert.That(shield.shell.positionCount, Is.GreaterThanOrEqualTo(32));
        Assert.That(shield.orbitArcs.Length, Is.EqualTo(2));
        Assert.That(shield.motes.main.maxParticles, Is.EqualTo(12));
        Assert.That(shield.droplets.main.maxParticles, Is.LessThanOrEqualTo(48));
        Sample(shield, 0.3f);
        Vector3 point = shield.orbitArcs[1].GetPosition(0);
        Sample(shield, 0.2f);
        Assert.That(shield.orbitArcs[1].GetPosition(0), Is.Not.EqualTo(point));
        shield.End();
        Assert.That(shield.motes.isEmitting, Is.False);
        Sample(shield, shield.exitSeconds);
        Assert.That(shield.motes.particleCount, Is.Zero);
    }

    private ArtifactAreaVisual Area(string name)
    {
        _host = Object.Instantiate(Resources.Load<GameObject>("FieldEffects/" + name));
        return _host.GetComponent<ArtifactAreaVisual>();
    }

    [TestCase("ScalesHexShield")]
    [TestCase("ChronoBloodExplosion")]
    [TestCase("DaggerThunderStrike")]
    [TestCase("CrestDarkFlameMark")]
    public void ShockwaveIsLargerAndOutlastsTheDamageRing(string prefab)
    {
        ArtifactAreaVisual visual = Area(prefab);
        visual.Begin(Vector2.zero, 3f, verticalMultiplier: 2f);
        if (visual.style != ArtifactAreaVisual.VisualStyle.BloodExplosion)
            visual.Pulse(Vector2.zero, 3f, 2f);
        Sample(visual, visual.expansionSeconds * visual.shockwaveDurationMultiplier);
        Assert.That(visual.CurrentAttackRadii, Is.EqualTo(new Vector2(3f, 6f)));
        Assert.That(visual.wave.transform.localScale.x, Is.EqualTo(24f).Within(0.001f));
        Assert.That(visual.wave.transform.localScale.y, Is.EqualTo(48f).Within(0.001f));
        Sample(visual, visual.attackFadeSeconds);
        Assert.That(visual.attackRing.startColor.a, Is.Zero);
        Assert.That(visual.wave.color.a, Is.GreaterThan(0f));
        Assert.That(visual.IsFinished, Is.False);
        Sample(visual, visual.attackFadeSeconds * visual.shockwaveDurationMultiplier);
        if (visual.style == ArtifactAreaVisual.VisualStyle.BloodExplosion
            || visual.style == ArtifactAreaVisual.VisualStyle.ThunderStrike)
        {
            Assert.That(visual.IsFinished, Is.True);
            Assert.That(visual.gameObject.activeSelf, Is.False);
        }
        else
            Assert.That(visual.wave.color.a, Is.Zero);
        visual.ClearImmediately();
        visual.Begin(Vector2.zero, 3f);
        if (visual.style != ArtifactAreaVisual.VisualStyle.BloodExplosion)
            Assert.That(visual.wave.color.a, Is.Zero, "Reuse must clear the previous wave tail.");
    }

    [Test]
    public void DisabledOrDestroyedActorClearsShieldImmediatelyAndZeroTimeFreezesMotion()
    {
        ArtifactAreaVisual shield = Area("ScalesHexShield");
        _parent = new GameObject("Wearer");
        shield.Begin(Vector2.zero, 3f, _parent.transform);
        Sample(shield, 0.5f);
        float alpha = shield.shell.startColor.a;
        Sample(shield, 0f);
        Assert.That(shield.shell.startColor.a, Is.EqualTo(alpha));
        _parent.SetActive(false);
        Sample(shield, 0f);
        Assert.That(shield.IsFinished, Is.True);
        _parent.SetActive(true);
        shield.Begin(Vector2.zero, 3f, _parent.transform);
        Object.DestroyImmediate(_parent);
        Sample(shield, 0f);
        Assert.That(shield.IsFinished, Is.True);
    }
    private static void Sample(ArtifactAreaVisual view, float time) => typeof(ArtifactAreaVisual).GetMethod("Sample", Private)
        .Invoke(view, new object[] { time });
}
#endif
