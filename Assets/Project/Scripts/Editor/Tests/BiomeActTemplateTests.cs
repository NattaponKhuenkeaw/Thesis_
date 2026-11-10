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
        Assert.That(director.BiomeTemplate, Is.EqualTo(new[] {
            BiomeActNode.Combat, BiomeActNode.Combat, BiomeActNode.SafeRoom,
            BiomeActNode.Combat, BiomeActNode.SafeRoom, BiomeActNode.Combat,
            BiomeActNode.SafeRoom, BiomeActNode.Boss
        }));
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
        SetOptionalSafeChance(0f);
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
            for (int step = 0; step < director.BiomeTemplate.Count; step++)
            {
                var offers = OffersFrom(current);
                Assert.That(offers, Is.Not.Empty);
                var node = director.BiomeTemplate[step];
                Assert.That(offers.All(o => node == BiomeActNode.SafeRoom ? o.Room.ZoneMode == ZoneMode.SafeZone
                    : o.Kind == (node == BiomeActNode.Boss ? RoomKind.Boss : RoomKind.Combat)), Is.True);
                var next = offers[0];
                director.NotifyLeavingRoom(current.RoomId);
                EnterRoom(next.RoomId);
                current = next.Room;
                if (next.Kind == RoomKind.Combat)
                    Assert.That(next.Room.BiomeIndex, Is.EqualTo(biome % 2));
            }
            Assert.That(current.RoomId, Is.EqualTo(biome % 2 == 0 ? "Boss_01" : "Boss_02"));
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
    public void ReopeningOffersAndRetryingTransitionDoesNotConsumeNodesOrCountClearsTwice()
    {
        RoomData lobby = map.GetRoomById("Test_Lobby");
        var first = director.GenerateLeaveOffersFromSafeRoom(lobby.RoomId)[0];
        for (int i = 0; i < 3; i++)
        {
            director.GenerateLeaveOffersFromSafeRoom(lobby.RoomId);
            director.NotifyLeavingRoom(lobby.RoomId);
            Assert.That(director.ActIndex, Is.Zero, "Preparing/aborting a transition must not consume a node");
            Assert.That(director.LastOffers[0].RoomId, Is.EqualTo(first.RoomId));
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
            Assert.That(director.LastOffers.All(o => o.Kind == RoomKind.Combat), Is.True);
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
    public void OptionalSafeReplacesOneCombatWithoutAddingAnotherRouteSlot()
    {
        SetOptionalSafeChance(1f);
        RoomData current = map.GetRoomById("Test_Lobby");
        var entered = new List<RoomKind>();
        for (int step = 0; step < 16 && current.RoomKind != RoomKind.Boss; step++)
        {
            var offers = OffersFrom(current);
            Assert.That(offers, Is.Not.Empty);
            if (step == 0) Assert.That(offers.Any(o => o.Kind == RoomKind.Combat), Is.True);
            var next = offers.FirstOrDefault(o => step == 0 ? o.Room.ZoneMode == ZoneMode.SafeZone : o.Kind == RoomKind.Combat);
            if (step == 0) Assert.That(next.IsValid, Is.True);
            if (!next.IsValid) next = offers[0];
            director.NotifyLeavingRoom(current.RoomId);
            EnterRoom(next.RoomId);
            current = next.Room;
            entered.Add(next.Kind);
        }
        Assert.That(entered.Count, Is.EqualTo(8));
        Assert.That(entered.Count(k => k == RoomKind.Combat), Is.EqualTo(3));
        Assert.That(entered.Last(), Is.EqualTo(RoomKind.Boss));
    }

    private void SetOptionalSafeChance(float chance)
    {
        typeof(RoomChoiceDirector).GetField("optionalSafeRoomChance", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(director, chance);
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
