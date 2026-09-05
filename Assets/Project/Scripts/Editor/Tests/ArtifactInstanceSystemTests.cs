#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.Progression;
using UnityEngine;
using UnityEditor;
using UnityEngine.EventSystems;
using Project.TeamBuilder.UI;

public class ArtifactInstanceSystemTests
{
    private sealed class SequenceArtifactRandom : IArtifactRandom
    {
        private readonly Queue<float> values;

        public SequenceArtifactRandom(params float[] values)
        {
            this.values = new Queue<float>(values);
        }

        public float Next01()
        {
            return values.Count > 0 ? values.Dequeue() : 0f;
        }
    }

    private ArtifactRollTable table;
    private ArtifactDefinition definition;

    [SetUp]
    public void SetUp()
    {
        table = ArtifactRollTable.CreateRuntimeDefault();
        definition = ScriptableObject.CreateInstance<ArtifactDefinition>();
        definition.id = "artifact-test";
        definition.itemName = "Test Artifact";
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(definition);
        UnityEngine.Object.DestroyImmediate(table);
    }

    [TestCase(0f, 4)]
    [TestCase(0.14999f, 4)]
    [TestCase(0.15f, 3)]
    [TestCase(0.34999f, 3)]
    [TestCase(0.35f, 2)]
    [TestCase(0.49999f, 2)]
    [TestCase(0.50f, 1)]
    [TestCase(1f, 1)]
    public void LineCountUsesApprovedCumulativeOdds(float percentile, int expected)
    {
        Assert.That(table.RollStatLineCount(percentile), Is.EqualTo(expected));
    }

    [Test]
    public void GeneratedRollsStayInCatalogRangesAndAllowDuplicateStats()
    {
        ArtifactRollGenerator generator = new ArtifactRollGenerator(table);
        bool foundDuplicate = false;

        for (int seed = 0; seed < 2000; seed++)
        {
            ArtifactInstance instance = generator.Generate(definition, seed, seed, $"test-{seed}");
            Assert.That(instance.rolls.Count, Is.InRange(1, 4));
            HashSet<ArtifactStatKind> kinds = new HashSet<ArtifactStatKind>();
            for (int i = 0; i < instance.rolls.Count; i++)
            {
                ArtifactRoll roll = instance.rolls[i];
                Assert.That(table.TryGet(roll.statKind, out ArtifactStatRollDefinition range), Is.True);
                Assert.That(roll.valueUnits, Is.InRange(range.minimumUnits, range.maximumUnits));
                if (!kinds.Add(roll.statKind)) foundDuplicate = true;
            }
        }

        Assert.That(foundDuplicate, Is.True, "Stat selection must be with replacement.");
    }

    [TestCase(0.10f, 0.00f, 0.20f)]
    [TestCase(0.40f, 0.20f, 0.40f)]
    [TestCase(0.70f, 0.40f, 0.60f)]
    [TestCase(0.90f, 0.60f, 0.80f)]
    [TestCase(0.98f, 0.80f, 1.00f)]
    public void AmountBandSelectionUsesApprovedWeightedThresholds(
        float bandRoll,
        float expectedMinimumNormalized,
        float expectedMaximumNormalized)
    {
        ArtifactRollGenerator generator = new ArtifactRollGenerator(table);
        SequenceArtifactRandom random = new SequenceArtifactRandom(
            0.9f,     // one stat line
            0f,       // first eligible stat: Flat HP
            bandRoll,
            0.5f);    // midpoint within the selected band

        ArtifactInstance instance = generator.Generate(definition, 1, random);
        ArtifactRoll roll = instance.rolls[0];
        Assert.That(table.TryGet(roll.statKind, out ArtifactStatRollDefinition range), Is.True);

        float normalized = Mathf.InverseLerp(range.minimumUnits, range.maximumUnits, roll.valueUnits);
        Assert.That(normalized, Is.InRange(expectedMinimumNormalized, expectedMaximumNormalized));
    }

