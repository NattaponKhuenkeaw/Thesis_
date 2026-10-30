#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class RoomTravelDebugTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void ProductionListContainsOnlyCatalogRoomsAndTenUsedCombatLayouts()
    {
        var host = new GameObject("F10 catalog test");
        try
        {
            var traveler = host.AddComponent<RoomTravelDebug>();
            var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
            Set(traveler, "mapData", map);
            traveler.RefreshRoomList();
            var rooms = Rooms(traveler);
            CollectionAssert.AreEquivalent(map.Rooms.Where(r => r != null).GroupBy(r => r.RoomId).Select(g => g.First()), rooms);
            Assert.That(rooms.Count(r => r.RoomKind == RoomKind.Combat && r.UsesLayoutSwap), Is.EqualTo(10));
            Assert.That(rooms.Any(r => r.RoomId.StartsWith("Debug_") || AssetDatabase.GetAssetPath(r).Contains("/MapSO/LV/")), Is.False);
            Assert.That(rooms.Any(r => r.RoomKind == RoomKind.Boss), Is.True, "Boss is used even though it is not a random offer");
            Assert.That(rooms.Any(r => r.RoomId == "Test_Lobby"), Is.True);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void EmptyAssignedCatalogDoesNotPullDraftAndLegacyRoomsFromResources()
    {
        var host = new GameObject("F10 empty catalog test");
        var map = ScriptableObject.CreateInstance<MapSO>();
        try
        {
            var traveler = host.AddComponent<RoomTravelDebug>(); Set(traveler, "mapData", map);
            traveler.RefreshRoomList();
            Assert.That(Rooms(traveler), Is.Empty);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(map); }
    }

    [TestCase(RoomKind.Rest)] [TestCase(RoomKind.Level)] [TestCase(RoomKind.Boss)]
    public void FilterCanReachEveryUsedKindWithoutVisitingUnusedKinds(RoomKind target)
    {
        var host = new GameObject("F10 filter test");
        try
        {
            var traveler = host.AddComponent<RoomTravelDebug>();
            Set(traveler, "mapData", Resources.Load<MapSO>("MapSO/Test_MapSO")); traveler.RefreshRoomList();
            var kinds = Rooms(traveler).Select(r => r.RoomKind).Distinct().OrderBy(k => k).ToArray();
            var next = typeof(RoomTravelDebug).GetMethod("NextKind", Private);
            var previous = typeof(RoomTravelDebug).GetMethod("PreviousKind", Private);
            var visited = new HashSet<RoomKind>();
            var current = kinds[0];
            for (int i = 0; i < kinds.Length; i++)
            {
                visited.Add(current);
                var following = (RoomKind)next.Invoke(traveler, new object[] { current });
                Assert.That(previous.Invoke(traveler, new object[] { following }), Is.EqualTo(current));
                current = following;
            }
            CollectionAssert.AreEquivalent(kinds, visited);
            Assert.That(current, Is.EqualTo(kinds[0]));
            Assert.That(visited.Contains(target), Is.True);
            Set(traveler, "filterEnabled", true); Set(traveler, "filterKindValue", target); traveler.RefreshRoomList();
            Assert.That(Rooms(traveler), Is.Not.Empty);
            Assert.That(Rooms(traveler).All(r => r.RoomKind == target), Is.True);
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static List<RoomData> Rooms(RoomTravelDebug traveler) => (List<RoomData>)typeof(RoomTravelDebug).GetField("rooms", Private).GetValue(traveler);
    private static void Set(RoomTravelDebug traveler, string name, object value) => typeof(RoomTravelDebug).GetField(name, Private).SetValue(traveler, value);
}
#endif
