#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.Core.RunEnd;
using Project.InventorySystem;
using Project.InventorySystem.Items;
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
        yield return new TestCaseData("P1_01_BlindsBoundlessRibbon", 650);
        yield return new TestCaseData("P1_02_FiendFleshMask", 500);
        yield return new TestCaseData("P1_03_SoulMerchantsScales", 650);
        yield return new TestCaseData("P1_04_BrokenChronoRelic", 850);
        yield return new TestCaseData("P1_05_AuraResonatingShard", 650);
        yield return new TestCaseData("P1_06_RivalsBrokenCrest", 850);
        yield return new TestCaseData("P1_07_ShroudOfDeprivation", 500);
        yield return new TestCaseData("P1_08_MemoryDrainingUrn", 500);
        yield return new TestCaseData("P1_09_ShinsRustyDagger", 650);
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
        var artifact = LoadArtifact("P1_07_ShroudOfDeprivation");
        Assert.That(inventory.currency, Is.EqualTo(1000));
        var room = Resources.Load<RoomData>("MapSO/LV/RoomData_LV_01");
        WithClearManagers(room, (runEnd, waves) => {
            for (int i = 0; i < 2; i++)
            {
                ActiveRouteOffer.Set(new RoomChoiceOffer(room, RouteDifficulty.Medium, null,
                    new RouteReward { Coins = profile.minCoins }));
                CallWave(waves, "NotifyRunEndRoomCleared", false);
                CallWave(waves, "TryGrantActiveRouteReward");
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
            Assert.That(shop.TryPurchase(LoadArtifact("P1_02_FiendFleshMask")), Is.True);
            Assert.That(shop.TryPurchase(character, 1, OfferPrice(character)), Is.False);
            Assert.That(inventory.currency, Is.EqualTo(240));
        }
    }

    [Test]
    public void ArtifactBudgetMatchesTheApprovedCardRewards()
    {
        var tables = Resources.Load<RouteOfferTables>("MapSO/RouteOfferTables");
        var easy = tables.GetProfile(RouteDifficulty.Easy);
        var hard = tables.GetProfile(RouteDifficulty.Hard);
        var artifacts = shop.codexInventory.allItems.OfType<ArtifactDefinition>().ToArray();
        Assert.That(artifacts.Length, Is.EqualTo(9));
        Assert.That(artifacts.Select(item => item.price).Distinct().OrderBy(price => price), Is.EqualTo(new[] { 500, 650, 850 }));
        Assert.That(inventory.currency, Is.EqualTo(1000));
        Assert.That(artifacts.Min(item => item.price) * 3, Is.GreaterThan(inventory.currency + 2 * hard.maxCoins),
            "Even saving the starting budget and two maximum Hard rewards cannot buy three artifacts at the first act shop.");
        Assert.That(easy.minCoins, Is.EqualTo(80));
        Assert.That(easy.maxCoins, Is.EqualTo(100));
        Assert.That(hard.minCoins, Is.EqualTo(180));
        Assert.That(hard.maxCoins, Is.EqualTo(220));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void RerollOnlyChangesItsOwnCostNotItemPrices(int rerolls)
    {
        typeof(ShopManager).GetField("rerollCount", Private).SetValue(shop, rerolls);
        Assert.That(shop.CurrentRerollCost, Is.EqualTo(80 + 40 * rerolls));
        foreach (BaseItem item in shop.codexInventory.allItems.Where(item =>
            item is ArtifactDefinition || item is CharacterItem character && character.availableInShop))
        {
            Assert.That(OfferPrice(item), Is.EqualTo(item.price), item.name);
        }
    }

    [Test]
    public void RerollsChargeEightyThenOneTwentyThenOneSixtyAndResetAtANewShop()
    {
        var changes = new List<int>();
        shop.OnRerollCostChanged += changes.Add;
        int currencyChanges = 0;
        inventory.OnCurrencyChanged += _ => currencyChanges++;
        foreach (int cost in new[] { 80, 120, 160 })
        {
            int before = inventory.currency;
            Assert.That(shop.CurrentRerollCost, Is.EqualTo(cost));
            offer.MarkSold();
            Assert.That(shop.TryRerollOffers(), Is.True);
            Assert.That(inventory.currency, Is.EqualTo(before - cost));
            Assert.That(offer.IsSold, Is.False);
            Assert.That(offer.CurrentItem, Is.Not.Null);
            Assert.That(offer.CurrentPrice, Is.EqualTo(offer.CurrentItem.price));
        }
        Assert.That(inventory.currency, Is.EqualTo(640));
        Assert.That(currencyChanges, Is.EqualTo(3));
        Assert.That(changes, Is.EqualTo(new[] { 120, 160, 200 }));
        shop.ResetShopState();
        Assert.That(shop.CurrentRerollCost, Is.EqualTo(80));
        Assert.That(inventory.currency, Is.EqualTo(640));
        Assert.That(currencyChanges, Is.EqualTo(3));
    }

    [Test]
    public void FailedRerollPreservesMoneyOffersSoldStateAndNextCost()
    {
        var artifact = LoadArtifact("P1_07_ShroudOfDeprivation");
        offer.Populate(artifact, 1, artifact.price); offer.MarkSold();
        shop.currentShopItems.Add(artifact);
        inventory.currency = 79;
        var state = Random.state;
        int changes = 0;
        shop.OnShopOffersChanged += () => changes++;
        shop.OnRerollCostChanged += _ => changes++;
        Assert.That(shop.TryRerollOffers(), Is.False);
        Assert.That(inventory.currency, Is.EqualTo(79));
        Assert.That(shop.CurrentRerollCost, Is.EqualTo(80));
        Assert.That(offer.CurrentItem, Is.SameAs(artifact));
        Assert.That(offer.IsSold, Is.True);
        Assert.That(shop.currentShopItems, Is.EqualTo(new[] { artifact }));
        Assert.That(Random.state, Is.EqualTo(state));
        Assert.That(changes, Is.Zero);
        inventory.AddCurrency(1);
        Assert.That(shop.TryRerollOffers(), Is.True);
        Assert.That(inventory.currency, Is.Zero);
        Assert.That(shop.CurrentRerollCost, Is.EqualTo(120));
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
            Assert.That(shop.CurrentRerollCost, Is.EqualTo(80));
            empty.allItems.Add(Resources.Load<CharacterItem>("ItemSO/char_1_CameraGIrl"));
            Assert.That(shop.TryRerollOffers(), Is.False, "Character-only inventory cannot populate an Artifact offer.");
            Assert.That(inventory.currency, Is.EqualTo(1000));
            Assert.That(shop.CurrentRerollCost, Is.EqualTo(80));
        }
        finally { Object.DestroyImmediate(empty); }
    }

    [Test]
    public void RerollPromptShowsTheNextSoulCostRatherThanAMarkup()
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
            Assert.That(method.Invoke(ui, new[] { prompt }), Is.EqualTo("Reroll 80 SOUL"));
            Assert.That(shop.TryRerollOffers(), Is.True);
            Assert.That(method.Invoke(ui, new[] { prompt }), Is.EqualTo("Reroll 120 SOUL"));
            inventory.currency = 119;
            Assert.That(shop.TryRerollOffers(), Is.False);
            Assert.That(method.Invoke(ui, new[] { prompt }), Is.EqualTo("Reroll 120 SOUL"));
        }
        finally { Object.DestroyImmediate(promptHost); }
    }

    [Test]
    public void AuthoredShopUsesTheSameFeesAndBasePricesAsTheTransactions()
    {
        var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath("d36c77798e07625409da16f442bc42f5"));
        try
        {
            var liveShop = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ShopManager>(true)).Single();
            liveShop.playerInventory = inventory;
            using var data = new SerializedObject(liveShop);
            Assert.That(data.FindProperty("rerollBaseCost").intValue, Is.EqualTo(80));
            Assert.That(data.FindProperty("rerollCostStep").intValue, Is.EqualTo(40));
            liveShop.ResetShopState();
            Assert.That(liveShop.TryRerollOffers(), Is.True);
            Assert.That(inventory.currency, Is.EqualTo(920));
            Assert.That(liveShop.CurrentRerollCost, Is.EqualTo(120));
            foreach (var list in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ShopItemListUI>(true)))
            {
                if (list.CurrentItem == null) continue;
                int expected = list.CurrentItem is Item ? list.CurrentItem.price * list.CurrentQuantity : list.CurrentItem.price;
                Assert.That(list.CurrentPrice, Is.EqualTo(expected));
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
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
                Assert.That(card.Reward.Coins, Is.InRange(minimum, maximum));
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
            CallWave(waves, "TryGrantActiveRouteReward");
            Assert.That(inventory.currency, Is.EqualTo(before + coins));
            Assert.That(ActiveRouteOffer.HasSelection, Is.False);
            CallWave(waves, "TryGrantActiveRouteReward");
            Assert.That(inventory.currency, Is.EqualTo(before + coins), "A consumed card must not pay twice.");
            Assert.That(typeof(RunEndController).GetField("roomsCleared", Private).GetValue(runEnd), Is.EqualTo(1));
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
            CallWave(waves, "NotifyRunEndRoomCleared", bossFlow);
            Assert.That(inventory.currency, Is.EqualTo(before + expected));
        });
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
