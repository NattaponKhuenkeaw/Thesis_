#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class FieldEffectVisualTests
{
    private GameObject _host;
    private RuntimeEffectReceiver _receiver;
    private StatRuntimeEffectDefinition _definition;
    private static readonly MethodInfo SampleMethod = typeof(FieldEffectVisual).GetMethod("Sample",
        BindingFlags.Instance | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        _host = new GameObject("FieldVisualTestTarget");
        _host.AddComponent<SpriteRenderer>();
        _receiver = _host.AddComponent<RuntimeEffectReceiver>();
        _definition = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_host);
        Object.DestroyImmediate(_definition);
    }

    [Test]
    public void IndependentEffectsShareOneViewUntilTheLastOwnerIsRemoved()
    {
        RuntimeEffectInstance first = Instance("first", "FieldPowerUp");
        RuntimeEffectInstance second = Instance("second", "FieldPowerUp");
        _receiver.SyncVisuals(new[] { first, second });
        FieldEffectVisual view = OnlyView();
        Sample(view, 0.4f);
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Loop));

        _receiver.SyncVisuals(new[] { second });
        Assert.That(OnlyView(), Is.SameAs(view));
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Loop));
        _receiver.SyncVisuals(null);
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Ending));
        Sample(view, 0.3f);
        Assert.That(view.IsFinished, Is.True);
        Assert.That(view.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void ReapplicationDuringEndingResumesTheSameViewWithoutAnotherEntryBurst()
    {
        RuntimeEffectInstance effect = Instance("buff", "FieldPowerUp");
        _receiver.SyncVisuals(new[] { effect });
        FieldEffectVisual view = OnlyView();
        Sample(view, 0.4f);
        _receiver.SyncVisuals(null);
        Sample(view, 0.1f);
        _receiver.SyncVisuals(new[] { effect });
        Assert.That(OnlyView(), Is.SameAs(view));
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Loop));
        _receiver.SyncVisuals(new[] { effect });
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Loop));
    }

    [Test]
    public void GameplayExpiryStartsAnEndingAnimationInsteadOfDestroyingImmediately()
    {
        RuntimeEffectRecipe recipe = Recipe("timed", "FieldPowerDown");
        recipe.policy.durationType = RuntimeEffectDurationType.Seconds;
        recipe.policy.durationSeconds = 0.1f;
        _receiver.ApplyEffect(recipe, null, null);
        FieldEffectVisual view = OnlyView();
        Sample(view, 0.4f);
        typeof(RuntimeEffectReceiver).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_receiver, new object[] { 0.2f });
        Assert.That(_receiver.ActiveEffects.Count, Is.Zero);
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Ending));
        Assert.That(view.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void DarkFlamesStopEmissionButRetainLiveParticlesUntilTheEndingCompletes()
    {
        _receiver.SyncVisuals(new[] { Instance("toxic", "FieldDarkFlame") });
        FieldEffectVisual view = OnlyView();
        Assert.That(view.flames.main.maxParticles, Is.EqualTo(12));
        view.flames.Emit(20);
        Assert.That(view.flames.particleCount, Is.InRange(1, 12));
        int count = view.flames.particleCount;
        _receiver.SyncVisuals(null);
        Assert.That(view.flames.isEmitting, Is.False);
        Assert.That(view.flames.particleCount, Is.EqualTo(count));
        Sample(view, 0.5f);
        Assert.That(view.flames.particleCount, Is.Zero);
        Assert.That(view.IsFinished, Is.True);
    }

    [Test]
    public void OppositeStylesCanCoexistWhileTheirGameplayEffectsRemainActive()
    {
        RuntimeEffectInstance up = Instance("up", "FieldPowerUp");
        RuntimeEffectInstance down = Instance("down", "FieldPowerDown");
        _receiver.SyncVisuals(new[] { up, down });
        FieldEffectVisual[] views = _host.GetComponentsInChildren<FieldEffectVisual>(true);
        Assert.That(views.Length, Is.EqualTo(2));
        foreach (FieldEffectVisual view in views)
        {
            Sample(view, 120f);
            Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Loop));
        }
        _receiver.SyncVisuals(new[] { down });
        foreach (FieldEffectVisual view in views)
            Assert.That(view.Phase, Is.EqualTo(view.style == FieldEffectVisual.VisualStyle.PowerUp
                ? FieldEffectVisual.AnimationPhase.Ending : FieldEffectVisual.AnimationPhase.Loop));
    }

    [Test]
    public void DisableClearsAndEnableRestoresActualEffectsWithoutChangingTheirStacks()
    {
        _receiver.ApplyEffect(Recipe("active", "FieldPowerUp"), null, null);
        FieldEffectVisual view = OnlyView();
        typeof(RuntimeEffectReceiver).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_receiver, null);
        Assert.That(view.IsFinished, Is.True);
        Assert.That(_receiver.ActiveEffects.Count, Is.EqualTo(1));
        typeof(RuntimeEffectReceiver).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_receiver, null);
        Assert.That(OnlyView(), Is.SameAs(view));
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Enter));
        Assert.That(_receiver.ActiveEffects[0].stackCount, Is.EqualTo(1));
    }

    [TestCase("P1_01_BlindsBoundlessRibbon", "P1-A01-TOXIC", FieldEffectVisual.VisualStyle.DarkFlame)]
    [TestCase("P1_02_FiendFleshMask", "P1-A02-ATK", FieldEffectVisual.VisualStyle.PowerUp)]
    [TestCase("P1_05_AuraResonatingShard", "P1-A05-AFFLICTION", FieldEffectVisual.VisualStyle.PowerDown)]
    [TestCase("P1_07_ShroudOfDeprivation", "P1-A07-SUPPORT", FieldEffectVisual.VisualStyle.PowerUp)]
    public void RequestedArtifactRecipesHaveTheCorrectVisual(string name, string id,
        FieldEffectVisual.VisualStyle style)
    {
        ArtifactPassiveSetDefinition set = Resources.Load<ArtifactPassiveSetDefinition>(
            "ItemSO/Artifacts/Phase1/" + name + "_PassiveSet");
        RuntimeEffectRecipe recipe = null;
        foreach (ArtifactPassiveRuleDefinition rule in set.rules)
            foreach (ArtifactPassiveResponseDefinition response in rule.responses)
                if (response is ApplyRuntimeEffectResponseDefinition apply && apply.recipe.recipeId == id)
                    recipe = apply.recipe;
        Assert.That(recipe, Is.Not.Null);
        Assert.That(recipe.presentation.fieldVisualPrefab, Is.Not.Null);
        Assert.That(recipe.presentation.fieldVisualPrefab.GetComponent<FieldEffectVisual>().style, Is.EqualTo(style));
    }

    private RuntimeEffectRecipe Recipe(string id, string prefab) => new RuntimeEffectRecipe
    {
        recipeId = id,
        definition = _definition,
        presentation = new RuntimeEffectPresentation
        {
            fieldVisualPrefab = Resources.Load<GameObject>("FieldEffects/" + prefab)
        },
        policy = new RuntimeEffectApplicationPolicy { durationType = RuntimeEffectDurationType.Seconds, durationSeconds = 10f }
    };

    private RuntimeEffectInstance Instance(string id, string prefab)
    {
        RuntimeEffectInstance effect = new RuntimeEffectInstance();
        effect.Initialize(Recipe(id, prefab), null, null);
        return effect;
    }

    private FieldEffectVisual OnlyView()
    {
        FieldEffectVisual[] views = _host.GetComponentsInChildren<FieldEffectVisual>(true);
        Assert.That(views.Length, Is.EqualTo(1), "Stacking must not multiply field views.");
        return views[0];
    }

    private static void Sample(FieldEffectVisual view, float seconds) =>
        SampleMethod.Invoke(view, new object[] { seconds });
}
#endif
