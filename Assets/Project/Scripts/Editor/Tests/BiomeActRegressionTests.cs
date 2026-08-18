#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEngine;

public class BiomeActRegressionTests
{
    [Test]
    public void ForcedBossResolvesWithoutDepthDue()
    {
        var go = new GameObject(nameof(ForcedBossResolvesWithoutDepthDue));
        try
        {
            RunProgressionManager progression = go.AddComponent<RunProgressionManager>();
            progression.currentRoomDepth = 0;
            progression.roomsBeforeBoss = 99;

            Assert.That(progression.IsBossDue, Is.False);
            Assert.That(
                progression.TryResolveForcedBossRoom(null, out RoomData boss, requireDue: false),
                Is.True);
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.RoomKind, Is.EqualTo(RoomKind.Boss));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ForcedBossRequiresDueWhenFlagSet()
    {
        var go = new GameObject(nameof(ForcedBossRequiresDueWhenFlagSet));
        try
        {
            RunProgressionManager progression = go.AddComponent<RunProgressionManager>();
            progression.currentRoomDepth = 0;
            progression.roomsBeforeBoss = 99;

            Assert.That(
                progression.TryResolveForcedBossRoom(null, out _, requireDue: true),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
#endif
