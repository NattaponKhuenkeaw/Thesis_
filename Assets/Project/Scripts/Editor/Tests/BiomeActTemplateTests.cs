#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEngine;

public class BiomeActTemplateTests
{
    [Test]
    public void DefaultTemplateHasFourCombatNodesBeforeBoss()
    {
        var go = new GameObject(nameof(BiomeActTemplateTests));
        try
        {
            RoomChoiceDirector director = go.AddComponent<RoomChoiceDirector>();
            Assert.That(director.CountCombatNodesBeforeBoss(), Is.EqualTo(4));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void DefaultTemplateEndsWithBoss()
    {
        var go = new GameObject(nameof(BiomeActTemplateTests) + "_Boss");
        try
        {
            RoomChoiceDirector director = go.AddComponent<RoomChoiceDirector>();
            IReadOnlyList<BiomeActNode> template = director.BiomeTemplate;
            Assert.That(template[template.Count - 1], Is.EqualTo(BiomeActNode.Boss));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OfferCountIsAtLeastThree()
    {
        var go = new GameObject(nameof(BiomeActTemplateTests) + "_Offers");
        try
        {
            RoomChoiceDirector director = go.AddComponent<RoomChoiceDirector>();
            Assert.That(director.OfferCount, Is.GreaterThanOrEqualTo(3));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

#if UNITY_EDITOR
    [Test]
    public void ManagerPrefabOfferCountIsThree()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefab/Core/Manager.prefab");
        Assert.That(prefab, Is.Not.Null, "Manager prefab missing.");
        RoomChoiceDirector director = prefab.GetComponentInChildren<RoomChoiceDirector>(true);
        Assert.That(director, Is.Not.Null, "RoomChoiceDirector missing on Manager prefab.");
        Assert.That(director.OfferCount, Is.EqualTo(3));
    }
#endif
}

public class BiomeActRouteProgressionTests
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private GameObject root;
    private RoomChoiceDirector director;
    private RunProgressionManager progression;
    private MapSO map;
    private RoomChoiceDirector previousDirector;
    private RunProgressionManager previousProgression;
    private Random.State randomState;

    [SetUp]
    public void SetUp()
    {
        previousDirector = RoomChoiceDirector.Instance;
        previousProgression = RunProgressionManager.Instance;
        SetInstance<RoomChoiceDirector>(null);
        SetInstance<RunProgressionManager>(null);
        randomState = Random.state;
        root = new GameObject(nameof(BiomeActRouteProgressionTests));
        progression = root.AddComponent<RunProgressionManager>();
        SetInstance(progression);
        director = root.AddComponent<RoomChoiceDirector>();
        SetInstance(director);
        map = Resources.Load<MapSO>("MapSO/Test_MapSO");
        Assert.That(map, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        SetInstance(previousDirector);
        SetInstance(previousProgression);
        Random.state = randomState;
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(17)]
    public void LobbyToBossMatchesTemplateAcrossThreeConsecutiveBiomes(int seed)
    {
        Random.InitState(seed);
        for (int biome = 0; biome < 3; biome++)
        {
            RoomData current = map.GetRoomById("Test_Lobby");
            var entered = new List<RoomKind>();
            for (int step = 0; step < 16 && current.RoomKind != RoomKind.Boss; step++)
            {
                var offers = OffersFrom(current);
                Assert.That(offers, Is.Not.Empty);
                BiomeActNode node = director.BiomeTemplate[step];
                if (node == BiomeActNode.SafeRoom)
                    Assert.That(offers.All(o => o.Room.ZoneMode == ZoneMode.SafeZone), Is.True);
                var next = node == BiomeActNode.Combat
                    ? offers.First(o => o.Kind == RoomKind.Combat) : offers[0];
                director.NotifyLeavingRoom(current.RoomId);
                EnterRoom(next.RoomId);
                current = next.Room;
                entered.Add(next.Kind);
                if (next.Kind == RoomKind.Combat)
                    Assert.That(next.Room.BiomeIndex, Is.EqualTo(biome % 2));
            }
            Assert.That(entered.Count, Is.EqualTo(8), $"Biome {biome}: extra or missing route node");
            Assert.That(entered.Last(), Is.EqualTo(RoomKind.Boss));
            Assert.That(progression.currentRoomDepth, Is.EqualTo(4));
            Assert.That(director.ActIndex, Is.EqualTo(7));
            var afterBoss = director.GenerateOffersAfterClear(current.RoomId);
            Assert.That(afterBoss.Any(o => o.Kind == RoomKind.Rest), Is.True);
            Assert.That(progression.CurrentBiomeIndex, Is.EqualTo(biome + 1));
            director.GenerateOffersAfterClear(current.RoomId);
            Assert.That(progression.CurrentBiomeIndex, Is.EqualTo(biome + 1), "Repeated clear must not advance biome twice");
        }
    }

    [Test]
    public void CombatPrizesGuaranteeSoulAndFavorHardOverMedium()
    {
        Random.InitState(203);
        RoomData combat = map.Rooms.First(room => room != null && room.RoomKind == RoomKind.Combat);
        int medium = 0;
        int hard = 0;
        for (int i = 0; i < 256; i++)
        {
            RoomChoiceOffer offer = RouteOfferGenerator.BuildOffer(combat);
            Assert.That(offer.Reward, Is.Not.Null);
            if (offer.Difficulty == RouteDifficulty.Hard)
            {
                Assert.That(offer.Reward.Coins, Is.EqualTo(219));
                hard++;
                continue;
            }
            Assert.That(offer.Difficulty, Is.EqualTo(RouteDifficulty.Medium));
            Assert.That(offer.Reward.Coins, Is.EqualTo(175));
            Assert.That(offer.Reward.ArtifactPulls, Is.Zero);
            Assert.That(offer.Reward.GrantsArtifact, Is.False);
            medium++;
        }
        Assert.That(medium, Is.Positive);
        Assert.That(hard, Is.GreaterThan(medium));
    }

    [Test]
    public void ReopeningOffersAndRetryingTransitionDoesNotConsumeNodesOrCountClearsTwice()
    {
        RoomData lobby = map.GetRoomById("Test_Lobby");
        director.GenerateLeaveOffersFromSafeRoom(lobby.RoomId);
        var first = director.LastOffers.First(o => o.Kind == RoomKind.Combat);
        var firstCard = director.LastOffers[0];
        for (int i = 0; i < 3; i++)
        {
            director.GenerateLeaveOffersFromSafeRoom(lobby.RoomId);
            director.NotifyLeavingRoom(lobby.RoomId);
            Assert.That(director.ActIndex, Is.Zero, "Preparing/aborting a transition must not consume a node");
            Assert.That(director.LastOffers[0].RoomId, Is.EqualTo(firstCard.RoomId));
        }
        EnterRoom(first.RoomId);
        Assert.That(director.ActIndex, Is.EqualTo(1));
        EnterRoom(first.RoomId);
        Assert.That(director.ActIndex, Is.EqualTo(1), "Repeated arrival must be idempotent");
        for (int i = 0; i < 3; i++)
        {
            director.GenerateOffersAfterClear(first.RoomId);
            Assert.That(director.ActIndex, Is.EqualTo(1));
            Assert.That(progression.currentRoomDepth, Is.EqualTo(1));
            Assert.That(director.LastOffers.Any(o => o.Kind == RoomKind.Combat), Is.True);
        }
    }

    [Test]
    public void ResetRunDiscardsPendingOffersAndStartsAtTheFirstCombatAgain()
    {
        var first = director.GenerateLeaveOffersFromSafeRoom("Test_Lobby")[0];
        director.NotifyRoomEntered(first.RoomId);
        var pending = director.GenerateOffersAfterClear(first.RoomId)[0];
        director.ResetRouteState();
        director.NotifyRoomEntered(pending.RoomId);
        Assert.That(director.ActIndex, Is.Zero);
        Assert.That(progression.currentRoomDepth, Is.Zero);
        Assert.That(director.LastOffers, Is.Empty);
        var restarted = director.GenerateLeaveOffersFromSafeRoom("Test_Lobby")[0];
        director.NotifyRoomEntered(restarted.RoomId);
        Assert.That(director.ActIndex, Is.EqualTo(1));
    }

    [Test]
    public void UnofferedDestinationDoesNotConsumeThePendingChoice()
    {
        var offer = director.GenerateLeaveOffersFromSafeRoom("Test_Lobby")[0];
        director.NotifyRoomEntered("Boss_01");
        Assert.That(director.ActIndex, Is.Zero);
        Assert.That(director.LastOffers.Count, Is.EqualTo(3));
        director.NotifyRoomEntered(offer.RoomId);
        Assert.That(director.ActIndex, Is.EqualTo(1));
    }

    [Test]
    public void OptionalSafeConsumesOneNodeAndGuaranteedSafesStillHaveADestination()
    {
        typeof(RoomChoiceDirector).GetField("optionalSafeRoomChance", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(director, 1f);
        RoomData current = map.GetRoomById("Test_Lobby");
        for (int step = 0; step < director.BiomeTemplate.Count; step++)
        {
            var offers = OffersFrom(current);
            var safeOffers = offers.Where(o => o.Room.ZoneMode == ZoneMode.SafeZone).ToArray();
            if (director.BiomeTemplate[step] == BiomeActNode.SafeRoom)
                Assert.That(safeOffers.Length, Is.EqualTo(offers.Count).And.GreaterThan(0));
            if (step == 0)
            {
                Assert.That(safeOffers.Length, Is.EqualTo(2));
                Assert.That(offers.Count(o => o.Kind == RoomKind.Combat), Is.EqualTo(1));
            }
            var next = safeOffers.Length > 0 ? safeOffers[0] : offers[0];
            director.NotifyLeavingRoom(current.RoomId);
            EnterRoom(next.RoomId);
            current = next.Room;
            Assert.That(director.ActIndex, Is.EqualTo(Mathf.Min(step + 1, director.BiomeTemplate.Count - 1)));
        }
        Assert.That(current.RoomKind, Is.EqualTo(RoomKind.Boss));
    }

    private IReadOnlyList<RoomChoiceOffer> OffersFrom(RoomData room)
    {
        return room.ZoneMode == ZoneMode.SafeZone
            ? director.GenerateLeaveOffersFromSafeRoom(room.RoomId)
            : director.GenerateOffersAfterClear(room.RoomId);
    }

    private void EnterRoom(string roomId)
    {
        director.NotifyRoomEntered(roomId);
    }

    private static void SetInstance<T>(T value)
    {
        typeof(T).GetField("<Instance>k__BackingField", StaticPrivate).SetValue(null, value);
    }
}
#endif
