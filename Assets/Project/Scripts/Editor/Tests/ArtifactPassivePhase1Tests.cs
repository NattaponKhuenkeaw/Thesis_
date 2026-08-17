#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using NUnit.Framework;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using UnityEngine;

public sealed class ArtifactPassivePhase1Tests
{
    [Test]
    public void Phase1ResourcesContainNineStableReplacementDefinitions()
    {
        ArtifactDefinition[] artifacts = Resources.LoadAll<ArtifactDefinition>(
            "ItemSO/Artifacts/Phase1");
        string[] ids = artifacts
            .Where(artifact => artifact != null && artifact.id.StartsWith("ART-P1-", StringComparison.Ordinal))
            .Select(artifact => artifact.id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.That(ids, Is.EqualTo(new[]
        {
            "ART-P1-001", "ART-P1-002", "ART-P1-003", "ART-P1-004", "ART-P1-005",
            "ART-P1-006", "ART-P1-007", "ART-P1-008", "ART-P1-009"
        }));
        Assert.That(artifacts.Where(artifact => artifact.id.StartsWith("ART-P1-", StringComparison.Ordinal))
            .All(artifact => artifact.passiveSet != null && artifact.passiveSet.rules.Count > 0), Is.True);
    }

    [Test]
    public void FocusAndThunderShareOnlyTheirInstanceRuntimeState()
    {
        ArtifactDefinition definition = Resources.Load<ArtifactDefinition>(
            "ItemSO/Artifacts/Phase1/P1_09_ShinsRustyDagger");
        ArtifactInstance firstInstance = new ArtifactInstance { instanceId = "first", definitionId = definition.id };
        ArtifactInstance secondInstance = new ArtifactInstance { instanceId = "second", definitionId = definition.id };
        ArtifactPassiveRuntime first = new ArtifactPassiveRuntime(null,
            new ArtifactRuntimeSnapshot(firstInstance, definition));
        ArtifactPassiveRuntime second = new ArtifactPassiveRuntime(null,
            new ArtifactRuntimeSnapshot(secondInstance, definition));

        Assert.That(first.Rules.Length, Is.EqualTo(2));
        Assert.That(ReferenceEquals(first.Rules[0], first.Rules[1]), Is.True);
        Assert.That(ReferenceEquals(first.Rules[0], second.Rules[0]), Is.False);
    }

    [Test]
    public void CompatibilityProcPolicyPreservesPrimaryAndKeepsDotTerminal()
    {
        DamageRequest primary = new DamageRequest
        {
            descriptor = new DamageDescriptor(DamageDelivery.Primary, ElementType.None)
        };
        DamageRequest dot = new DamageRequest
        {
            descriptor = new DamageDescriptor(DamageDelivery.Dot, ElementType.Bleed)
        };

        Assert.That(primary.ResolveProcPolicy().HasFlag(DamageProcPolicy.TriggerOrdinaryOnHit), Is.True);
        Assert.That(primary.ResolveProcPolicy().HasFlag(DamageProcPolicy.GrantAuraEnergy), Is.True);
        Assert.That(dot.ResolveProcPolicy().HasFlag(DamageProcPolicy.TriggerOrdinaryOnHit), Is.False);
        Assert.That(dot.ResolveProcPolicy().HasFlag(DamageProcPolicy.TriggerArtifactRules), Is.False);
    }

    [Test]
    public void ParticleBehaviorHasNoProjectileDataDependency()
    {
        bool hasProjectileField = typeof(ParticleBehaviorDefinition).GetFields()
            .Any(field => field.FieldType == typeof(ProjectileData));
        Assert.That(hasProjectileField, Is.False);
        Assert.That(Enum.GetNames(typeof(DamageDelivery)), Is.EqualTo(new[] { "Primary", "Dot", "Additional" }));
    }

    [Test]
    public void ArtifactRuleRuntimeStateTracksActiveState()
    {
        ArtifactRuleRuntimeState state = new ArtifactRuleRuntimeState();
        Assert.That(state.active, Is.False);
        state.active = true;
        Assert.That(state.active, Is.True);
    }
}
#endif

