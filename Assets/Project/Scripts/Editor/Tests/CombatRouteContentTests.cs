#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEngine;

public class CombatRouteContentTests
{
    [TestCase(RouteDifficulty.Easy, 12)]
    [TestCase(RouteDifficulty.Medium, 16)]
    [TestCase(RouteDifficulty.Hard, 20)]
    public void EveryCombatLayoutSharesTheCardBudgetAcrossItsChambers(RouteDifficulty difficulty, int budget)
    {
        var previous = ActiveRouteOffer.Current;
        bool hadOffer = ActiveRouteOffer.HasSelection;
        var rooms = Resources.Load<MapSO>("MapSO/Test_MapSO").Rooms
            .Where(room => room != null && room.RoomKind == RoomKind.Combat && room.IncludeInRandomOffers).ToArray();
        Assert.That(rooms.Length, Is.EqualTo(10));
        var buildCounts = typeof(CombatStageController).GetMethod("BuildRouteChamberCounts",
            BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            foreach (var room in rooms)
            {
                ActiveRouteOffer.Set(new RoomChoiceOffer(room, difficulty, null, null));
                var stage = room.LayoutPrefab.GetComponentInChildren<CombatStageController>(true);
                var counts = (int[])buildCounts.Invoke(stage, null);
                int playable = stage.Chambers.Count(chamber => chamber != null && chamber.enemyCount > 0);
                int remainingExtra = budget % playable;
                var expected = stage.Chambers.Select(chamber => chamber != null && chamber.enemyCount > 0
                    ? budget / playable + (remainingExtra-- > 0 ? 1 : 0) : 0).ToArray();
                Assert.That(counts, Is.EqualTo(expected), room.RoomId);
                Assert.That(counts.Sum(), Is.EqualTo(budget), room.RoomId);
            }
        }
        finally
        {
            if (hadOffer) ActiveRouteOffer.Set(previous); else ActiveRouteOffer.Clear();
        }
    }

    [Test]
    public void UsesAuthoredChambersDetectsStageControllerOnLayout()
    {
        var layout = new GameObject(nameof(UsesAuthoredChambersDetectsStageControllerOnLayout));
        try
        {
            Assert.That(CombatRouteContent.UsesAuthoredChambers(layout), Is.False);
            layout.AddComponent<CombatStageController>();
            Assert.That(CombatRouteContent.UsesAuthoredChambers(layout), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(layout);
        }
    }

    [Test]
    public void ResolvePalettePicksDifficultyAsset()
    {
        EncounterSO easy = ScriptableObject.CreateInstance<EncounterSO>();
        EncounterSO medium = ScriptableObject.CreateInstance<EncounterSO>();
        EncounterSO hard = ScriptableObject.CreateInstance<EncounterSO>();
        var melee = new GameObject("melee");
        var ranged = new GameObject("ranged");
        try
        {
            AddType(easy, melee);
            AddType(medium, melee);
            AddType(hard, ranged);

            Assert.That(
                CombatRouteContent.ResolvePalette(RouteDifficulty.Easy, easy, medium, hard),
                Is.SameAs(easy));
            Assert.That(
                CombatRouteContent.ResolvePalette(RouteDifficulty.Medium, easy, medium, hard),
                Is.SameAs(medium));
            Assert.That(
                CombatRouteContent.ResolvePalette(RouteDifficulty.Hard, easy, medium, hard),
                Is.SameAs(hard));
        }
        finally
        {
            Object.DestroyImmediate(easy);
            Object.DestroyImmediate(medium);
            Object.DestroyImmediate(hard);
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(ranged);
        }
    }

    [Test]
    public void CombatOfferShowsDifficultyWithoutGeneratedWaves()
    {
        RoomData room = ScriptableObject.CreateInstance<RoomData>();
        try
        {
            SetRoomKind(room, RoomKind.Combat);
            var offer = new RoomChoiceOffer(room, RouteDifficulty.Hard, null, new RouteReward { Coins = 200 });
            Assert.That(offer.HasGeneratedCombat, Is.False);
            Assert.That(offer.ShowsDifficulty, Is.True);
            Assert.That(offer.Difficulty, Is.EqualTo(RouteDifficulty.Hard));
        }
        finally
        {
            Object.DestroyImmediate(room);
        }
    }

    [Test]
    public void ShopOfferDoesNotShowCombatDifficulty()
    {
        RoomData room = ScriptableObject.CreateInstance<RoomData>();
        try
        {
            SetRoomKind(room, RoomKind.Shop);
            var offer = new RoomChoiceOffer(room);
            Assert.That(offer.ShowsDifficulty, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(room);
        }
    }

    [Test]
    public void ResolveChamberEncounterPrefersActiveCardPalette()
    {
        EncounterSO easy = ScriptableObject.CreateInstance<EncounterSO>();
        EncounterSO hard = ScriptableObject.CreateInstance<EncounterSO>();
        RoomData room = ScriptableObject.CreateInstance<RoomData>();
        var melee = new GameObject("melee");
        var ranged = new GameObject("ranged");
        try
        {
            AddType(easy, melee);
            AddType(hard, ranged);
            SetRoomKind(room, RoomKind.Combat);
            ActiveRouteOffer.Set(new RoomChoiceOffer(room, RouteDifficulty.Hard, null, null));

            EncounterSO resolved = CombatRouteContent.ResolvePalette(
                ActiveRouteOffer.Current.Difficulty,
                easy,
                easy,
                hard);

            Assert.That(resolved, Is.SameAs(hard));
            Assert.That(resolved, Is.Not.SameAs(easy));
        }
        finally
        {
            ActiveRouteOffer.Clear();
            Object.DestroyImmediate(easy);
            Object.DestroyImmediate(hard);
            Object.DestroyImmediate(room);
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(ranged);
        }
    }

    [Test]
    public void ChamberCombatOfferOmitsGeneratedWavesAndKeepsReward()
    {
        RoomData room = Resources.Load<RoomData>("MapSO/RoomData_Combat_01");
        if (room == null || room.LayoutPrefab == null)
        {
            Assert.Ignore("RoomData_Combat_01 is not available.");
        }

        Assert.That(CombatRouteContent.UsesAuthoredChambers(room.LayoutPrefab), Is.True);

        RoomChoiceOffer offer = RouteOfferGenerator.BuildOffer(room);
        Assert.That(offer.ShowsDifficulty, Is.True);
        Assert.That(offer.HasGeneratedCombat, Is.False);
        Assert.That(offer.Reward, Is.Not.Null);
        Assert.That(offer.Reward.Coins, Is.GreaterThan(0));
    }

    private static void AddType(EncounterSO encounter, GameObject prefab)
    {
        var so = new UnityEditor.SerializedObject(encounter);
        UnityEditor.SerializedProperty types = so.FindProperty("enemyTypes");
        types.arraySize = 1;
        types.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue = prefab;
        types.GetArrayElementAtIndex(0).FindPropertyRelative("weight").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRoomKind(RoomData room, RoomKind kind)
    {
        var so = new UnityEditor.SerializedObject(room);
        so.FindProperty("roomKind").enumValueIndex = (int)kind;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
