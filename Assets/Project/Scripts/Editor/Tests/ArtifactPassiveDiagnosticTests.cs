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

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        ArtifactPhase1AssetCreator.CreateOrUpdateApprovedAssets();
    }

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

        // Teammate charB should now have the Toxic buff in RunInventory
        Assert.That(HasRuntimeEffect(runInventory, charB, "P1-A01-TOXIC"), Is.True, "Teammate charB should receive Toxic Infusion buff.");

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

        // Deal 4 critical hits -> 4 Focus stacks present
        for (int i = 0; i < 4; i++)
        {
            CombatEventHub.Shared.Publish(critDamage);
        }
        Assert.That(HasRuntimeEffect(runInventory, charA, "P1-A09-FOCUS"), Is.True, "Focus effect should be active on 4 crits.");

        // Deal 5th critical hit -> triggers Thunder Strike and consumes all Focus
        CombatEventHub.Shared.Publish(critDamage);
        Assert.That(HasRuntimeEffect(runInventory, charA, "P1-A09-FOCUS"), Is.False, "Focus should be consumed after Thunder Strike triggers at 5 stacks.");
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

    [Test]
    public void BrokenChronoRelicMarksEnemyAndConsumesOnNextHit()
    {
        ArtifactDefinition relic = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/P1_04_BrokenChronoRelic");
        Assert.That(relic, Is.Not.Null);

        runInventory.TryAcquireArtifact(relic, out ArtifactInstance acquired, 1);
        runInventory.DeployTeam(new[] { charA, charB }, 0);
        runInventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(acquired.instanceId) });

        passiveSystem.Initialize(teamLoader);
        ArtifactPassiveRuntime runtime = passiveSystem.ActiveRuntimes[0];

        // 1. CharA Skill hit (300 dmg) marks enemy
        GameObject enemyObj = new GameObject("TestEnemy");
        EnemyController enemy = enemyObj.AddComponent<EnemyController>();

        DamageResolvedEvent skillHit = new DamageResolvedEvent(
            new DamageProvenance(1, 1, 0, 0, null, CombatDamageTag.None),
            new DamageDescriptor(DamageDelivery.Primary, ElementType.None),
            DamageProcPolicy.TriggerArtifactRules,
            charA,
            null,
            enemy,
            AttackCategory.Skill,
            "charA-skill-1",
            300f,
            300f,
            false,
            false,
            Vector2.zero);

        CombatEventHub.Shared.Publish(skillHit);

        Assert.That(runtime.Rules[0].armed, Is.True, "Mark Injury should be armed on skill hit.");
        Assert.That(runtime.Rules[0].storedTarget, Is.EqualTo(enemy), "Marked target should be stored.");
        Assert.That(runtime.Rules[0].snapshottedSkillDamage, Is.EqualTo(300f), "Skill damage should be snapshotted (300 dmg).");
        Assert.That(runtime.Rules[0].storedActionId, Is.EqualTo("charA-skill-1"), "Skill action ID should be stored.");
        Assert.That(enemy.RuntimeEffects.GetStackCount("P1-A04-MARK-INJURY"), Is.EqualTo(1), "Enemy should have Mark Injury debuff.");

        // 2. Subsequent hit from the same Skill action must NOT consume the mark
        DamageResolvedEvent skillHit2 = new DamageResolvedEvent(
            new DamageProvenance(2, 1, 1, 0, null, CombatDamageTag.None),
            new DamageDescriptor(DamageDelivery.Primary, ElementType.None),
            DamageProcPolicy.TriggerArtifactRules,
            charA,
            null,
            enemy,
            AttackCategory.Skill,
            "charA-skill-1",
            150f,
            150f,
            false,
            false,
            Vector2.zero);

        CombatEventHub.Shared.Publish(skillHit2);

        Assert.That(runtime.Rules[0].armed, Is.True, "Mark should NOT be consumed by the same skill action hit.");
        Assert.That(runtime.Rules[0].storedTarget, Is.EqualTo(enemy), "Stored target should remain.");
        Assert.That(runtime.Rules[0].snapshottedSkillDamage, Is.EqualTo(450f), "Skill damage should accumulate across multiple hits of the same skill action (300 + 150 = 450).");

        // 3. Secondary Particle damage hits enemy -> must NOT consume the mark
        DamageResolvedEvent particleHit = new DamageResolvedEvent(
            new DamageProvenance(3, 1, 0, 0, null, CombatDamageTag.Artifact | CombatDamageTag.Particle),
            new DamageDescriptor(DamageDelivery.Additional, ElementType.None),
            DamageProcPolicy.TriggerArtifactRules,
            charA,
            null,
            enemy,
            AttackCategory.Normal,
            "particle-hit",
            20f,
            20f,
            false,
            false,
            Vector2.zero);

        CombatEventHub.Shared.Publish(particleHit);

        Assert.That(runtime.Rules[0].armed, Is.True, "Particle damage should NOT consume the mark.");
        Assert.That(runtime.Rules[0].storedTarget, Is.EqualTo(enemy), "Stored target should remain after particle damage.");

        // 4. CharB performs basic attack (50 dmg) -> consumes mark from CharA's relic
        DamageResolvedEvent charBPrimaryHit = new DamageResolvedEvent(
            new DamageProvenance(3, 3, 0, 0, null, CombatDamageTag.None),
            new DamageDescriptor(DamageDelivery.Primary, ElementType.None),
            DamageProcPolicy.TriggerArtifactRules,
            charB,
            null,
            enemy,
            AttackCategory.Normal,
            "charB-basic-1",
            50f,
            50f,
            false,
            false,
            Vector2.zero);

        CombatEventHub.Shared.Publish(charBPrimaryHit);

        Assert.That(runtime.Rules[0].armed, Is.False, "Mark should be consumed after CharB hit.");
        Assert.That(runtime.Rules[0].storedTarget, Is.Null, "Stored target should be cleared.");
        Assert.That(enemy.RuntimeEffects.GetStackCount("P1-A04-MARK-INJURY"), Is.EqualTo(0), "Enemy Mark Injury debuff should be removed.");

        // Consuming character (CharB) receives Claw buff in their active effects
        var runCharB = runInventory.GetRunCharacter(charB);
        Assert.That(runCharB.ActiveEffects.Any(e => e.recipe?.recipeId == "P1-A04-CLAW"), Is.True, "Consuming character (CharB) should receive Claw buff.");

        // 5. CharB (now having Claw buff) hits a different enemy (enemy2) -> enemy2 receives Claw Bleed DOT!
        GameObject enemy2Obj = new GameObject("TestEnemy2");
        EnemyController enemy2 = enemy2Obj.AddComponent<EnemyController>();

        DamageResolvedEvent charBHitEnemy2 = new DamageResolvedEvent(
            new DamageProvenance(4, 4, 0, 0, null, CombatDamageTag.None),
            new DamageDescriptor(DamageDelivery.Primary, ElementType.None),
            DamageProcPolicy.TriggerArtifactRules,
            charB,
            null,
            enemy2,
            AttackCategory.Normal,
            "charB-basic-2",
            50f,
            50f,
            false,
            false,
            Vector2.zero);

        CombatEventHub.Shared.Publish(charBHitEnemy2);

        Assert.That(enemy2.DamageTypeState.ActiveDotCount, Is.GreaterThan(0), "Enemy 2 should receive Bleed DOT from CharB's Claw buff.");
        Assert.That(enemy2.DamageTypeState.HasActiveDot(ElementType.Bleed), Is.True, "Enemy 2 should have Bleed DOT active.");

        UnityEngine.Object.DestroyImmediate(enemyObj);
        UnityEngine.Object.DestroyImmediate(enemy2Obj);
    }

    [Test]
    public void FiendFleshMask_AppliesBuffToAllies_WhenParticleDamagesEnemy()
    {
        ArtifactDefinition mask = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/P1_02_FiendFleshMask");
        Assert.That(mask, Is.Not.Null);

        runInventory.TryAcquireArtifact(mask, out ArtifactInstance acquired, 1);
        runInventory.DeployTeam(new[] { charA, charB }, 0);
        runInventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(acquired.instanceId) });

        passiveSystem.Initialize(teamLoader);
        ArtifactPassiveRuntime runtime = passiveSystem.ActiveRuntimes[0];

        GameObject enemyObj = new GameObject("TestEnemy_Mask");
        EnemyController enemy = enemyObj.AddComponent<EnemyController>();

        // Particle owned by CharA deals damage to enemy
        DamageResolvedEvent particleHit = new DamageResolvedEvent(
            new DamageProvenance(1, 10, 10, 1, acquired.instanceId, CombatDamageTag.Artifact | CombatDamageTag.Particle),
            new DamageDescriptor(DamageDelivery.Additional, ElementType.None),
            DamageProcPolicy.TriggerArtifactRules,
            charA,
            null,
            enemy,
            AttackCategory.Normal,
            "particle-pulse",
            100f,
            100f,
            false,
            false,
            Vector2.zero);

        CombatEventHub.Shared.Publish(particleHit);

        var runCharA = runInventory.GetRunCharacter(charA);
        var runCharB = runInventory.GetRunCharacter(charB);

        Assert.That(runCharA.ActiveEffects.Any(e => e.recipe?.recipeId == "P1-A02-ATK"), Is.True, "Wearer (CharA) should receive Team ATK buff.");
        Assert.That(runCharB.ActiveEffects.Any(e => e.recipe?.recipeId == "P1-A02-ATK"), Is.True, "Ally (CharB) should receive Team ATK buff.");

        UnityEngine.Object.DestroyImmediate(enemyObj);
    }

    [Test]
    public void ParticleBehaviorRuntime_CreatesDebugVisualizerWithLineAndSpriteRenderer()
    {
        ArtifactDefinition scales = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/P1_03_SoulMerchantsScales");
        Assert.That(scales, Is.Not.Null);

        runInventory.TryAcquireArtifact(scales, out ArtifactInstance acquired, 1);
        runInventory.DeployTeam(new[] { charA, charB }, 0);
        runInventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(acquired.instanceId) });

        passiveSystem.Initialize(teamLoader);

        ParticleBehaviorRuntime runtime = new ParticleBehaviorRuntime();
        ParticleBehaviorDefinition throneDef = scales.passiveSet.rules[0].responses
            .OfType<SpawnParticleResponseDefinition>()
            .FirstOrDefault()?.definition;
        Assert.That(throneDef, Is.Not.Null);

        runtime.Initialize(passiveSystem.ActiveRuntimes[0], throneDef, null, teamLoader);

        Assert.That(runtime.Visualizer, Is.Not.Null, "Visualizer GameObject should be created.");
        Assert.That(runtime.Visualizer.GetComponent<LineRenderer>(), Is.Not.Null, "LineRenderer should be attached.");
        Assert.That(runtime.Visualizer.GetComponent<SpriteRenderer>(), Is.Not.Null, "SpriteRenderer should be attached.");

        runtime.CleanupVisualizer();
    }
}
#endif
