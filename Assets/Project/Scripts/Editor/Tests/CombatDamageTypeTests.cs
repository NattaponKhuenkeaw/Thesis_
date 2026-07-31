#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CombatDamageTypeTests
{
    [Test]
    public void EnemyProfileMapsEveryDamageTypeToItsReduction()
    {
        EnemyElementalProfile profile = ScriptableObject.CreateInstance<EnemyElementalProfile>();
        profile.physicalDamageReduction = 10f;
        profile.memoryDamageReduction = 20f;
        profile.echoDamageReduction = 30f;
        profile.ashDamageReduction = 40f;
        profile.voidDamageReduction = 50f;

        Assert.That(profile.GetDamageReductionPercent(ElementType.None), Is.EqualTo(10f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Memory), Is.EqualTo(20f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Echo), Is.EqualTo(30f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Ash), Is.EqualTo(40f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Void), Is.EqualTo(50f));

        Object.DestroyImmediate(profile);
    }

    [Test]
    public void DamageTypeStateAppliesTheConfiguredReduction()
    {
        GameObject enemy = new GameObject("Damage Type Test Enemy");
        EnemyDamageTypeState state = enemy.AddComponent<EnemyDamageTypeState>();
        EnemyElementalProfile profile = ScriptableObject.CreateInstance<EnemyElementalProfile>();
        profile.physicalDamageReduction = 25f;
        profile.voidDamageReduction = 95f;
        state.Initialize(profile);

        Assert.That(state.ModifyIncomingDamage(ElementType.None, 100f), Is.EqualTo(75f));
        Assert.That(state.ModifyIncomingDamage(ElementType.Memory, 100f), Is.EqualTo(100f));
        Assert.That(state.ModifyIncomingDamage(ElementType.Void, 100f), Is.EqualTo(5f).Within(0.001f));
        Assert.That(state.ModifyIncomingDamage(ElementType.Void, 0f), Is.EqualTo(0f));

        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(enemy);
    }

    [Test]
    public void CharacterObjectNoLongerOwnsAnElementType()
    {
        FieldInfo elementField = typeof(CharacterObject).GetField(
            "elementType",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(elementField, Is.Null);
    }

    [Test]
    public void ElementDefinitionExposesStandaloneDotConfiguration()
    {
        ElementDefinition definition = ScriptableObject.CreateInstance<ElementDefinition>();
        definition.dotDuration = 4f;
        definition.dotDamageRatio = 0.25f;
        definition.dotTickInterval = 0.5f;

        Assert.That(definition.dotDuration, Is.EqualTo(4f));
        Assert.That(definition.dotDamageRatio, Is.EqualTo(0.25f));
        Assert.That(definition.dotTickInterval, Is.EqualTo(0.5f));

        Object.DestroyImmediate(definition);
    }

    [Test]
    public void DotBypassesDefenseButStillUsesGeneralDamageReduction()
    {
        float dotDamage = CombatFormula.CalculateIncomingDamage(
            100f,
            500f,
            20f,
            DamageRoutine.Dot);
        float normalDamage = CombatFormula.CalculateIncomingDamage(
            100f,
            500f,
            20f,
            DamageRoutine.Normal);

        Assert.That(dotDamage, Is.EqualTo(80f).Within(0.001f));
        Assert.That(normalDamage, Is.EqualTo(13.333f).Within(0.001f));
    }
}
#endif
