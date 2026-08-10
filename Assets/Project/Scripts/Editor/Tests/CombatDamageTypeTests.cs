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
        profile.lightningDamageReduction = 15f;
        profile.bleedDamageReduction = 20f;
        profile.darknessDamageReduction = 25f;

        Assert.That(profile.GetDamageReductionPercent(ElementType.None), Is.EqualTo(10f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Lightning), Is.EqualTo(15f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Bleed), Is.EqualTo(20f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Darkness), Is.EqualTo(25f));

        Object.DestroyImmediate(profile);
    }

    [Test]
    public void DamageTypeStateAppliesTheConfiguredReduction()
    {
        GameObject enemy = new GameObject("Damage Type Test Enemy");
        EnemyDamageTypeState state = enemy.AddComponent<EnemyDamageTypeState>();
        EnemyElementalProfile profile = ScriptableObject.CreateInstance<EnemyElementalProfile>();
        profile.physicalDamageReduction = 25f;
        profile.lightningDamageReduction = 50f;
        state.Initialize(profile);

        Assert.That(state.ModifyIncomingDamage(ElementType.None, 100f), Is.EqualTo(75f));
        Assert.That(state.ModifyIncomingDamage(ElementType.Lightning, 100f), Is.EqualTo(50f));
        Assert.That(state.ModifyIncomingDamage(ElementType.Lightning, 0f), Is.EqualTo(0f));

        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(enemy);
    }

    [Test]
    public void ElementDefinitionExposesStandaloneDotConfigurationAndMaxStacks()
    {
        ElementDefinition definition = ScriptableObject.CreateInstance<ElementDefinition>();
        definition.dotDuration = 4f;
        definition.dotDamageRatio = 0.25f;
        definition.dotTickInterval = 0.5f;
        definition.maxStacks = 5;

        Assert.That(definition.dotDuration, Is.EqualTo(4f));
        Assert.That(definition.dotDamageRatio, Is.EqualTo(0.25f));
        Assert.That(definition.dotTickInterval, Is.EqualTo(0.5f));
        Assert.That(definition.maxStacks, Is.EqualTo(5));

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
        float physicalDamage = CombatFormula.CalculateIncomingDamage(
            100f,
            500f,
            20f,
            DamageRoutine.Physical);

        Assert.That(dotDamage, Is.EqualTo(80f).Within(0.001f));
        Assert.That(physicalDamage, Is.EqualTo(13.333f).Within(0.001f));
    }

    [Test]
    public void ParticleDamageBypassesBothDefenseAndResistance()
    {
        float particleIncoming = CombatFormula.CalculateIncomingDamage(
            100f,
            500f,
            50f,
            DamageRoutine.Particle);

        Assert.That(particleIncoming, Is.EqualTo(100f));
    }

    [Test]
    public void ParticleDamageScalesWithElementalMasteryMultiplier()
    {
        CharacterStats stats = new CharacterStats
        {
            elementalMastery = 50f
        };

        float resolved = CombatFormula.CalculateResolvedDamage(
            100f,
            DamageRoutine.Particle,
            ElementType.Lightning,
            stats,
            500f,
            AttackCategory.Normal,
            out _,
            defenderDamageResistance: 50f,
            allowParticleCrit: false);

        // Particle ignores DEF and RES (100 base) and scales with (1 + 50/100) = 150
        Assert.That(resolved, Is.EqualTo(150f).Within(0.001f));
    }
}
#endif
