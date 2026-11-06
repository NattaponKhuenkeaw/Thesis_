#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.Core.RunEnd;
using Project.InventorySystem;
using Project.InventorySystem.Items;
using Project.InventorySystem.Artifacts;
using Project.ShopSystem;
using Project.ShopSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ShopEconomyBalanceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private PlayerRunInventory inventory;
    private ShopManager shop;
    private ShopItemListUI offer;
    private Random.State previousRandomState;

    private static IEnumerable<TestCaseData> ArtifactPrices()
    {
        yield return new TestCaseData("P1_01_BlindsBoundlessRibbon", 3000);
        yield return new TestCaseData("P1_02_FiendFleshMask", 1800);
        yield return new TestCaseData("P1_03_SoulMerchantsScales", 2200);
        yield return new TestCaseData("P1_04_BrokenChronoRelic", 4250);
        yield return new TestCaseData("P1_05_AuraResonatingShard", 2250);
        yield return new TestCaseData("P1_06_RivalsBrokenCrest", 2350);
        yield return new TestCaseData("P1_07_ShroudOfDeprivation", 1900);
        yield return new TestCaseData("P1_08_MemoryDrainingUrn", 2900);
        yield return new TestCaseData("P1_09_ShinsRustyDagger", 4100);
    }

    [SetUp]
    public void SetUp()
    {
        previousRandomState = Random.state;
        host = new GameObject(nameof(ShopEconomyBalanceTests));
        host.SetActive(false);
        inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        shop = host.AddComponent<ShopManager>();
        shop.playerInventory = inventory;
        shop.codexInventory = Resources.Load<CodexInventory>("InventorySO/CodexInventory");
        offer = host.AddComponent<ShopItemListUI>();
        var settings = new SerializedObject(offer);
        settings.FindProperty("itemType").enumValueIndex = (int)ShopItemListType.Artifact;
        settings.ApplyModifiedPropertiesWithoutUndo();
        shop.RegisterItemList(offer);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(inventory);
        Random.state = previousRandomState;
    }

    [TestCaseSource(nameof(ArtifactPrices))]
    public void AuthoredArtifactPriceSupportsRejectionPurchaseOnceAndHalfPriceResale(string resource, int price)
    {
        ArtifactDefinition artifact = LoadArtifact(resource);
        Assert.That(artifact.price, Is.EqualTo(price));
        Assert.That(shop.codexInventory.allItems, Does.Contain(artifact));
        Assert.That(OfferPrice(artifact), Is.EqualTo(price));
        inventory.currency = price - 1;
        offer.Populate(artifact, 1, OfferPrice(artifact));
        Assert.That(shop.CanPurchaseOffer(artifact, 1, offer.CurrentPrice), Is.False);
        Assert.That(shop.TryPurchaseOffer(offer), Is.False);
        Assert.That(inventory.currency, Is.EqualTo(price - 1));
        Assert.That(inventory.GetItemCount(artifact), Is.Zero);
        Assert.That(offer.IsSold, Is.False);

        inventory.AddCurrency(1);
        Assert.That(shop.TryPurchaseOffer(offer), Is.True);
        Assert.That(inventory.currency, Is.Zero);
        Assert.That(inventory.GetItemCount(artifact), Is.EqualTo(1));
        Assert.That(offer.IsSold, Is.True);
        Assert.That(shop.TryPurchaseOffer(offer), Is.False);
        Assert.That(inventory.currency, Is.Zero);

        ShopSellEntry sale = shop.GetSellableEntries().Single(entry => entry.Item == artifact);
        Assert.That(sale.Price, Is.EqualTo(price / 2));
        Assert.That(shop.TrySell(sale), Is.True);
        Assert.That(inventory.currency, Is.EqualTo(price / 2));
        Assert.That(inventory.GetItemCount(artifact), Is.Zero);
    }

    [Test]
    public void CharactersStillRequireEarnedSoulBeyondTheStartingBudget()
    {
        Assert.That(inventory.currency, Is.EqualTo(1000));
        foreach (string resource in new[] { "char_0_Main", "char_1_CameraGIrl" })
        {
            CharacterItem character = Resources.Load<CharacterItem>("ItemSO/" + resource);
            Assert.That(character.price, Is.EqualTo(1100));
            Assert.That(character.availableInShop, Is.True);
            Assert.That(inventory.CanAfford(OfferPrice(character)), Is.False);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TwoMinimumMediumRewardsRequireChoosingCameraGirlOrArtifacts(bool buyCharacter)
    {
        var tables = Resources.Load<RouteOfferTables>("MapSO/RouteOfferTables");
        var profile = tables.GetProfile(RouteDifficulty.Medium);
        var character = Resources.Load<CharacterItem>("ItemSO/char_1_CameraGIrl");
        var artifact = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/RarityR/R_CrackedWardPlate");
        Assert.That(inventory.currency, Is.EqualTo(1000));
        var room = Resources.Load<RoomData>("MapSO/LV/RoomData_LV_01");
        WithClearManagers(room, (runEnd, waves) => {
            for (int i = 0; i < 2; i++)
            {
                ActiveRouteOffer.Set(new RoomChoiceOffer(room, RouteDifficulty.Medium, null,
                    new RouteReward { Coins = profile.minCoins }));
                CallWave(waves, "NotifyRunEndRoomCleared", false);
                CallWave(waves, "TryGrantActiveRouteReward", room);
            }
        });
        Assert.That(inventory.currency, Is.EqualTo(1240));
        if (buyCharacter)
        {
            Assert.That(shop.TryPurchase(character, 1, OfferPrice(character)), Is.True);
            Assert.That(shop.TryPurchase(artifact, 1, OfferPrice(artifact)), Is.False);
            Assert.That(inventory.HasItem(character), Is.True);
            Assert.That(inventory.currency, Is.EqualTo(140));
        }
        else
        {
            Assert.That(shop.TryPurchase(artifact, 1, OfferPrice(artifact)), Is.True);
            Assert.That(shop.TryPurchase(Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/RarityR/R_EchoBell")), Is.True);
            Assert.That(shop.TryPurchase(character, 1, OfferPrice(character)), Is.False);
            Assert.That(inventory.currency, Is.EqualTo(640));
        }
    }

    [Test]
    public void ArtifactCatalogUsesIncomingRarityPricesAndKeepsEasyMediumRewards()
    {
        var tables = Resources.Load<RouteOfferTables>("MapSO/RouteOfferTables");
        var easy = tables.GetProfile(RouteDifficulty.Easy);
        var hard = tables.GetProfile(RouteDifficulty.Hard);
        var artifacts = shop.codexInventory.allItems.OfType<ArtifactDefinition>().ToArray();
        Assert.That(artifacts.Count(item => item.rarity == ArtifactRarity.R), Is.EqualTo(10));
        Assert.That(artifacts.Count(item => item.rarity == ArtifactRarity.SR), Is.EqualTo(8));
        Assert.That(artifacts.Count(item => item.rarity == ArtifactRarity.SSR), Is.EqualTo(9));
        Assert.That(artifacts.Where(item => item.rarity == ArtifactRarity.R).All(item => item.price >= 275 && item.price <= 450), Is.True);
        Assert.That(artifacts.Where(item => item.rarity == ArtifactRarity.SR).All(item => item.price >= 800 && item.price <= 1200), Is.True);
        Assert.That(artifacts.Where(item => item.rarity == ArtifactRarity.SSR).Min(item => item.price), Is.EqualTo(1800));
        Assert.That(inventory.currency, Is.EqualTo(1000));
        Assert.That(easy.minCoins, Is.EqualTo(80));
        Assert.That(easy.maxCoins, Is.EqualTo(100));
        Assert.That(hard.minCoins, Is.EqualTo(180));
        Assert.That(hard.maxCoins, Is.EqualTo(220));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void FreeRerollsRaiseOfferPricesByFifteenPercentOfBaseEachTime(int rerolls)
    {
        for (int i = 0; i < rerolls; i++) Assert.That(shop.TryRerollOffers(), Is.True);
        Assert.That(shop.CurrentRerollCost, Is.Zero);
        Assert.That(shop.CurrentRerollPriceIncreasePercent, Is.EqualTo(15f * rerolls));
        Assert.That(shop.CurrentRerollPriceMultiplier, Is.EqualTo(1f + .15f * rerolls).Within(.0001f));
        foreach (BaseItem item in shop.codexInventory.allItems.Where(item =>
            item is ArtifactDefinition || item is CharacterItem character && character.availableInShop))
        {
            Assert.That(OfferPrice(item), Is.EqualTo(Mathf.CeilToInt(item.price * shop.CurrentRerollPriceMultiplier)), item.name);
        }
    }

    [Test]
    public void RepeatedRerollsAreFreeEvenWithZeroSoulAndResetSoldOffers()
    {
        var changes = new List<int>();
        shop.OnRerollCostChanged += changes.Add;
        var multipliers = new List<float>();
        shop.OnRerollPriceMultiplierChanged += multipliers.Add;
        int currencyChanges = 0;
        inventory.OnCurrencyChanged += _ => currencyChanges++;
        inventory.currency = 0;
        for (int i = 0; i < 20; i++)
        {
            int before = inventory.currency;
            Assert.That(shop.CurrentRerollCost, Is.Zero);
            offer.MarkSold();
            Assert.That(shop.TryRerollOffers(), Is.True);
            Assert.That(inventory.currency, Is.EqualTo(before));
            Assert.That(offer.IsSold, Is.False);
            Assert.That(offer.CurrentItem, Is.Not.Null);
            Assert.That(shop.CurrentRerollPriceIncreasePercent, Is.EqualTo(15f * (i + 1)));
            Assert.That(offer.CurrentPrice, Is.EqualTo(Mathf.CeilToInt(offer.CurrentItem.price * shop.CurrentRerollPriceMultiplier)));
        }
        Assert.That(inventory.currency, Is.Zero);
        Assert.That(currencyChanges, Is.Zero);
        Assert.That(changes.Count, Is.EqualTo(20));
        Assert.That(changes.All(cost => cost == 0), Is.True);
        Assert.That(multipliers.Count, Is.EqualTo(20));
        Assert.That(multipliers.Last(), Is.EqualTo(4f));
        shop.ResetShopState();
        Assert.That(shop.CurrentRerollCost, Is.Zero);
        Assert.That(inventory.currency, Is.Zero);
        Assert.That(currencyChanges, Is.Zero);
        Assert.That(shop.CurrentRerollPriceMultiplier, Is.EqualTo(1f));
        Assert.That(offer.CurrentPrice, Is.EqualTo(offer.CurrentItem.price));
        Assert.That(multipliers.Last(), Is.EqualTo(1f));
    }

    [Test]
    public void MissingInventoryRerollPreservesOffersSoldStateAndRandomState()
    {
        var artifact = LoadArtifact("P1_07_ShroudOfDeprivation");
        offer.Populate(artifact, 1, artifact.price); offer.MarkSold();
        shop.currentShopItems.Add(artifact);
        inventory.currency = 0;
        shop.playerInventory = null;
        var state = Random.state;
        int changes = 0;
        shop.OnShopOffersChanged += () => changes++;
        shop.OnRerollCostChanged += _ => changes++;
        Assert.That(shop.TryRerollOffers(), Is.False);
        Assert.That(inventory.currency, Is.Zero);
        Assert.That(shop.CurrentRerollCost, Is.Zero);
        Assert.That(offer.CurrentItem, Is.SameAs(artifact));
        Assert.That(shop.CurrentRerollPriceMultiplier, Is.EqualTo(1f));
        Assert.That(offer.IsSold, Is.True);
        Assert.That(shop.currentShopItems, Is.EqualTo(new[] { artifact }));
        Assert.That(Random.state, Is.EqualTo(state));
        Assert.That(changes, Is.Zero);
        shop.playerInventory = inventory;
        Assert.That(shop.TryRerollOffers(), Is.True);
        Assert.That(inventory.currency, Is.Zero);
        Assert.That(shop.CurrentRerollCost, Is.Zero);
    }

    [Test]
    public void RerollDoesNotChargeWhenNothingCanBeRolled()
    {
        var empty = ScriptableObject.CreateInstance<CodexInventory>();
        try
        {
            shop.codexInventory = empty;
            Assert.That(shop.TryRerollOffers(), Is.False);
            Assert.That(inventory.currency, Is.EqualTo(1000));
            Assert.That(shop.CurrentRerollCost, Is.Zero);
            empty.allItems.Add(Resources.Load<CharacterItem>("ItemSO/char_1_CameraGIrl"));
            Assert.That(shop.TryRerollOffers(), Is.False, "Character-only inventory cannot populate an Artifact offer.");
            Assert.That(inventory.currency, Is.EqualTo(1000));
            Assert.That(shop.CurrentRerollCost, Is.Zero);
        }
        finally { Object.DestroyImmediate(empty); }
    }

    [Test]
    public void RerollPromptShowsFreeEvenWithZeroSoul()
    {
        host.SetActive(true);
        var promptHost = new GameObject("Reroll prompt check");
        try
        {
            var ui = promptHost.AddComponent<InteractableWorldUI>();
            var type = typeof(InteractableWorldUI).GetNestedType("PromptConfig", BindingFlags.NonPublic);
            var prompt = System.Activator.CreateInstance(type, true);
            type.GetField("interactionType").SetValue(prompt, InteractableWorldUI.InteractionType.ShopReroll);
            type.GetField("descriptionString").SetValue(prompt, "Reroll");
            var method = typeof(InteractableWorldUI).GetMethod("GetDisplayDescription", Private);
            Assert.That(method.Invoke(ui, new[] { prompt }), Is.EqualTo("Reroll FREE (+0%)"));
            Assert.That(shop.TryRerollOffers(), Is.True);
            Assert.That(method.Invoke(ui, new[] { prompt }), Is.EqualTo("Reroll FREE (+15%)"));
            inventory.currency = 0;
            Assert.That(shop.TryRerollOffers(), Is.True);
            Assert.That(method.Invoke(ui, new[] { prompt }), Is.EqualTo("Reroll FREE (+30%)"));
        }
        finally { Object.DestroyImmediate(promptHost); }
    }

    [Test]
    public void AuthoredShopRerollsForFreeAndAppliesTheOriginalFifteenPercentMarkup()
    {
        var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath("d36c77798e07625409da16f442bc42f5"));
        try
        {
            var liveShop = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ShopManager>(true)).Single();
            liveShop.playerInventory = inventory;
            using var data = new SerializedObject(liveShop);
            Assert.That(data.FindProperty("rerollBaseCost"), Is.Null);
            Assert.That(data.FindProperty("rerollCostStep"), Is.Null);
            Assert.That(data.FindProperty("rerollPriceIncreasePercent").floatValue, Is.EqualTo(15f));
            liveShop.ResetShopState();
            Assert.That(liveShop.TryRerollOffers(), Is.True);
            Assert.That(inventory.currency, Is.EqualTo(1000));
            Assert.That(liveShop.CurrentRerollCost, Is.Zero);
            foreach (var list in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ShopItemListUI>(true)))
            {
                if (list.CurrentItem == null) continue;
                int expected = list.CurrentItem is Item ? list.CurrentItem.price * list.CurrentQuantity : list.CurrentItem.price;
                Assert.That(list.CurrentPrice, Is.EqualTo(Mathf.CeilToInt(expected * liveShop.CurrentRerollPriceMultiplier)));
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void RerolledArtifactPurchaseChargesTheDisplayedMarkupOnceWithoutChangingTheAsset()
    {
        Assert.That(shop.TryRerollOffers(), Is.True);
        var item = (ArtifactDefinition)offer.CurrentItem;
        int assetPrice = item.price;
        int quoted = offer.CurrentPrice;
        Assert.That(quoted, Is.EqualTo(Mathf.CeilToInt(assetPrice * 1.15f)));
        inventory.currency = quoted - 1;
        Assert.That(shop.TryPurchaseOffer(offer), Is.False);
        Assert.That(inventory.currency, Is.EqualTo(quoted - 1));
        Assert.That(offer.IsSold, Is.False);
        inventory.AddCurrency(1);
        Assert.That(shop.TryPurchaseOffer(offer), Is.True);
        Assert.That(inventory.currency, Is.Zero);
        Assert.That(inventory.GetItemCount(item), Is.EqualTo(1));
        Assert.That(shop.TryPurchaseOffer(offer), Is.False);
        Assert.That(inventory.currency, Is.Zero);
        Assert.That(item.price, Is.EqualTo(assetPrice));
    }

    [TestCase(RouteDifficulty.Easy, 80, 100)]
    [TestCase(RouteDifficulty.Medium, 120, 150)]
    [TestCase(RouteDifficulty.Hard, 180, 220)]
    public void GeneratedCardsUseTheApprovedSoulRange(RouteDifficulty difficulty, int minimum, int maximum)
    {
        var tables = Object.Instantiate(Resources.Load<RouteOfferTables>("MapSO/RouteOfferTables"));
        try
        {
            using (var data = new SerializedObject(tables))
            {
                var profiles = data.FindProperty("profiles");
                for (int i = 0; i < profiles.arraySize; i++)
                {
                    var profile = profiles.GetArrayElementAtIndex(i);
                    profile.FindPropertyRelative("rollWeight").floatValue =
                        profile.FindPropertyRelative("difficulty").enumValueIndex == (int)difficulty ? 1f : 0f;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.That(tables.GetProfile(difficulty).minCoins, Is.EqualTo(minimum));
            Assert.That(tables.GetProfile(difficulty).maxCoins, Is.EqualTo(maximum));
            var room = Resources.Load<RoomData>("MapSO/LV/RoomData_LV_01");
            Assert.That(room, Is.Not.Null);
            Random.InitState(175 + (int)difficulty);
            for (int i = 0; i < 128; i++)
            {
                RoomChoiceOffer card = RouteOfferGenerator.BuildOffer(room, tables);
                Assert.That(card.Difficulty, Is.EqualTo(difficulty));
                if (difficulty == RouteDifficulty.Hard)
                {
                    Assert.That(card.Reward.IncludesBasePayout, Is.True);
                    Assert.That(tables.HardRewardPatterns.Any(pattern =>
                        pattern.artifactPulls == card.Reward.ArtifactPulls &&
                        card.Reward.Coins >= pattern.minSoul && card.Reward.Coins <= pattern.maxSoul), Is.True);
                }
                else
                {
                    Assert.That(card.Reward.IncludesBasePayout, Is.False);
                    Assert.That(card.Reward.Coins, Is.InRange(minimum, maximum));
                }
            }
        }
        finally { Object.DestroyImmediate(tables); }
    }

    [TestCase(RouteDifficulty.Easy, 80)]
    [TestCase(RouteDifficulty.Easy, 100)]
    [TestCase(RouteDifficulty.Medium, 120)]
    [TestCase(RouteDifficulty.Medium, 150)]
    [TestCase(RouteDifficulty.Hard, 180)]
    [TestCase(RouteDifficulty.Hard, 220)]
    public void CombatPaysTheSelectedCardExactlyOnceWithoutTheFixedBonus(RouteDifficulty difficulty, int coins)
    {
        var room = Resources.Load<RoomData>("MapSO/LV/RoomData_LV_01");
        WithClearManagers(room, (runEnd, waves) => {
            ActiveRouteOffer.Set(new RoomChoiceOffer(room, difficulty, null, new RouteReward { Coins = coins }));
            int before = inventory.currency;
            CallWave(waves, "NotifyRunEndRoomCleared", false);
            Assert.That(inventory.currency, Is.EqualTo(before), "Combat must not pay the fixed 500 Soul bonus.");
            CallWave(waves, "TryGrantActiveRouteReward", room);
            Assert.That(inventory.currency, Is.EqualTo(before + coins));
            Assert.That(ActiveRouteOffer.HasSelection, Is.False);
            CallWave(waves, "TryGrantActiveRouteReward", room);
            Assert.That(inventory.currency, Is.EqualTo(before + coins), "A consumed card must not pay twice.");
            Assert.That(typeof(RunEndController).GetField("roomsCleared", Private).GetValue(runEnd), Is.EqualTo(1));
        });
    }

    [TestCase(135, 0, 135)]
    [TestCase(135, 1, 189)]
    [TestCase(135, 2, 243)]
    [TestCase(135, 4, 351)]
    [TestCase(135, 9, 621)]
    [TestCase(0, 99, 0)]
    [TestCase(135, -1, 135)]
    [TestCase(-1, 3, 0)]
    [TestCase(int.MaxValue, int.MaxValue, int.MaxValue)]
    public void CombatSoulUsesFortyPercentOfBasePerLevelWithoutCompounding(int coins, int biome, int expected)
    {
        var method = typeof(RouteOfferGenerator).GetMethod("ScaleCombatSoul", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method.Invoke(null, new object[] { coins, biome }), Is.EqualTo(expected));
    }

    [TestCase(RouteDifficulty.Easy, 0)]
    [TestCase(RouteDifficulty.Easy, 1)]
    [TestCase(RouteDifficulty.Easy, 2)]
    [TestCase(RouteDifficulty.Easy, 4)]
    [TestCase(RouteDifficulty.Medium, 0)]
    [TestCase(RouteDifficulty.Medium, 1)]
    [TestCase(RouteDifficulty.Medium, 2)]
    [TestCase(RouteDifficulty.Medium, 4)]
    [TestCase(RouteDifficulty.Hard, 0)]
    [TestCase(RouteDifficulty.Hard, 1)]
    [TestCase(RouteDifficulty.Hard, 2)]
    [TestCase(RouteDifficulty.Hard, 4)]
    public void HigherLevelCardsScaleOnlySoulAndKeepTheSameArtifactDrawCount(RouteDifficulty difficulty, int biome)
    {
        WithProgression(biome, progression => {
            var tables = Object.Instantiate(Resources.Load<RouteOfferTables>("MapSO/RouteOfferTables"));
            try
            {
                foreach (var profile in tables.Profiles) profile.rollWeight = profile.difficulty == difficulty ? 1f : 0f;
                var room = Resources.Load<RoomData>("MapSO/LV/RoomData_LV_01");
                var patterns = new HashSet<int>();
                for (int i = 0; i < 96; i++)
                {
                    progression.SetCurrentBiomeIndex(0);
                    Random.InitState(619 + i);
                    var baseline = RouteOfferGenerator.BuildOffer(room, tables);
                    progression.SetCurrentBiomeIndex(biome);
                    Random.InitState(619 + i);
                    var scaled = RouteOfferGenerator.BuildOffer(room, tables);
                    int expected = (int)System.Math.Round(baseline.Reward.Coins * (1d + .4d * biome),
                        System.MidpointRounding.AwayFromZero);
                    Assert.That(scaled.Reward.Coins, Is.EqualTo(expected));
                    Assert.That(scaled.Reward.ArtifactPulls, Is.EqualTo(baseline.Reward.ArtifactPulls));
                    Assert.That(scaled.Reward.GrantsArtifact, Is.EqualTo(baseline.Reward.GrantsArtifact));
                    Assert.That(scaled.Reward.IncludesBasePayout, Is.EqualTo(baseline.Reward.IncludesBasePayout));
                    Assert.That(scaled.Reward.RewardSeed, Is.EqualTo(baseline.Reward.RewardSeed));
                    if (difficulty == RouteDifficulty.Hard)
                    {
                        Assert.That(scaled.Reward.BiomeIndex, Is.EqualTo(biome));
                        patterns.Add(scaled.Reward.ArtifactPulls);
                    }
                }
                if (difficulty == RouteDifficulty.Hard) CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, patterns);
            }
            finally { Object.DestroyImmediate(tables); }
        });
    }

    [TestCase(RouteDifficulty.Easy)]
    [TestCase(RouteDifficulty.Medium)]
    public void ScaledCardPaysTheAdvertisedSoulOnceEvenIfTheLevelChangesLater(RouteDifficulty difficulty)
    {
        WithProgression(4, progression => {
            var tables = Object.Instantiate(Resources.Load<RouteOfferTables>("MapSO/RouteOfferTables"));
            try
            {
                foreach (var profile in tables.Profiles) profile.rollWeight = profile.difficulty == difficulty ? 1f : 0f;
                var room = Resources.Load<RoomData>("MapSO/LV/RoomData_LV_01");
                var card = RouteOfferGenerator.BuildOffer(room, tables);
                int advertised = card.Reward.Coins;
                Assert.That(advertised, Is.InRange(tables.GetProfile(difficulty).minCoins * 2.6,
                    tables.GetProfile(difficulty).maxCoins * 2.6));
                progression.SetCurrentBiomeIndex(9);
                WithClearManagers(room, (runEnd, waves) => {
                    ActiveRouteOffer.Set(card);
                    int before = inventory.currency;
                    CallWave(waves, "NotifyRunEndRoomCleared", true);
                    CallWave(waves, "TryGrantActiveRouteReward", room);
                    Assert.That(inventory.currency, Is.EqualTo(before + advertised));
                    CallWave(waves, "TryGrantActiveRouteReward", room);
                    Assert.That(inventory.currency, Is.EqualTo(before + advertised));
                });
            }
            finally { Object.DestroyImmediate(tables); }
        });
    }

    [Test]
    public void LaterLevelsDoNotIncreaseTheBossBonusOrStartingSoul()
    {
        WithProgression(9, progression => {
            Assert.That(inventory.currency, Is.EqualTo(1000));
            WithClearManagers(null, (runEnd, waves) => {
                runEnd.NotifyRoomCleared(RoomKind.Boss);
                Assert.That(inventory.currency, Is.EqualTo(1500));
            });
        });
    }

    [TestCase(RoomKind.Combat, 0)]
    [TestCase(RoomKind.Elite, 0)]
    [TestCase(RoomKind.Boss, 500)]
    public void FixedClearBonusIsRemovedFromCombatButPreservedForBosses(RoomKind kind, int expected)
    {
        WithClearManagers(null, (runEnd, waves) => {
            int before = inventory.currency;
            runEnd.NotifyRoomCleared(kind);
            Assert.That(inventory.currency, Is.EqualTo(before + expected));
        });
    }

    [TestCase(false, 0)]
    [TestCase(true, 500)]
    public void LegacyWaveWithoutAMountedRoomStillDistinguishesBosses(bool bossFlow, int expected)
    {
        WithClearManagers(null, (runEnd, waves) => {
            int before = inventory.currency;
            typeof(RoomWaveManager).GetField("useBossDeathFlow", Private).SetValue(waves, bossFlow);
            CallWave(waves, "NotifyRunEndRoomCleared", true);
            Assert.That(inventory.currency, Is.EqualTo(before + expected));
        });
    }

    [Test]
    public void NormalCombatClearWithLegacyPayoutEnabledStillCannotRestoreTheFixedBonus()
    {
        WithClearManagers(Resources.Load<RoomData>("MapSO/LV/RoomData_LV_01"), (runEnd, waves) => {
            int before = inventory.currency;
            CallWave(waves, "NotifyRunEndRoomCleared", true);
            Assert.That(inventory.currency, Is.EqualTo(before));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void HardDraftsHaveThreeDistinctSameRarityChoicesAndStableRolls(int biome)
    {
        var table = Resources.Load<ArtifactRollTable>("ItemSO/Artifacts/ArtifactRollTable");
        var method = typeof(RouteReward).Assembly.GetType("Project.Core.ZoneState.ArtifactRewardDraft")
            .GetMethod("TryPrepare", BindingFlags.Public | BindingFlags.Static);
        var terms = RouteReward.Patterned(0, 2, 8431, biome);
        var first = new object[] { terms, table, null };
        var second = new object[] { terms, table, null };
        Assert.That(method.Invoke(null, first), Is.True);
        Assert.That(method.Invoke(null, second), Is.True);
        var draws = (ArtifactInstance[][])first[2];
        var repeated = (ArtifactInstance[][])second[2];
        Assert.That(draws.Length, Is.EqualTo(2));
        var definitions = Resources.LoadAll<ArtifactDefinition>("ItemSO/Artifacts")
            .Where(item => !string.IsNullOrEmpty(item.id)).GroupBy(item => item.id)
            .ToDictionary(group => group.Key, group => group.First());
        for (int pull = 0; pull < draws.Length; pull++)
        {
            Assert.That(draws[pull].Length, Is.EqualTo(3));
            Assert.That(draws[pull].Select(item => item.definitionId).Distinct().Count(), Is.EqualTo(3));
            var rarities = draws[pull].Select(item => definitions[item.definitionId].rarity).Distinct().ToArray();
            Assert.That(rarities.Length, Is.EqualTo(1));
            Assert.That(rarities[0] == ArtifactRarity.R || rarities[0] == ArtifactRarity.SR, Is.True);
            if (biome == 0) Assert.That(rarities[0], Is.EqualTo(ArtifactRarity.R));
            for (int card = 0; card < 3; card++)
            {
                Assert.That(draws[pull][card].definitionId, Is.EqualTo(repeated[pull][card].definitionId));
                Assert.That(draws[pull][card].rolls, Is.EqualTo(repeated[pull][card].rolls));
            }
        }
        int before = inventory.currency;
        Assert.That(inventory.TryCommitArtifact(draws[0][0], 0, out _), Is.True);
        Assert.That(inventory.currency, Is.EqualTo(before));
    }

    [TestCase(false, 0)]
    [TestCase(true, 500)]
    public void LegacyTrackedEnemyClearPreservesBossReward(bool boss, int expected)
    {
        WithClearManagers(null, (runEnd, waves) => {
            var clear = host.AddComponent<RoomClearManager>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(boss
                ? "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab"
                : "Assets/Project/Character/enemy/enemy/Enm101/Enemy1.prefab");
            typeof(RoomClearManager).GetField("enemies", Private).SetValue(clear,
                new List<EnemyController> { prefab.GetComponent<EnemyController>() });
            int before = inventory.currency;
            typeof(RoomClearManager).GetMethod("NotifyRunEndRoomCleared", Private).Invoke(clear, null);
            Assert.That(inventory.currency, Is.EqualTo(before + expected));
        });
    }

    private void WithClearManagers(RoomData room, System.Action<RunEndController, RoomWaveManager> check)
    {
        RunEndController previousRunEnd = RunEndController.Instance;
        RoomLayoutHost previousLayout = RoomLayoutHost.Instance;
        TeamLoader previousTeam = TeamLoader.Instance;
        RoomChoiceOffer previousCard = ActiveRouteOffer.Current;
        bool hadCard = ActiveRouteOffer.HasSelection;
        try
        {
            var runEnd = host.AddComponent<RunEndController>();
            typeof(RunEndController).GetField("runInventory", Private).SetValue(runEnd, inventory);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Core/Manager.prefab");
            using (var authored = new SerializedObject(prefab.GetComponentInChildren<RunEndController>(true)))
            {
                int bonus = authored.FindProperty("roomClearCurrencyReward").intValue;
                Assert.That(bonus, Is.EqualTo(500), "The authored boss reward must stay unchanged.");
                typeof(RunEndController).GetField("roomClearCurrencyReward", Private).SetValue(runEnd, bonus);
            }
            var layout = host.AddComponent<RoomLayoutHost>();
            typeof(RoomLayoutHost).GetField("activeRoom", Private).SetValue(layout, room);
            var team = host.AddComponent<TeamLoader>();
            typeof(TeamLoader).GetField("runInventory", Private).SetValue(team, inventory);
            typeof(RunEndController).GetProperty("Instance").SetValue(null, runEnd);
            typeof(RoomLayoutHost).GetProperty("Instance").SetValue(null, layout);
            typeof(TeamLoader).GetProperty("Instance").SetValue(null, team);
            ActiveRouteOffer.Clear();
            host.AddComponent<BoxCollider2D>().isTrigger = true;
            check(runEnd, host.AddComponent<RoomWaveManager>());
        }
        finally
        {
            typeof(RunEndController).GetProperty("Instance").SetValue(null, previousRunEnd);
            typeof(RoomLayoutHost).GetProperty("Instance").SetValue(null, previousLayout);
            typeof(TeamLoader).GetProperty("Instance").SetValue(null, previousTeam);
            if (hadCard) ActiveRouteOffer.Set(previousCard);
            else ActiveRouteOffer.Clear();
        }
    }

    private static void WithProgression(int biome, System.Action<RunProgressionManager> check)
    {
        var previous = RunProgressionManager.Instance;
        var root = new GameObject("Economy level context");
        root.SetActive(false);
        try
        {
            var progression = root.AddComponent<RunProgressionManager>();
            progression.SetCurrentBiomeIndex(biome);
            typeof(RunProgressionManager).GetProperty("Instance").SetValue(null, progression);
            check(progression);
        }
        finally
        {
            typeof(RunProgressionManager).GetProperty("Instance").SetValue(null, previous);
            Object.DestroyImmediate(root);
        }
    }

    private static void CallWave(RoomWaveManager waves, string method, params object[] arguments)
        => typeof(RoomWaveManager).GetMethod(method, Private).Invoke(waves, arguments);

    private static ArtifactDefinition LoadArtifact(string resource)
    {
        var artifact = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/" + resource);
        Assert.That(artifact, Is.Not.Null, resource);
        return artifact;
    }

    private int OfferPrice(BaseItem item)
    {
        return (int)typeof(ShopManager).GetMethod("CalculateOfferPrice", Private)
            .Invoke(shop, new object[] { item, 1 });
    }
}
#endif
