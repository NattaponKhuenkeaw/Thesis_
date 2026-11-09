#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CameraGirlFieldVisualTests
{
    private CharacterObject _character;
    private GameObject _host;

    [SetUp]
    public void SetUp()
    {
        _character = AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_1_CameraGirl/CharacterObject_cg.asset");
        Assert.That(_character, Is.Not.Null);
        _host = new GameObject("CameraGirlBuffVisualTest");
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_host);

    [Test]
    public void BothRandomBaseSkillOutcomesAndTheBaseAttackBuffHavePowerUpFeedback()
    {
        PassiveRuntimeEffectDefinition random = _character.kitDefinition.baseSkillNode.runtimeEffects
            .First(effect => effect.enableRandomBuffSelection);
        Assert.That(random.randomBuffPool.Count, Is.EqualTo(2));
        foreach (RuntimeEffectRecipeData recipe in random.randomBuffPool)
            AssertPowerUp(recipe);
        AssertPowerUp(_character.kitDefinition.basePassiveNode.runtimeEffects[0].recipe);
    }

    [Test]
    public void AllThreeSkillUpgradesGiveTheirCriticalRateAndDamageBuffsPowerUpFeedback()
    {
        foreach (string id in new[] { "cg.skill.lv1", "cg.skill.lv2", "cg.skill.lv3" })
        {
            PotentialNodeDefinition node = _character.kitDefinition.GetNodeById(id);
            Assert.That(node.runtimeEffects.Count, Is.EqualTo(id == "cg.skill.lv3" ? 3 : 2));
            foreach (PassiveRuntimeEffectDefinition effect in node.runtimeEffects)
                AssertPowerUp(effect.recipe);
        }
    }

    [Test]
    public void SimultaneousCameraGirlCriticalBuffsKeepOneLoopUntilTheLastBuffExpires()
    {
        PotentialNodeDefinition node = _character.kitDefinition.GetNodeById("cg.skill.lv1");
        RuntimeEffectReceiver receiver = _host.AddComponent<RuntimeEffectReceiver>();
        receiver.ApplyEffect(node.runtimeEffects[0].recipe, _character, _character.stats, 0.1f);
        receiver.ApplyEffect(node.runtimeEffects[1].recipe, _character, _character.stats, 0.2f);
        FieldEffectVisual view = OnlyView();
        Invoke(view, "Sample", 0.4f);
        Invoke(receiver, "Tick", 0.15f);
        Assert.That(receiver.ActiveEffects.Count, Is.EqualTo(1));
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Loop));
        Invoke(receiver, "Tick", 0.1f);
        Assert.That(receiver.ActiveEffects.Count, Is.Zero);
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Ending));
    }

    [Test]
    public void ReapplyingCameraGirlAttackBuffKeepsItsExistingLoop()
    {
        RuntimeEffectRecipe recipe = _character.kitDefinition.basePassiveNode.runtimeEffects[0].recipe;
        RuntimeEffectReceiver receiver = _host.AddComponent<RuntimeEffectReceiver>();
        receiver.ApplyEffect(recipe, _character, _character.stats);
        FieldEffectVisual view = OnlyView();
        Invoke(view, "Sample", 0.4f);
        receiver.ApplyEffect(recipe, _character, _character.stats);
        Assert.That(OnlyView(), Is.SameAs(view));
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Loop));
    }

    private void AssertPowerUp(RuntimeEffectRecipeData recipe)
    {
        Assert.That(recipe.IsValid, Is.True);
        Assert.That(recipe.presentation.fieldVisualPrefab,
            Is.SameAs(Resources.Load<GameObject>("FieldEffects/FieldPowerUp")));
    }

    private FieldEffectVisual OnlyView()
    {
        FieldEffectVisual[] views = _host.GetComponentsInChildren<FieldEffectVisual>(true);
        Assert.That(views.Length, Is.EqualTo(1));
        return views[0];
    }

    private static void Invoke(object target, string method, float seconds) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, new object[] { seconds });
}
#endif
