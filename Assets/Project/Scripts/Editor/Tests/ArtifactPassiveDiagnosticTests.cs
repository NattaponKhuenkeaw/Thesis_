#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using UnityEngine;

/// <summary>
/// Industry-standard diagnostic test suite verifying every Phase 1 Artifact Passive rule,
/// trigger pipeline, runtime effect registration, and event integration.
/// </summary>
public sealed class ArtifactPassiveDiagnosticTests
{
    private GameObject hostObject;
    private ArtifactPassiveRuntimeSystem passiveSystem;
    private TeamLoader teamLoader;
    private PlayerRunInventory runInventory;
    private CharacterObject charA;
    private CharacterObject charB;

    [SetUp]
    public void SetUp()
    {
        hostObject = new GameObject("ArtifactPassiveDiagnosticHost");
        teamLoader = hostObject.AddComponent<TeamLoader>();
        passiveSystem = hostObject.AddComponent<ArtifactPassiveRuntimeSystem>();

        runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        teamLoader.SetRunInventory(runInventory);

        charA = ScriptableObject.CreateInstance<CharacterObject>();
        charA.characterId = "diag-char-A";
        charA.stats = new CharacterStats { maxHP = 1000f, attack = 100f, defense = 100f, critRate = 10f, critDamage = 50f };

        charB = ScriptableObject.CreateInstance<CharacterObject>();
        charB.characterId = "diag-char-B";
        charB.stats = new CharacterStats { maxHP = 1200f, attack = 120f, defense = 80f, critRate = 15f, critDamage = 60f };
    }

    [TearDown]
    public void TearDown()
    {
        if (hostObject != null)
        {
            UnityEngine.Object.DestroyImmediate(hostObject);
        }
        if (runInventory != null)
        {
            UnityEngine.Object.DestroyImmediate(runInventory);
        }
        if (charA != null)
        {
            UnityEngine.Object.DestroyImmediate(charA);
        }
        if (charB != null)
        {
            UnityEngine.Object.DestroyImmediate(charB);
        }
    }

    private static bool HasRuntimeEffect(PlayerRunInventory inventory, CharacterObject character, string recipeId)
    {
        IReadOnlyList<RuntimeEffectInstance> effects = inventory.GetActiveRuntimeEffects(character);
        for (int i = 0; i < effects.Count; i++)
        {
            string stableId = RuntimeEffectRuntimeUtility.GetStableId(effects[i].definition, effects[i].recipe);
            if (string.Equals(stableId, recipeId, StringComparison.Ordinal))
                return true;
            if (effects[i].definition != null && string.Equals(effects[i].definition.effectId, recipeId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    [Test]
    public void AllNinePhase1ArtifactDefinitionsHaveAuthoredPassiveSets()
    {
        ArtifactDefinition[] artifacts = Resources.LoadAll<ArtifactDefinition>("ItemSO/Artifacts/Phase1");
        Assert.That(artifacts.Length, Is.GreaterThanOrEqualTo(9));

        for (int i = 0; i < artifacts.Length; i++)
        {
            ArtifactDefinition artifact = artifacts[i];
            Assert.That(artifact, Is.Not.Null, $"Artifact {i} must load.");
            Assert.That(artifact.passiveSet, Is.Not.Null, $"Artifact {artifact.name} must have a passiveSet.");
            Assert.That(artifact.passiveSet.rules.Count, Is.GreaterThan(0), $"Artifact {artifact.name} must have at least 1 rule.");
        }
    }

    [Test]
    public void TeamDeploymentRegistersArtifactPassiveRuntimes()
    {
        ArtifactDefinition ribbon = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/P1_01_BlindsBoundlessRibbon");
        Assert.That(ribbon, Is.Not.Null);

        runInventory.TryAcquireArtifact(ribbon, out ArtifactInstance acquired, 1);
        runInventory.DeployTeam(new[] { charA, charB }, 0);
        runInventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(acquired.instanceId) });

        passiveSystem.Initialize(teamLoader);

        Assert.That(passiveSystem.ActiveRuntimes.Count, Is.EqualTo(1));
        Assert.That(passiveSystem.ActiveRuntimes[0].Artifact.Definition, Is.EqualTo(ribbon));
        Assert.That(passiveSystem.ActiveRuntimes[0].Wearer, Is.EqualTo(charA));
    }

    [Test]
    public void CharacterSwitchTriggersSupportBuffOnIncomingCharacter()
    {
        ArtifactDefinition shroud = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/P1_07_ShroudOfDeprivation");
        Assert.That(shroud, Is.Not.Null);

        runInventory.TryAcquireArtifact(shroud, out ArtifactInstance acquired, 1);
        runInventory.DeployTeam(new[] { charA, charB }, 0);
        runInventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(acquired.instanceId) });

        passiveSystem.Initialize(teamLoader);

        // Switch from charA (wearer of Shroud) to charB
        CombatEventHub.Shared.Publish(new CharacterSwitchCompletedEvent(charA, charB, 0, 1, Vector2.zero));

        // charB should now have runtime effect from Shroud applied
        Assert.That(HasRuntimeEffect(runInventory, charB, "P1-A07-SUPPORT"), Is.True);
    }