    [Test]
    public void DuplicateDefinitionsProduceIndependentInstances()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            Assert.That(inventory.TryAcquireArtifact(definition, out ArtifactInstance first, 100), Is.True);
            Assert.That(inventory.TryAcquireArtifact(definition, out ArtifactInstance second, 101), Is.True);
            Assert.That(first.instanceId, Is.Not.EqualTo(second.instanceId));
            Assert.That(first.definitionId, Is.EqualTo(second.definitionId));
            Assert.That(inventory.GetItemCount(definition), Is.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void InstanceRemovalCannotDeleteAnEquippedCopyOrItsSibling()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            inventory.TryAcquireArtifact(definition, out ArtifactInstance equipped, 200);
            inventory.TryAcquireArtifact(definition, out ArtifactInstance sellable, 201);
            inventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(equipped.instanceId) });

            Assert.That(inventory.TryRemoveArtifactInstance(equipped.instanceId), Is.False);
            Assert.That(inventory.TryRemoveArtifactInstance(sellable.instanceId), Is.True);
            Assert.That(inventory.TryGetArtifactInstance(equipped.instanceId, out _), Is.True);
            Assert.That(inventory.GetItemCount(definition), Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void DecimalPercentAndFlatRollsAggregateFromCleanBase()
    {
        CharacterStats cleanBase = new CharacterStats { maxHP = 1000f, attack = 100f, critRate = 5f };
        CharacterStats result = cleanBase.Clone();
        ArtifactInstance instance = new ArtifactInstance
        {
            rolls = new List<ArtifactRoll>
            {
                new ArtifactRoll(ArtifactStatKind.FlatMaxHP, 153, ArtifactRollSource.Generated),
                new ArtifactRoll(ArtifactStatKind.AttackPercent, 1234, ArtifactRollSource.Generated),
                new ArtifactRoll(ArtifactStatKind.CritRate, 466, ArtifactRollSource.Generated)
            }
        };

        ArtifactStatAggregator.Apply(result, cleanBase, new[] { instance });

        Assert.That(result.maxHP, Is.EqualTo(1153f));
        Assert.That(result.attack, Is.EqualTo(112.34f).Within(0.001f));
        Assert.That(result.critRate, Is.EqualTo(9.66f).Within(0.001f));
    }

    [Test]
    public void ImportedLegacyRollHasUnavailableQuality()
    {
        ArtifactInstance imported = new ArtifactInstance
        {
            rolls = new List<ArtifactRoll>
            {
                new ArtifactRoll(ArtifactStatKind.DamageIncrease, 2000, ArtifactRollSource.ImportedLegacy)
            }
        };

        Assert.That(ArtifactRollQuality.TryGetAverageQuality(imported, table, out _), Is.False);
    }

    [Test]
    public void LegacySpeedIsRuntimeOnlyAndNotPlayerVisible()
    {
        Assert.That(ArtifactStatCatalog.IsPlayerVisible(ArtifactStatKind.LegacySystemSpeed), Is.False);
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.LegacySystemSpeed), Is.EqualTo("Hidden SPD (legacy)"));
    }

    [Test]
    public void ArtifactStatCatalogReturnsFullDisplayNames()
    {
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.CritRate), Is.EqualTo("CRIT RATE"));
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.CritDamage), Is.EqualTo("CRIT DAMAGE"));
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.CooldownMastery), Is.EqualTo("COOLDOWN MASTERY"));
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.DotMastery), Is.EqualTo("DOT MASTERY"));
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.DamageIncrease), Is.EqualTo("DAMAGE INCREASE"));
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.AdditionalDamageIncrease), Is.EqualTo("ADDITIONAL DAMAGE INCREASE"));
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.AuraEnergyRecharge), Is.EqualTo("AURA ENERGY RECHARGE"));
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.FlatMaxHP), Is.EqualTo("HP"));
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.FlatAttack), Is.EqualTo("ATK"));
        Assert.That(ArtifactStatCatalog.GetLabel(ArtifactStatKind.FlatDefense), Is.EqualTo("DEF"));
    }

    [Test]
    public void ArtifactInventoryCardsAreClickOnlyWhileEquippedSlotsRemainDraggable()
    {
        Assert.That(typeof(IPointerClickHandler).IsAssignableFrom(typeof(ArtifactGridItemUI)), Is.True);
        Assert.That(typeof(IBeginDragHandler).IsAssignableFrom(typeof(ArtifactGridItemUI)), Is.False);
        Assert.That(typeof(IDragHandler).IsAssignableFrom(typeof(ArtifactGridItemUI)), Is.False);
        Assert.That(typeof(IEndDragHandler).IsAssignableFrom(typeof(ArtifactGridItemUI)), Is.False);

        Assert.That(typeof(IPointerClickHandler).IsAssignableFrom(typeof(ArtifactSlotUI)), Is.True);
        Assert.That(typeof(IBeginDragHandler).IsAssignableFrom(typeof(ArtifactSlotUI)), Is.True);
        Assert.That(typeof(IDragHandler).IsAssignableFrom(typeof(ArtifactSlotUI)), Is.True);
        Assert.That(typeof(IEndDragHandler).IsAssignableFrom(typeof(ArtifactSlotUI)), Is.True);
        Assert.That(typeof(IDropHandler).IsAssignableFrom(typeof(ArtifactSlotUI)), Is.True);
    }

    [Test]
    public void QueryFiltersAndSortsDuplicateInstancesWithoutCollapsingThem()
    {
        ArtifactInstance low = new ArtifactInstance
        {
            instanceId = "low",
            definitionId = definition.id,
            acquisitionOrder = 1,
            rolls = new List<ArtifactRoll>
            {
                new ArtifactRoll(ArtifactStatKind.CritRate, 466, ArtifactRollSource.Generated)
            }
        };
        ArtifactInstance high = new ArtifactInstance
        {
            instanceId = "high",
            definitionId = definition.id,
            acquisitionOrder = 2,
            isFavourite = true,
            rolls = new List<ArtifactRoll>
            {
                new ArtifactRoll(ArtifactStatKind.CritRate, 2334, ArtifactRollSource.Generated)
            }
        };
        ArtifactInventoryQuery query = ArtifactInventoryQuery.Default;
        query.sortMode = ArtifactSortMode.AverageRollQuality;
        query.requiredStatMask = ArtifactInventoryQueryEngine.MaskFor(ArtifactStatKind.CritRate);
        List<ArtifactInstance> results = new List<ArtifactInstance>();

        ArtifactInventoryQueryEngine.Execute(
            new[] { low, high },
            query,
            table,
            _ => definition,
            new HashSet<string>(),
            results);
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results[0].instanceId, Is.EqualTo("high"));
        Assert.That(results[1].instanceId, Is.EqualTo("low"));
    }

    [Test]
    public void QuerySortsNewestAndOldestDeterministicallyWithDescendingToggle()
    {
        ArtifactInstance first = new ArtifactInstance { instanceId = "id-1", definitionId = definition.id, acquisitionOrder = 10 };
        ArtifactInstance second = new ArtifactInstance { instanceId = "id-2", definitionId = definition.id, acquisitionOrder = 20 };
        ArtifactInstance third = new ArtifactInstance { instanceId = "id-3", definitionId = definition.id, acquisitionOrder = 30 };

        List<ArtifactInstance> results = new List<ArtifactInstance>();

        // Newest (Descending = true -> largest acquisitionOrder first)
        ArtifactInventoryQuery queryNewest = ArtifactInventoryQuery.Default;
        queryNewest.sortMode = ArtifactSortMode.Newest;
        queryNewest.descending = true;
        ArtifactInventoryQueryEngine.Execute(new[] { second, first, third }, queryNewest, table, _ => definition, new HashSet<string>(), results);
        Assert.That(results[0].instanceId, Is.EqualTo("id-3"));
        Assert.That(results[1].instanceId, Is.EqualTo("id-2"));
        Assert.That(results[2].instanceId, Is.EqualTo("id-1"));

        // Newest with Descending = false -> inverted (smallest acquisitionOrder first)
        queryNewest.descending = false;
        ArtifactInventoryQueryEngine.Execute(new[] { second, first, third }, queryNewest, table, _ => definition, new HashSet<string>(), results);
        Assert.That(results[0].instanceId, Is.EqualTo("id-1"));
        Assert.That(results[1].instanceId, Is.EqualTo("id-2"));
        Assert.That(results[2].instanceId, Is.EqualTo("id-3"));

        // Oldest (Descending = true -> smallest acquisitionOrder first)
        ArtifactInventoryQuery queryOldest = ArtifactInventoryQuery.Default;
        queryOldest.sortMode = ArtifactSortMode.Oldest;
        queryOldest.descending = true;
        ArtifactInventoryQueryEngine.Execute(new[] { second, first, third }, queryOldest, table, _ => definition, new HashSet<string>(), results);
        Assert.That(results[0].instanceId, Is.EqualTo("id-1"));
        Assert.That(results[1].instanceId, Is.EqualTo("id-2"));
        Assert.That(results[2].instanceId, Is.EqualTo("id-3"));
    }

    [Test]
    public void DeployedArtifactSnapshotDoesNotTrackMutableInventoryRollList()
    {
        ArtifactInstance instance = new ArtifactInstance
        {
            instanceId = "snapshot",
            definitionId = definition.id,
            rolls = new List<ArtifactRoll>
            {
                new ArtifactRoll(ArtifactStatKind.DamageIncrease, 1000, ArtifactRollSource.Generated)
            }
        };
        ArtifactRuntimeSnapshot snapshot = new ArtifactRuntimeSnapshot(instance, definition);

        instance.rolls[0] = new ArtifactRoll(ArtifactStatKind.DamageIncrease, 5000, ArtifactRollSource.Generated);

        Assert.That(snapshot.Rolls[0].Value, Is.EqualTo(10f));
    }

    [Test]
    public void MetaMigrationDropsOnlyArtifactFragments()
    {
        PlayerMetaProgression progression = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        const string json = "{\"fragments\":[{\"sourceItemId\":\"char\",\"fragmentType\":0,\"quantity\":3},{\"sourceItemId\":\"artifact\",\"fragmentType\":1,\"quantity\":9}],\"characterPotentials\":[{\"characterId\":\"char\",\"skillLevel\":2}]}";
        try
        {
            progression.ImportJson(json);

            Assert.That(progression.GetFragmentCount("char", ProgressionFragmentType.CharacterPotential), Is.EqualTo(3));
#pragma warning disable CS0618
            Assert.That(progression.GetFragmentCount("artifact", ProgressionFragmentType.ArtifactTranscendence), Is.Zero);
#pragma warning restore CS0618
            Assert.That(progression.GetPotentialLevel("char", PotentialTrack.Skill), Is.EqualTo(2));
            StringAssert.DoesNotContain("artifact", progression.ExportJson());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(progression);
        }
    }

    [Test]
    public void AllNineAuthoredArtifactsMigrateWithoutChangingStats()
    {
        string[] guids = AssetDatabase.FindAssets("t:Artifact", new[] { "Assets/Project/Resources/ItemSO" });
        Assert.That(guids.Length, Is.EqualTo(9));

        for (int i = 0; i < guids.Length; i++)
        {
            Artifact artifact = AssetDatabase.LoadAssetAtPath<Artifact>(AssetDatabase.GUIDToAssetPath(guids[i]));
            Assert.That(artifact, Is.Not.Null);
            CharacterStats cleanBase = new CharacterStats { maxHP = 1000f, attack = 100f, defense = 100f };
            CharacterStats expected = cleanBase.Clone();
            expected.ApplyArtifactBonuses(cleanBase, artifact.bonusStats);
            // SPD is now system-only, so the shared buildable-stat helper intentionally omits it.
            // Legacy Artifact a4 authored a hidden -1 SPD modifier and the importer must preserve it exactly.
            expected.speed += artifact.bonusStats != null ? artifact.bonusStats.speed : 0f;
            CharacterStats migrated = cleanBase.Clone();
            ArtifactInstance instance = ArtifactLegacyImporter.Create(artifact, i, $"legacy-{i}");
            ArtifactStatAggregator.Apply(migrated, cleanBase, new[] { instance });

            AssertStatsEqual(expected, migrated, artifact.name);
            Assert.That(ArtifactRollQuality.TryGetAverageQuality(instance, table, out _), Is.False);
        }
    }

    [Test]
    public void ArtifactStatAggregatorCalculatesAllRolledStats()
    {
        CharacterStats cleanBase = new CharacterStats
        {
            maxHP = 1000f,
            attack = 100f,
            defense = 50f
        };
        CharacterStats destination = cleanBase.Clone();

        ArtifactInstance instance = new ArtifactInstance
        {
            instanceId = "test-all-stats",
            definitionId = definition.id,
            rolls = new List<ArtifactRoll>
            {
                new ArtifactRoll(ArtifactStatKind.FlatMaxHP, 25000, ArtifactRollSource.Generated),     // +250 HP
                new ArtifactRoll(ArtifactStatKind.MaxHPPercent, 1000, ArtifactRollSource.Generated),   // +10% Max HP (100)
                new ArtifactRoll(ArtifactStatKind.FlatAttack, 5000, ArtifactRollSource.Generated),      // +50 ATK
                new ArtifactRoll(ArtifactStatKind.AttackPercent, 2000, ArtifactRollSource.Generated),  // +20% ATK (20)
                new ArtifactRoll(ArtifactStatKind.FlatDefense, 2500, ArtifactRollSource.Generated),    // +25 DEF
                new ArtifactRoll(ArtifactStatKind.DefensePercent, 1000, ArtifactRollSource.Generated), // +10% DEF (5)
                new ArtifactRoll(ArtifactStatKind.CritRate, 1500, ArtifactRollSource.Generated),       // +15% Crit Rate
                new ArtifactRoll(ArtifactStatKind.CritDamage, 3000, ArtifactRollSource.Generated),     // +30% Crit DMG
                new ArtifactRoll(ArtifactStatKind.CooldownMastery, 1200, ArtifactRollSource.Generated),// +12% CD Mastery
                new ArtifactRoll(ArtifactStatKind.DotMastery, 2500, ArtifactRollSource.Generated),     // +25% DoT Mastery
                new ArtifactRoll(ArtifactStatKind.DamageIncrease, 1800, ArtifactRollSource.Generated), // +18% Damage Increase
                new ArtifactRoll(ArtifactStatKind.AdditionalDamageIncrease, 2200, ArtifactRollSource.Generated), // +22% Add Damage Increase
                new ArtifactRoll(ArtifactStatKind.AuraEnergyRecharge, 3000, ArtifactRollSource.Generated) // +30% AE Recharge
            }
        };

        ArtifactStatAggregator.Apply(destination, cleanBase, new[] { instance });

        Assert.That(destination.maxHP, Is.EqualTo(1350f).Within(0.01f));
        Assert.That(destination.attack, Is.EqualTo(170f).Within(0.01f));
        Assert.That(destination.defense, Is.EqualTo(80f).Within(0.01f));
        Assert.That(destination.critRate, Is.EqualTo(15f).Within(0.01f));
        Assert.That(destination.critDamage, Is.EqualTo(30f).Within(0.01f));
        Assert.That(destination.cooldownMastery, Is.EqualTo(12f).Within(0.01f));
        Assert.That(destination.dotMastery, Is.EqualTo(25f).Within(0.01f));
        Assert.That(destination.damageIncrease, Is.EqualTo(18f).Within(0.01f));
        Assert.That(destination.additionalDamageIncrease, Is.EqualTo(22f).Within(0.01f));
        Assert.That(destination.auraEnergyRecharge, Is.EqualTo(30f).Within(0.01f));
    }

    private static void AssertStatsEqual(CharacterStats expected, CharacterStats actual, string context)
    {
        Assert.That(actual.maxHP, Is.EqualTo(expected.maxHP).Within(0.001f), context);
        Assert.That(actual.attack, Is.EqualTo(expected.attack).Within(0.001f), context);
        Assert.That(actual.defense, Is.EqualTo(expected.defense).Within(0.001f), context);
        Assert.That(actual.speed, Is.EqualTo(expected.speed).Within(0.001f), context);
        Assert.That(actual.cooldownMastery, Is.EqualTo(expected.cooldownMastery).Within(0.001f), context);
        Assert.That(actual.critRate, Is.EqualTo(expected.critRate).Within(0.001f), context);
        Assert.That(actual.critDamage, Is.EqualTo(expected.critDamage).Within(0.001f), context);
        Assert.That(actual.dotMastery, Is.EqualTo(expected.dotMastery).Within(0.001f), context);
        Assert.That(actual.damageIncrease, Is.EqualTo(expected.damageIncrease).Within(0.001f), context);
        Assert.That(actual.additionalDamageIncrease, Is.EqualTo(expected.additionalDamageIncrease).Within(0.001f), context);
        Assert.That(actual.auraEnergyRecharge, Is.EqualTo(expected.auraEnergyRecharge).Within(0.001f), context);
    }

    [Test]
    public void AcquisitionOrderPreservesSequentialInsertion()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            Assert.That(inventory.TryAcquireArtifact(definition, out ArtifactInstance first, 100), Is.True);
            Assert.That(inventory.TryAcquireArtifact(definition, out ArtifactInstance second, 101), Is.True);
            Assert.That(inventory.TryAcquireArtifact(definition, out ArtifactInstance third, 102), Is.True);

            Assert.That(first.acquisitionOrder, Is.LessThan(second.acquisitionOrder));
            Assert.That(second.acquisitionOrder, Is.LessThan(third.acquisitionOrder));
            Assert.That(inventory.ArtifactInstances[0].instanceId, Is.EqualTo(first.instanceId));
            Assert.That(inventory.ArtifactInstances[2].instanceId, Is.EqualTo(third.instanceId));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void SettingFavouriteDoesNotTriggerDestructiveArtifactsChangedEvent()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            Assert.That(inventory.TryAcquireArtifact(definition, out ArtifactInstance instance, 200), Is.True);

            int fullResetCount = 0;
            string favInstanceId = null;
            bool favState = false;

            inventory.OnArtifactsChanged += () => fullResetCount++;
            inventory.OnArtifactFavouriteChanged += (id, state) =>
            {
                favInstanceId = id;
                favState = state;
            };

            bool success = inventory.TrySetArtifactFavourite(instance.instanceId, true);

            Assert.That(success, Is.True);
            Assert.That(instance.isFavourite, Is.True);
            Assert.That(favInstanceId, Is.EqualTo(instance.instanceId));
            Assert.That(favState, Is.True);
            Assert.That(fullResetCount, Is.Zero, "Toggling favorite should not trigger full OnArtifactsChanged grid reset.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void MarkingArtifactSeenDoesNotTriggerFullArtifactsChangedEvent()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            Assert.That(inventory.TryAcquireArtifact(definition, out ArtifactInstance instance, 200), Is.True);
            Assert.That(instance.isNew, Is.True);

            int fullResetCount = 0;
            string seenInstanceId = null;

            inventory.OnArtifactsChanged += () => fullResetCount++;
            inventory.OnArtifactSeenChanged += id => seenInstanceId = id;

            bool success = inventory.TryMarkArtifactSeen(instance.instanceId);

            Assert.That(success, Is.True);
            Assert.That(instance.isNew, Is.False);
            Assert.That(seenInstanceId, Is.EqualTo(instance.instanceId));
            Assert.That(fullResetCount, Is.Zero, "Marking artifact seen should not trigger full OnArtifactsChanged grid reset.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void QuerySortsNewestAndOldestByCollectedTimestampUtc()
    {
        long baseTicks = DateTime.UtcNow.Ticks;
        ArtifactInstance early = new ArtifactInstance { instanceId = "early", definitionId = definition.id, collectedTimestampUtc = baseTicks - 10000 };
        ArtifactInstance mid = new ArtifactInstance { instanceId = "mid", definitionId = definition.id, collectedTimestampUtc = baseTicks };
        ArtifactInstance late = new ArtifactInstance { instanceId = "late", definitionId = definition.id, collectedTimestampUtc = baseTicks + 10000 };

        List<ArtifactInstance> results = new List<ArtifactInstance>();

        ArtifactInventoryQuery queryNewest = ArtifactInventoryQuery.Default;
        queryNewest.sortMode = ArtifactSortMode.Newest;
        queryNewest.descending = true;
        ArtifactInventoryQueryEngine.Execute(new[] { mid, early, late }, queryNewest, table, _ => definition, new HashSet<string>(), results);
        Assert.That(results[0].instanceId, Is.EqualTo("late"));
        Assert.That(results[1].instanceId, Is.EqualTo("mid"));
        Assert.That(results[2].instanceId, Is.EqualTo("early"));

        ArtifactInventoryQuery queryOldest = ArtifactInventoryQuery.Default;
        queryOldest.sortMode = ArtifactSortMode.Oldest;
        queryOldest.descending = true;
        ArtifactInventoryQueryEngine.Execute(new[] { mid, early, late }, queryOldest, table, _ => definition, new HashSet<string>(), results);
        Assert.That(results[0].instanceId, Is.EqualTo("early"));
        Assert.That(results[1].instanceId, Is.EqualTo("mid"));
        Assert.That(results[2].instanceId, Is.EqualTo("late"));
    }
}
#endif
