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
        yield return new TestCaseData("P1_01_BlindsBoundlessRibbon", 160);
        yield return new TestCaseData("P1_02_FiendFleshMask", 100);
        yield return new TestCaseData("P1_03_SoulMerchantsScales", 180);
        yield return new TestCaseData("P1_04_BrokenChronoRelic", 300);
        yield return new TestCaseData("P1_05_AuraResonatingShard", 140);
        yield return new TestCaseData("P1_06_RivalsBrokenCrest", 240);
        yield return new TestCaseData("P1_07_ShroudOfDeprivation", 80);
        yield return new TestCaseData("P1_08_MemoryDrainingUrn", 120);
        yield return new TestCaseData("P1_09_ShinsRustyDagger", 220);
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

    [Test]
    public void TwoMinimumMediumRewardsFundCameraGirlAndOneStarterArtifact()
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
        Assert.That(shop.TryPurchase(character, 1, OfferPrice(character)), Is.True);
        Assert.That(shop.TryPurchase(artifact, 1, OfferPrice(artifact)), Is.True);
        Assert.That(inventory.HasItem(character), Is.True);
        Assert.That(inventory.GetItemCount(artifact), Is.EqualTo(1));
        Assert.That(inventory.currency, Is.EqualTo(60));
    }

    [Test]
    public void ArtifactBudgetMatchesTheApprovedCardRewards()
    {
        var tables = Resources.Load<RouteOfferTables>("MapSO/RouteOfferTables");
        var easy = tables.GetProfile(RouteDifficulty.Easy);
        var hard = tables.GetProfile(RouteDifficulty.Hard);
        var artifacts = shop.codexInventory.allItems.OfType<ArtifactDefinition>().ToArray();
        Assert.That(artifacts.Length, Is.EqualTo(9));
        Assert.That(artifacts.Min(item => item.price), Is.LessThanOrEqualTo(easy.minCoins + easy.maxCoins),
            "Two average Easy rewards should cover an entry artifact, excluding the initial budget.");
        Assert.That(artifacts.Max(item => item.price), Is.LessThanOrEqualTo(2 * hard.minCoins),
            "Two minimum Hard rewards should cover any artifact at its base price.");
        Assert.That(easy.minCoins, Is.EqualTo(80));
        Assert.That(easy.maxCoins, Is.EqualTo(100));
        Assert.That(hard.minCoins, Is.EqualTo(180));
        Assert.That(hard.maxCoins, Is.EqualTo(220));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void RerollKeepsExistingFifteenPercentMarkupOnNewBasePrices(int rerolls)
    {
        typeof(ShopManager).GetField("rerollCount", Private).SetValue(shop, rerolls);
        float multiplier = 1f + .15f * rerolls;
        Assert.That(shop.CurrentRerollPriceMultiplier, Is.EqualTo(multiplier).Within(.0001f));
        foreach (BaseItem item in shop.codexInventory.allItems.Where(item =>
            item is ArtifactDefinition || item is CharacterItem character && character.availableInShop))
        {
            Assert.That(OfferPrice(item), Is.EqualTo(Mathf.CeilToInt(item.price * multiplier)), item.name);
        }
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
            runEnd.NotifyRoomCleared(awardLegacyCurrency: kind == RoomKind.Boss);
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