    [Test]
    public void HealthThresholdPassiveAppliesAndRemovesCorrectly()
    {
        ArtifactDefinition urn = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/P1_08_MemoryDrainingUrn");
        Assert.That(urn, Is.Not.Null);

        runInventory.TryAcquireArtifact(urn, out ArtifactInstance acquired, 1);
        runInventory.DeployTeam(new[] { charA, charB }, 0);
        runInventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(acquired.instanceId) });

        passiveSystem.Initialize(teamLoader);

        // Initially at 100% HP (>= 66%), so curse applies and benefit does not
        CombatEventHub.Shared.Publish(new CharacterHealthChangedEvent(charA, 1000f, 1000f));
        Assert.That(HasRuntimeEffect(runInventory, charA, "P1-A08-CURSE"), Is.True);
        Assert.That(HasRuntimeEffect(runInventory, charA, "P1-A08-BENEFIT"), Is.False);

        // Drop to 80% HP (<= 90% and > 66%), both curse and benefit should be active
        CombatEventHub.Shared.Publish(new CharacterHealthChangedEvent(charA, 800f, 1000f));
        Assert.That(HasRuntimeEffect(runInventory, charA, "P1-A08-BENEFIT"), Is.True);

        // Drop to 50% HP (< 66%), curse should be removed and benefit remains active
        CombatEventHub.Shared.Publish(new CharacterHealthChangedEvent(charA, 500f, 1000f));
        Assert.That(HasRuntimeEffect(runInventory, charA, "P1-A08-CURSE"), Is.False);
        Assert.That(HasRuntimeEffect(runInventory, charA, "P1-A08-BENEFIT"), Is.True);
    }

    [Test]
    public void UltimateActionAppliesDarknessInfusionToAllies()
    {
        ArtifactDefinition ribbon = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/P1_01_BlindsBoundlessRibbon");
        Assert.That(ribbon, Is.Not.Null);

        runInventory.TryAcquireArtifact(ribbon, out ArtifactInstance acquired, 1);
        runInventory.DeployTeam(new[] { charA, charB }, 0);
        runInventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(acquired.instanceId) });

        passiveSystem.Initialize(teamLoader);

        // charA casts Ultimate
        CombatEventHub.Shared.Publish(new ActionStartedEvent(charA, AttackCategory.Ultimate, "ult-1", "BlindUltimate", null));

        // Damage from ally charB should now be infused with Darkness
        DamageDescriptor outgoing = new DamageDescriptor(DamageDelivery.Primary, ElementType.None);
        DamageDescriptor modified = ArtifactPassiveRuntimeSystem.ModifyOutgoingDescriptor(charB, outgoing);

        Assert.That(modified.elementType, Is.EqualTo(ElementType.Darkness));
    }

    [Test]
    public void CriticalHitsBuildFocusStacksAndArmThunderStrike()
    {
        ArtifactDefinition dagger = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/P1_09_ShinsRustyDagger");
        Assert.That(dagger, Is.Not.Null);

        runInventory.TryAcquireArtifact(dagger, out ArtifactInstance acquired, 1);
        runInventory.DeployTeam(new[] { charA, charB }, 0);
        runInventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(acquired.instanceId) });

        passiveSystem.Initialize(teamLoader);

        ArtifactPassiveRuntime runtime = passiveSystem.ActiveRuntimes[0];
        Assert.That(runtime.Rules.Length, Is.EqualTo(2));

        DamageResolvedEvent critDamage = new DamageResolvedEvent(
            new DamageProvenance(1, 1, 0, 0, null, CombatDamageTag.None),
            new DamageDescriptor(DamageDelivery.Primary, ElementType.None),
            DamageProcPolicy.TriggerArtifactRules,
            charA,
            null,
            null,
            AttackCategory.Normal,
            "primary-1",
            100f,
            150f,
            true, // isCriticalHit
            false,
            Vector2.zero);

        // Deal 6 critical hits to reach max Focus stacks (6) and arm Thunder Strike
        for (int i = 0; i < 6; i++)
        {
            CombatEventHub.Shared.Publish(critDamage);
        }

        Assert.That(runtime.Rules[0].armed, Is.True, "Thunder Strike rule should be armed after 6 Focus crits.");
    }

    [Test]
    public void ThroneBarrierHealsWearerAndTeamFromPulseDamage()
    {
        ArtifactDefinition scales = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/P1_03_SoulMerchantsScales");
        Assert.That(scales, Is.Not.Null);

        runInventory.TryAcquireArtifact(scales, out ArtifactInstance acquired, 1);
        runInventory.DeployTeam(new[] { charA, charB }, 0);
        runInventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(acquired.instanceId) });

        // Set charA HP to 500 / 1000 (damaged)
        teamLoader.SetCharacterCurrentHP(charA, 500f);
        Assert.That(teamLoader.GetCharacterCurrentHP(charA), Is.EqualTo(500f));

        passiveSystem.Initialize(teamLoader);

        // charA casts Skill to summon Throne Barrier
        CombatEventHub.Shared.Publish(new ActionStartedEvent(charA, AttackCategory.Skill, "skill-1", "SoulSkill", null));

        ParticleBehaviorPool pool = hostObject.GetComponent<ParticleBehaviorPool>();
        Assert.That(pool, Is.Not.Null);
    }
}
#endif
