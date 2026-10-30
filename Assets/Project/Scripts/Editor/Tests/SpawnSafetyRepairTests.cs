#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public class SpawnSafetyRepairTests
{
    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void SavedSpawnClearanceSeparatesTwoActual101Bodies(int level)
    {
        var root=PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        GameObject first=null,second=null;
        try
        {
            var waves=root.GetComponentInChildren<RoomWaveManager>();
            using(var data=new SerializedObject(waves)) Assert.That(data.FindProperty("minSpawnSeparation").floatValue,Is.GreaterThanOrEqualTo(SpawnSafetyRepair.Separation));
            var slots=root.GetComponentInChildren<CombatStageController>().Chambers[0].spawnSlots;
            waves.BindSpawnSlots(slots);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var occupied=(List<Vector3>)typeof(RoomWaveManager).GetField("livingEnemyPositions",flags).GetValue(waves);
            var resolve=typeof(RoomWaveManager).GetMethod("TryResolveSeparatedPosition",flags);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/Enm101/Enemy1.prefab");
            first=Object.Instantiate(prefab); second=Object.Instantiate(prefab);
            int accepted=0;
            foreach(var slot in slots)
            {
                occupied.Clear(); occupied.Add(slot.position);
                object[] args={slot,Vector3.zero};
                if(!(bool)resolve.Invoke(waves,args)) continue;
                accepted++;
                var a=first.GetComponent<EnemyController>(); var b=second.GetComponent<EnemyController>();
                first.transform.position=slot.position; a.rb.position=slot.position;
                second.transform.position=(Vector3)args[1]; b.rb.position=second.transform.position;
                Physics2D.SyncTransforms();
                Assert.That(Physics2D.Distance(a.bodyCollider,b.bodyCollider).distance,Is.GreaterThan(.04f),$"LV{level} {slot.name} still stacks the real 101 colliders.");
            }
            Assert.That(accepted,Is.GreaterThan(0),"The check must exercise a successful nudge, not just rejected spawns.");
        }
        finally
        {
            if(first!=null) Object.DestroyImmediate(first); if(second!=null) Object.DestroyImmediate(second);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [TestCase(1)] [TestCase(2)] [TestCase(5)] [TestCase(10)]
    public void OnlyTheFourApprovedMarkersMoveLeftAndKeepTheirOriginalHeight(int level)
    {
        var root=PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            var plan=LevelDesignLayoutBuilder.GetPlan(level);
            var rooms=LevelDesignSpawnBuilder.Rooms(level);
            var chambers=root.GetComponentInChildren<CombatStageController>().Chambers;
            for(int room=0;room<rooms.Length;room++)
                for(int slot=0;slot<rooms[room].slots.Length;slot++)
                {
                    Vector3 original=LevelDesignLayoutBuilder.ToWorld(plan,rooms[room].slots[slot])+Vector2.up*.05f;
                    Vector3 actual=chambers[room].spawnSlots[slot].position;
                    float shift=room==SpawnSafetyRepair.TargetRoom(level) && slot==4 ? -.5f : 0;
                    Assert.That(actual.x,Is.EqualTo(original.x+shift).Within(.001f));
                    Assert.That(actual.y,Is.EqualTo(original.y).Within(.001f));
                    Assert.That(actual.z,Is.EqualTo(original.z).Within(.001f));
                }
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif
