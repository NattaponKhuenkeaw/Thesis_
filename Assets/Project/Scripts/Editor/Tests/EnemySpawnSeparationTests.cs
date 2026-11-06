#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class EnemySpawnSeparationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    private static bool Resolve(RoomWaveManager waves,Transform slot)
    {
        object[] args={slot,Vector3.zero};
        return (bool)typeof(RoomWaveManager).GetMethod("TryResolveSeparatedPosition",Private).Invoke(waves,args);
    }

    [Test]
    public void FullSlotsRejectTheSpawnInsteadOfReturningAnOverlappingFallback()
    {
        var go=new GameObject("Full Spawn Slots");
        try
        {
            go.AddComponent<BoxCollider2D>(); var waves=go.AddComponent<RoomWaveManager>();
            var slot=new GameObject("Spawn_0"); slot.transform.SetParent(go.transform,false);
            waves.BindSpawnSlots(new[] {slot.transform});
            var positions=(List<Vector3>)typeof(RoomWaveManager).GetField("livingEnemyPositions",Private).GetValue(waves);
            positions.Add(Vector3.zero);
            Assert.That(Resolve(waves,slot.transform),Is.False);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void NudgeDoesNotBorrowSpaceFromASlotOnAnotherFloor()
    {
        var go=new GameObject("Multi Floor Spawn Slots");
        try
        {
            go.AddComponent<BoxCollider2D>(); var waves=go.AddComponent<RoomWaveManager>();
            var low=new GameObject("Spawn_0"); low.transform.SetParent(go.transform,false);
            var high=new GameObject("Spawn_1"); high.transform.SetParent(go.transform,false); high.transform.localPosition=new Vector3(20,10,0);
            waves.BindSpawnSlots(new[] {low.transform,high.transform});
            ((List<Vector3>)typeof(RoomWaveManager).GetField("livingEnemyPositions",Private).GetValue(waves)).Add(Vector3.zero);
            Assert.That(Resolve(waves,low.transform),Is.False);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void AirborneEnemyReservesItsOriginAndCurrentColumnUntilDeath()
    {
        var go=new GameObject("Airborne Spawn Occupancy");
        var actor=new GameObject("Lifted Enemy");
        try
        {
            go.AddComponent<BoxCollider2D>(); var waves=go.AddComponent<RoomWaveManager>();
            var slot=new GameObject("Spawn_0"); slot.transform.SetParent(go.transform,false);
            waves.BindSpawnSlots(new[] {slot.transform});
            var enemy=actor.AddComponent<EnemyController>(); actor.transform.position=new Vector3(5,6,0);
            var active=(List<EnemyController>)typeof(RoomWaveManager).GetField("activeEnemies",Private).GetValue(waves);
            active.Add(enemy);
            var origins=(Dictionary<EnemyController,Vector3>)typeof(RoomWaveManager).GetField("spawnOrigins",Private).GetValue(waves);
            origins[enemy]=Vector3.zero;
            var collect=typeof(RoomWaveManager).GetMethod("CollectLivingEnemyPositions",Private);
            collect.Invoke(waves,null);
            Assert.That(Resolve(waves,slot.transform),Is.False,"The original spawn must remain reserved after flight/launch");
            slot.transform.position=new Vector3(5,0,0);
            Assert.That(Resolve(waves,slot.transform),Is.False,"Height alone must not free the current spawn column");
            typeof(EnemyController).GetField("isAlive",Private).SetValue(enemy,false);
            collect.Invoke(waves,null);
            Assert.That(Resolve(waves,slot.transform),Is.True);
            active.Clear();
        }
        finally
        {
            var waves=go.GetComponent<RoomWaveManager>();
            if (waves!=null) ((List<EnemyController>)typeof(RoomWaveManager).GetField("activeEnemies",Private).GetValue(waves)).Clear();
            Object.DestroyImmediate(go); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void PressureRetainsQuotaWhenSpawnMarkersAreMissing()
    {
        var go=new GameObject("Missing Spawn Markers");
        var prefab=new GameObject("Pending Enemy");
        try
        {
            go.AddComponent<BoxCollider2D>(); var waves=go.AddComponent<RoomWaveManager>();
            prefab.AddComponent<EnemyController>();
            var director=new EncounterPressureDirector(3,new[] {prefab});
            typeof(RoomWaveManager).GetField("pressureDirector",Private).SetValue(waves,director);
            typeof(RoomWaveManager).GetField("runtimeSpawnInterval",Private).SetValue(waves,0f);
            typeof(RoomWaveManager).GetField("roomCleared",Private).SetValue(waves,true);
            string warning="[RoomWaveManager] No spawn point for 'Pending Enemy'.";
            LogAssert.Expect(LogType.Warning,warning); LogAssert.Expect(LogType.Warning,warning);
            var routine=(IEnumerator)typeof(RoomWaveManager).GetMethod("PressureRoutine",Private).Invoke(waves,null);
            Assert.That(routine.MoveNext(),Is.True,"Retry must yield instead of clearing the room or spinning");
            Assert.That(director.Remaining,Is.EqualTo(1));
            (routine as IDisposable)?.Dispose();
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(prefab); }
    }

    [UnityTest]
    public IEnumerator PressureBurstAndLegacyWaitForSpaceWithoutLosingTheSecondFlyer()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (string mode in new[] {"Pressure","Burst","Batched","Legacy"})
        {
            var fixture=new GameObject(mode+" Spawn Retry Test");
            EncounterSO encounter=ScriptableObject.CreateInstance<EncounterSO>();
            try
            {
                fixture.AddComponent<AudioListener>();
                var floor=new GameObject("Floor"); floor.transform.SetParent(fixture.transform,false);
                floor.layer=LayerMask.NameToLayer("Ground"); floor.transform.position=new Vector3(1000,-.5f,0);
                floor.AddComponent<BoxCollider2D>().size=new Vector2(20,1);
                var host=new GameObject("Spawner"); host.transform.SetParent(fixture.transform,false); host.transform.position=new Vector3(1000,0,0);
                host.AddComponent<BoxCollider2D>().isTrigger=true;
                var slot=new GameObject("Spawn_0"); slot.transform.SetParent(host.transform,false);
                var waves=host.AddComponent<RoomWaveManager>(); waves.BindSpawnSlots(new[] {slot.transform});
                typeof(RoomWaveManager).GetField("roomCleared",Private).SetValue(waves,true);
                var active=(List<EnemyController>)typeof(RoomWaveManager).GetField("activeEnemies",Private).GetValue(waves);
                var flyer=CombatRouteContent.LoadPalette(RouteDifficulty.Easy).CollectPalettePrefabs()
                    .First(p=>EncounterSpawnSlotPicker.PrefersFarSlot(p));
                using (var data=new SerializedObject(encounter))
                {
                    data.FindProperty("spawnMode").enumValueIndex=(int)(mode=="Pressure" ? EncounterSpawnMode.Pressure
                        : mode=="Batched" ? EncounterSpawnMode.Batched : EncounterSpawnMode.Burst);
                    data.FindProperty("aliveCap").intValue=3; data.FindProperty("spawnInterval").floatValue=0;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                Physics2D.SyncTransforms();
                if (mode=="Legacy")
                {
                    Type waveType=typeof(RoomWaveManager).GetNestedType("Wave",BindingFlags.NonPublic);
                    Type entryType=typeof(RoomWaveManager).GetNestedType("SpawnEntry",BindingFlags.NonPublic);
                    object wave=Activator.CreateInstance(waveType,true);
                    var entries=(IList)waveType.GetField("enemies").GetValue(wave);
                    for (int i=0;i<2;i++)
                    {
                        object entry=Activator.CreateInstance(entryType,true);
                        entryType.GetField("enemyPrefab").SetValue(entry,flyer);
                        entryType.GetField("spawnPoint").SetValue(entry,slot.transform); entries.Add(entry);
                    }
                    waves.StartCoroutine((IEnumerator)typeof(RoomWaveManager).GetMethod("SpawnWave",Private).Invoke(waves,new[] {wave}));
                }
                else waves.StartChamber(new[] {flyer,flyer},encounter);
                var first=active.Single(e=>e!=null && e.IsAlive);
                float deadline=Time.realtimeSinceStartup+3f;
                while (first.LifecycleState==EnemyLifecycleStateId.Spawn && Time.realtimeSinceStartup<deadline) yield return null;
                // Model a flyer lifted above the clearance radius without modifying its shared prefab.
                first.GetComponent<EnemyLocomotion>().enabled=false;
                first.rb.simulated=false;
                first.transform.position=new Vector3(1000,5,0); first.rb.position=first.transform.position;
                Physics2D.SyncTransforms();
                deadline=Time.realtimeSinceStartup+.25f;
                while (Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(active.Count(e=>e!=null && e.IsAlive),Is.EqualTo(1),mode+" must not stack two flyers in one slot");
                Assert.That(first.transform.position.y,Is.GreaterThan(1f),"Exercise a flyer that moved away from its ground marker");
                if (mode=="Pressure")
                    Assert.That(((EncounterPressureDirector)typeof(RoomWaveManager).GetField("pressureDirector",Private).GetValue(waves)).Remaining,Is.EqualTo(1));
                first.Die();
                deadline=Time.realtimeSinceStartup+3f;
                while (!active.Any(e=>e!=null && e.IsAlive && e!=first) && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(active.Count(e=>e!=null && e.IsAlive),Is.EqualTo(1),mode+" lost its blocked spawn quota");
                Assert.That(active.Single(e=>e!=null && e.IsAlive),Is.Not.SameAs(first));
            }
            finally
            {
                Object.DestroyImmediate(fixture); Object.DestroyImmediate(encounter);
            }
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeOnFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
