#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.Progression;
using UnityEngine;
using UnityEditor;
using UnityEngine.EventSystems;
using Project.TeamBuilder.UI;
using Project.UI;

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

    [Test]
    public void AuthoredRarityProfilesLimitEarlyPowerWithoutChangingStatUnits()
    {
        ArtifactRollTable authored = Resources.Load<ArtifactRollTable>("ItemSO/Artifacts/ArtifactRollTable");
        var generator = new ArtifactRollGenerator(authored);
        definition.rarity = ArtifactRarity.R;
        definition.fixedStatBonus = new ArtifactRoll(ArtifactStatKind.AttackPercent, 500, ArtifactRollSource.Generated);
        ArtifactInstance first = generator.Generate(definition, 0, 10, 2, "first");
        ArtifactInstance repeat = generator.Generate(definition, 0, 10, 0, "repeat");
        Assert.That(first.rolls.Count, Is.EqualTo(1));
        Assert.That(first.rolls[0].statKind, Is.EqualTo(repeat.rolls[0].statKind));
        Assert.That(first.rolls[0].valueUnits, Is.EqualTo(repeat.rolls[0].valueUnits));
        ArtifactInstance fallback = new ArtifactRollGenerator(table).Generate(definition, 0, 10, 2, "fallback");
        Assert.That(fallback.rolls, Is.EqualTo(first.rolls), "Inventory fallback must respect the same rarity profile.");
        Assert.That(ArtifactRollQuality.TryGetAverageQuality(first, authored, out float quality), Is.True);
        Assert.That(quality, Is.LessThanOrEqualTo(.61f));

        first.rolls.Clear();
        first.rolls.Add(new ArtifactRoll(ArtifactStatKind.AttackPercent, 700, ArtifactRollSource.Generated));
        CharacterStats cleanBase = new CharacterStats { attack = 100 };
        CharacterStats result = cleanBase.Clone();
        ArtifactStatAggregator.Apply(result, cleanBase, new[] { first }, _ => definition);
        Assert.That(result.attack, Is.EqualTo(112f).Within(.001f));
    }

    [Test]
    public void PreparedArtifactCommitChargesOnceAndPreservesTheSelectedRolls()
    {
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            var generator = new ArtifactRollGenerator(table);
            ArtifactInstance prepared = generator.Generate(definition, 0, 15, "selected-copy");
            inventory.currency = 100;
            Assert.That(inventory.TryCommitArtifact(prepared, 40, out ArtifactInstance acquired), Is.True);
            Assert.That(inventory.currency, Is.EqualTo(60));
            Assert.That(acquired.instanceId, Is.EqualTo(prepared.instanceId));
            Assert.That(acquired.rolls, Is.EqualTo(prepared.rolls));
            Assert.That(inventory.TryCommitArtifact(prepared, 40, out _), Is.False);
            Assert.That(inventory.currency, Is.EqualTo(60));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void ClearingStateRejectsACompletionFromAnEarlierVisit()
    {
        var clearing = ScriptableObject.CreateInstance<ZoneStateProvider>();
        try
        {
            clearing.BeginVisit();
            int firstVisit = clearing.Visit;
            Assert.That(clearing.SetClearing(firstVisit, ClearingState.RewardPending), Is.True);
            Assert.That(clearing.AccessMode, Is.EqualTo(ZoneMode.CombatZone));
            clearing.BeginVisit();
            Assert.That(clearing.SetClearing(firstVisit, ClearingState.Accessible), Is.False);
            Assert.That(clearing.SetClearing(clearing.Visit, ClearingState.Accessible), Is.True);
            Assert.That(clearing.AccessMode, Is.EqualTo(ZoneMode.SafeZone));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clearing);
        }
    }

    [Test]
    public void RewardPrefabsAndAuthoredRarityCatalogResolve()
    {
        var counts = new Dictionary<ArtifactRarity, int>();
        var ids = new HashSet<string>();
        foreach (ArtifactDefinition item in Resources.LoadAll<ArtifactDefinition>("ItemSO/Artifacts"))
        {
            if (item.rarity == ArtifactRarity.Unassigned) continue;
            Assert.That(ids.Add(item.id), Is.True, $"Duplicate Artifact ID {item.id}");
            counts.TryGetValue(item.rarity, out int count);
            counts[item.rarity] = count + 1;
            if (item.rarity != ArtifactRarity.R)
            {
                Assert.That(item.passiveSet, Is.Not.Null, item.id);
                Assert.That(item.passiveSet.rules, Is.Not.Empty, item.id);
                foreach (ArtifactPassiveRuleDefinition rule in item.passiveSet.rules)
                {
                    Assert.That(rule.responses, Is.Not.Empty, item.id);
                    foreach (ArtifactPassiveResponseDefinition response in rule.responses)
                        Assert.That(response, Is.Not.Null, item.id);
                }
            }
        }
        Assert.That(counts[ArtifactRarity.R], Is.EqualTo(10));
        Assert.That(counts[ArtifactRarity.SR], Is.EqualTo(8));
        Assert.That(counts[ArtifactRarity.SSR], Is.EqualTo(9));
        var picker = Resources.Load<ArtifactRewardPickerUI>(ArtifactRewardPickerUI.ResourcePath);
        Assert.That(picker, Is.Not.Null);
        Assert.That(picker.IsConfigured, Is.True);
        Assert.That(picker.transform.Find("Card3/RarityStar3"), Is.Not.Null);
        Assert.That(Resources.Load<ArtifactCopyMenuUI>(ArtifactCopyMenuUI.MenuPath), Is.Not.Null);
        Assert.That(Resources.Load<ArtifactCopyMenuUI>(ArtifactCopyMenuUI.DialogPath), Is.Not.Null);
        var tile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/UI/TeamArtifactInventoryCard.prefab");
        Assert.That(tile.transform.Find("FavouriteBadge"), Is.Not.Null);
        Assert.That(tile.transform.Find("NewBadge"), Is.Not.Null);
    }

    [Test]
    public void HardOffersPreviewTheSameTotalAndPullCountThatTheRoomReceives()
    {
        var room = Resources.Load<RoomData>("MapSO/RoomData_Combat_01");
        Assert.That(room, Is.Not.Null);
        UnityEngine.Random.State originalRandom = UnityEngine.Random.state;
        var pullCounts = new HashSet<int>();
        try
        {
            UnityEngine.Random.InitState(15273);
            for (int i = 0; i < 128 && pullCounts.Count < 3; i++)
            {
                RoomChoiceOffer offer = RouteOfferGenerator.BuildOffer(room);
                if (offer.Difficulty != RouteDifficulty.Hard) continue;
                Assert.That(offer.Reward.IncludesBasePayout, Is.True);
                pullCounts.Add(offer.Reward.ArtifactPulls);
                switch (offer.Reward.ArtifactPulls)
                {
                    case 0: Assert.That(offer.Reward.Coins, Is.InRange(300, 400)); break;
                    case 1: Assert.That(offer.Reward.Coins, Is.InRange(180, 280)); break;
                    case 2: Assert.That(offer.Reward.Coins, Is.Zero); break;
                    default: Assert.Fail("Unexpected Hard reward pattern."); break;
                }
            }
        }
        finally
        {
            UnityEngine.Random.state = originalRandom;
        }
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, pullCounts);
    }

    [Test]
    public void RewardPickerConfirmsTheDisplayedCopyOnceAndKeepsTheOtherChoicesUnowned()
    {
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        var picker = UnityEngine.Object.Instantiate(Resources.Load<ArtifactRewardPickerUI>(ArtifactRewardPickerUI.ResourcePath));
        try
        {
            ArtifactDefinition common = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/RarityR/R_RustedOathRing");
            var generator = new ArtifactRollGenerator(inventory.ArtifactRollTable);
            var choices = new ArtifactInstance[3];
            for (int i = 0; i < choices.Length; i++)
                choices[i] = generator.Generate(common, 0, 100 + i, 0, $"candidate-{i}");

            int calls = 0;
            IEnumerator presentation = picker.Present(choices, inventory, 1, 2, choice =>
            {
                calls++;
                return inventory.TryCommitArtifact(choices[choice], 0, out _);
            });
            Assert.That(presentation.MoveNext(), Is.True);
            Assert.That(picker.transform.Find("Card2/Title").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(common.itemName));
            picker.GetComponent<CanvasGroup>().interactable = true;
            picker.transform.Find("Card2").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            var confirm = picker.transform.Find("Confirm").GetComponent<UnityEngine.UI.Button>();
            confirm.onClick.Invoke();
            confirm.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(inventory.TryGetArtifactInstance("candidate-1", out ArtifactInstance claimed), Is.True);
            Assert.That(claimed.rolls, Is.EqualTo(choices[1].rolls));
            Assert.That(inventory.TryGetArtifactInstance("candidate-0", out _), Is.False);
            Assert.That(inventory.TryGetArtifactInstance("candidate-2", out _), Is.False);
            Assert.That(picker.transform.Find("Card2/RarityStar2").gameObject.activeSelf, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(picker.gameObject);
            UnityEngine.Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void DiscardDeletesOnlyTheTargetedCopyAndRejectsAStaleTarget()
    {
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        var host = new GameObject("DiscardValidation");
        try
        {
            inventory.TryAcquireArtifact(definition, out ArtifactInstance first, 1);
            inventory.TryAcquireArtifact(definition, out ArtifactInstance second, 2);
            host.SetActive(false);
            var panel = host.AddComponent<ArtifactGridPanel>();
            typeof(ArtifactGridPanel).GetField("playerInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(panel, inventory);
            host.SetActive(true);
            MethodInfo discard = typeof(ArtifactGridPanel).GetMethod("Discard", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That((bool)discard.Invoke(panel, new object[] { first.instanceId }), Is.True);
            Assert.That(inventory.TryGetArtifactInstance(first.instanceId, out _), Is.False);
            Assert.That(inventory.TryGetArtifactInstance(second.instanceId, out _), Is.True);
            Assert.That((bool)discard.Invoke(panel, new object[] { first.instanceId }), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(inventory);
        }
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
        string[] guids = AssetDatabase.FindAssets("t:ArtifactDefinition",
            new[] { "Assets/Project/Resources/ItemSO/Artifacts/Phase1" });
        Assert.That(guids.Length, Is.EqualTo(9));

        for (int i = 0; i < guids.Length; i++)
        {
            ArtifactDefinition artifact = AssetDatabase.LoadAssetAtPath<ArtifactDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
            Assert.That(artifact, Is.Not.Null);
            CharacterStats cleanBase = new CharacterStats { maxHP = 1000f, attack = 100f, defense = 100f };
            CharacterStats expected = cleanBase.Clone();
            expected.ApplyArtifactBonuses(cleanBase, artifact.LegacyBonusStats);
            // SPD is now system-only, so the shared buildable-stat helper intentionally omits it.
            // Legacy Artifact a4 authored a hidden -1 SPD modifier and the importer must preserve it exactly.
            expected.speed += artifact.LegacyBonusStats != null ? artifact.LegacyBonusStats.speed : 0f;
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
            defense = 50f,
            critRate = 0f,
            critDamage = 0f
        };
        CharacterStats destination = cleanBase.Clone();

        ArtifactInstance instance = new ArtifactInstance
        {
            instanceId = "test-all-stats",
            definitionId = definition.id,
            rolls = new List<ArtifactRoll>
            {
                new ArtifactRoll(ArtifactStatKind.FlatMaxHP, 250, ArtifactRollSource.Generated),       // +250 HP
                new ArtifactRoll(ArtifactStatKind.MaxHPPercent, 1000, ArtifactRollSource.Generated),   // +10% Max HP (100)
                new ArtifactRoll(ArtifactStatKind.FlatAttack, 50, ArtifactRollSource.Generated),        // +50 ATK
                new ArtifactRoll(ArtifactStatKind.AttackPercent, 2000, ArtifactRollSource.Generated),  // +20% ATK (20)
                new ArtifactRoll(ArtifactStatKind.FlatDefense, 25, ArtifactRollSource.Generated),      // +25 DEF
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

    [Test]
    public void BiomeRollQualityUpgradesAcrossBiomes()
    {
        ArtifactRollGenerator generator = new ArtifactRollGenerator(table);
        const int samples = 500;

        float totalQualityBiome0 = 0f;
        int countBiome0 = 0;
        for (int i = 0; i < samples; i++)
        {
            ArtifactInstance instance = generator.Generate(definition, i, i, 0);
            if (ArtifactRollQuality.TryGetAverageQuality(instance, table, out float q))
            {
                totalQualityBiome0 += q;
                countBiome0++;
            }
        }

        float totalQualityBiome1 = 0f;
        int countBiome1 = 0;
        for (int i = 0; i < samples; i++)
        {
            ArtifactInstance instance = generator.Generate(definition, i, i + 5000, 1);
            if (ArtifactRollQuality.TryGetAverageQuality(instance, table, out float q))
            {
                totalQualityBiome1 += q;
                countBiome1++;
            }
        }

        float totalQualityBiome2 = 0f;
        int countBiome2 = 0;
        for (int i = 0; i < samples; i++)
        {
            ArtifactInstance instance = generator.Generate(definition, i, i + 10000, 2);
            if (ArtifactRollQuality.TryGetAverageQuality(instance, table, out float q))
            {
                totalQualityBiome2 += q;
                countBiome2++;
            }
        }

        float avg0 = totalQualityBiome0 / countBiome0;
        float avg1 = totalQualityBiome1 / countBiome1;
        float avg2 = totalQualityBiome2 / countBiome2;

        Assert.That(avg1, Is.GreaterThan(avg0), $"Biome 1 quality ({avg1:F3}) must be higher than Biome 0 ({avg0:F3})");
        Assert.That(avg2, Is.GreaterThan(avg1), $"Biome 2 (Biome 3 Phase) quality ({avg2:F3}) must be higher than Biome 1 ({avg1:F3})");
    }

    [Test]
    public void BiomeBandWeightsMatchAuthoredProfiles()
    {
        IReadOnlyList<ArtifactAmountBand> b0 = table.GetAmountBandsForBiome(0);
        IReadOnlyList<ArtifactAmountBand> b1 = table.GetAmountBandsForBiome(1);
        IReadOnlyList<ArtifactAmountBand> b2 = table.GetAmountBandsForBiome(2);

        Assert.That(b0.Count, Is.EqualTo(5));
        Assert.That(b1.Count, Is.EqualTo(5));
        Assert.That(b2.Count, Is.EqualTo(5));

        // Biome 1: Band 0 (min) is 35, Band 4 (max) is 5
        Assert.That(b0[0].weight, Is.EqualTo(35f));
        Assert.That(b0[4].weight, Is.EqualTo(5f));

        // Biome 2: Band 0 is 15, Band 2 is 35, Band 4 is 5
        Assert.That(b1[0].weight, Is.EqualTo(15f));
        Assert.That(b1[2].weight, Is.EqualTo(35f));

        // Biome 3: Band 0 is 5, Band 3 is 35, Band 4 (max) is 25
        Assert.That(b2[0].weight, Is.EqualTo(5f));
        Assert.That(b2[3].weight, Is.EqualTo(35f));
        Assert.That(b2[4].weight, Is.EqualTo(25f));
    }

    [Test]
    public void NegativeBiomeClampsToBiomeZero()
    {
        IReadOnlyList<ArtifactAmountBand> negativeBands = table.GetAmountBandsForBiome(-5);
        IReadOnlyList<ArtifactAmountBand> zeroBands = table.GetAmountBandsForBiome(0);
        Assert.That(negativeBands[0].weight, Is.EqualTo(zeroBands[0].weight));
        Assert.That(negativeBands[4].weight, Is.EqualTo(zeroBands[4].weight));
    }

    [Test]
    public void OutOfRangeBiomeClampsToHighestAuthoredProfile()
    {
        IReadOnlyList<ArtifactAmountBand> highBands = table.GetAmountBandsForBiome(99);
        IReadOnlyList<ArtifactAmountBand> b2 = table.GetAmountBandsForBiome(2);
        Assert.That(highBands[4].weight, Is.EqualTo(b2[4].weight));
    }

    [Test]
    public void ArtifactInstanceTracksAcquisitionBiome()
    {
        ArtifactRollGenerator generator = new ArtifactRollGenerator(table);
        ArtifactInstance inst0 = generator.Generate(definition, 1, 100, 0);
        ArtifactInstance inst2 = generator.Generate(definition, 2, 200, 2);

        Assert.That(inst0.acquisitionBiomeIndex, Is.EqualTo(0));
        Assert.That(inst2.acquisitionBiomeIndex, Is.EqualTo(2));
    }

    [Test]
    public void InventoryAcquisitionPreservesBiomeOverride()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            Assert.That(inventory.TryAcquireArtifact(definition, out ArtifactInstance inst, 100, 2), Is.True);
            Assert.That(inst.acquisitionBiomeIndex, Is.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventory);
        }
    }
}
#endif
