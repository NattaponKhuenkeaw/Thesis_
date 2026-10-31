#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine;
using Object=UnityEngine.Object;

public class LevelDesignSpawnTests
{
    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void EntrySensorsCoverAirborneCrossingsWithoutMovingTheAuthoredFloorOrSpawnSlots(int level)
    {
        var root=PrefabUtility.LoadPrefabContents($"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab");
        try
        {
            var stage=root.GetComponentInChildren<CombatStageController>();
            var first=root.GetComponentInChildren<RoomWaveManager>().GetComponent<BoxCollider2D>();
            var ceiling=root.transform.Find("Boundaries/Ceiling").GetComponent<BoxCollider2D>();
            var plan=LevelDesignLayoutBuilder.GetPlan(level);
            var rooms=LevelDesignSpawnBuilder.Rooms(level);
            Physics2D.SyncTransforms();
            for (int i=0;i<stage.ChamberCount;i++)
            {
                var sensor=i==0 ? first : (BoxCollider2D)stage.Chambers[i].enterTrigger;
                Vector2 entry=LevelDesignLayoutBuilder.ToWorld(plan,rooms[i].entry);
                if ((level==4 || level==9) && (i==3 || i==4))
                    entry.x=LayoutRoadAuthoring.WorldRect((BoxCollider2D)stage.Chambers[i].entranceWall).xMax+1f;
                Assert.That(sensor.isTrigger,Is.True);
                Assert.That(sensor.size.x,Is.EqualTo(1.6f));
                Assert.That(sensor.bounds.min.y,Is.EqualTo(entry.y).Within(.001f));
                Assert.That(sensor.bounds.max.y,Is.EqualTo(ceiling.bounds.min.y).Within(.001f));
                Assert.That(sensor.transform.position.x,Is.EqualTo(entry.x).Within(.001f));
                Assert.That(sensor.OverlapPoint(entry+Vector2.up*7f),Is.True,$"LV{level} Room{i+1} airborne entry");
                Assert.That(sensor.OverlapPoint(entry-Vector2.up*.1f),Is.False,"Do not extend the trigger into the lower route.");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void StageExitIsFlushWithTheMapEdgeAndReachableBeforeTheWall(int level)
    {
        var root=PrefabUtility.LoadPrefabContents($"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab");
        try
        {
            var plan=LevelDesignLayoutBuilder.GetPlan(level);
            var exit=root.transform.Find("StageExitTrigger").GetComponent<BoxCollider2D>();
            var wall=root.transform.Find("Boundaries/StageRightWall").GetComponent<BoxCollider2D>();
            var edge=LevelDesignLayoutBuilder.ToWorld(plan,plan.exit);
            Physics2D.SyncTransforms();
            Assert.That(exit.bounds.max.x,Is.EqualTo(wall.bounds.min.x).Within(.001f));
            Assert.That(exit.bounds.max.x,Is.EqualTo(plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r).xMax).Max()).Within(.001f));
            Assert.That(root.transform.Find("ExitGuide").position.x,Is.EqualTo(edge.x).Within(.001f));
            Assert.That(exit.bounds.min.y,Is.EqualTo(edge.y).Within(.001f));
            Assert.That(exit.isTrigger,Is.True);
            Assert.That(exit.size,Is.EqualTo(new Vector2(1.8f,2.8f)));
            Assert.That(wall.size.x,Is.GreaterThanOrEqualTo(6f));
            var boundary=root.GetComponentInChildren<SceneBoundaryController>();
            using (var data=new SerializedObject(boundary))
                Assert.That(data.FindProperty("_exitTrigger").objectReferenceValue,Is.SameAs(exit.GetComponent<TransitionTrigger>()));
            var stage=root.GetComponentInChildren<CombatStageController>();
            Assert.That(stage.Chambers.Last().exitWall,Is.SameAs(wall));

            // A player's body stopped by the wall still overlaps the exit, on supported floor.
            float bodyLeft=wall.bounds.min.x-1.55f-.02f;
            var support=root.transform.Find("Terrain").GetComponentsInChildren<BoxCollider2D>().Select(LayoutRoadAuthoring.WorldRect)
                .Where(r=>bodyLeft>=r.xMin && bodyLeft+1.55f<=r.xMax && r.yMax<=edge.y+.12f).OrderByDescending(r=>r.yMax).First();
            var body=new Rect(bodyLeft,support.yMax+.01f,1.55f,2.6f);
            var zone=new Rect(exit.bounds.min,exit.bounds.size);
            Assert.That(zone.Overlaps(body),Is.True);
            Assert.That(support.xMin,Is.LessThanOrEqualTo(body.xMin));
            Assert.That(support.xMax,Is.GreaterThanOrEqualTo(body.xMax));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void ProductionCombatCatalogUsesNewLayoutWithoutDuplicatingDraftOffers(int level)
    {
        var catalog=Resources.Load<MapSO>("MapSO/Test_MapSO");
        var room=catalog.GetRoomById($"Combat_{level:00}");
        var draft=Resources.Load<RoomData>($"MapSO/LV/RoomData_LV_{level:00}");
        var layout=Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}");
        Assert.That(room,Is.Not.Null);
        Assert.That(layout,Is.Not.Null);
        Assert.That(room.LayoutPrefab,Is.SameAs(layout));
        Assert.That(room.SceneName,Is.EqualTo("combat_1"));
        Assert.That(room.IncludeInRandomOffers,Is.True);
        Assert.That(room.DefaultEntranceId,Is.EqualTo("Default"));
        Assert.That(CombatRouteContent.ShouldAttachGeneratedWaves(room),Is.False);
        Assert.That(layout.GetComponentsInChildren<EntrancePoint>(true)
            .Any(p=>p.EntranceId==room.DefaultEntranceId),Is.True);
        var candidates=new System.Collections.Generic.List<RoomData>();
        catalog.CollectOfferCandidates(RoomKind.Combat,null,candidates);
        CollectionAssert.Contains(candidates,room);
        CollectionAssert.DoesNotContain(candidates,draft);
        Assert.That(draft.IncludeInRandomOffers,Is.False);
    }

    [UnityTest]
    public IEnumerator AuthoredTriggersSpawnEnemiesAndAdvanceThroughEveryLayout()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var current=typeof(CombatStageController).GetField("currentIndex",flags);
        var waiting=typeof(CombatStageController).GetField("waitingForIndex",flags);
        for (int level=1;level<=10;level++)
        {
            GameObject root=Object.Instantiate(Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}"));
            var player=new GameObject("LV Trigger Test Player");
            try
            {
                var stage=root.GetComponentInChildren<CombatStageController>();
                var boundary=root.GetComponentInChildren<SceneBoundaryController>();
                player.AddComponent<AudioListener>();
                player.tag="Player"; player.layer=LayerMask.NameToLayer("Player");
                var body=player.AddComponent<Rigidbody2D>(); body.bodyType=RigidbodyType2D.Kinematic;
                var box=player.AddComponent<BoxCollider2D>(); box.size=new Vector2(1.55f,2.6f);
                var start=root.GetComponentInChildren<RoomWaveManager>().GetComponent<Collider2D>();
                body.position=new Vector2(start.bounds.center.x,start.bounds.min.y+7f);
                for (int room=0;room<stage.ChamberCount;room++)
                {
                    float deadline=Time.realtimeSinceStartup+5f;
                    while ((int)current.GetValue(stage)!=room && Time.realtimeSinceStartup<deadline) yield return null;
                    Assert.That(current.GetValue(stage),Is.EqualTo(room),$"LV{level} Room{room+1} trigger did not start");
                    Assert.That(stage.Chambers[room].exitWall.isTrigger,Is.False);
                    Assert.That(boundary.IsExitAvailable,Is.False,"The stage exit must stay locked before the final clear");
                    Assert.That(Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Any(e=>e.IsAlive),Is.True);
                    // Do not finish the stage: that would run rewards and progression outside this isolated fixture.
                    if (room==stage.ChamberCount-1) break;
                    int defeated=0;
                    deadline=Time.realtimeSinceStartup+5f;
                    while ((int)waiting.GetValue(stage)!=room+1 && Time.realtimeSinceStartup<deadline)
                    {
                        foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                            if (enemy.IsAlive) { enemy.Die(); defeated++; }
                        yield return null;
                    }
                    Assert.That(defeated,Is.EqualTo(8),$"LV{level} Room{room+1} must exhaust its difficulty quota");
                    Assert.That(waiting.GetValue(stage),Is.EqualTo(room+1));
                    Assert.That(stage.Chambers[room].exitWall.isTrigger,Is.True);
                    var next=stage.Chambers[room+1].enterTrigger.bounds;
                    body.position=new Vector2(next.center.x,next.min.y+7f);
                    Physics2D.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                }
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(player); }
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterSmokeTestFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void EveryRoomHasAtLeastFiveSafeSlotsAndWiredProgression(int level)
    {
        var root=PrefabUtility.LoadPrefabContents($"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab");
        try
        {
            var plan=LevelDesignLayoutBuilder.GetPlan(level);
            var expected=LevelDesignSpawnBuilder.Rooms(level);
            var stage=root.GetComponentInChildren<CombatStageController>();
            Assert.That(stage,Is.Not.Null);
            Assert.That(stage.ChamberCount,Is.EqualTo(expected.Length));
            Assert.That(root.GetComponentsInChildren<RoomWaveManager>().Length,Is.EqualTo(1));
            Assert.That(root.GetComponentInChildren<RoomWaveManager>().GetComponent<Collider2D>().isTrigger,Is.True);
            Assert.That(root.GetComponentInChildren<TransitionTrigger>(),Is.Not.Null);
            for (int i=0;i<expected.Length;i++)
            {
                var room=stage.Chambers[i];
                Assert.That(room.spawnSlots.Length,Is.GreaterThanOrEqualTo(5));
                bool repairedCombat=level==4 || level==9;
                Assert.That(room.spawnSlots.Length,Is.EqualTo(repairedCombat ? 5 : expected[i].slots.Length));
                Assert.That(room.encounter,Is.EqualTo(AssetDatabase.LoadAssetAtPath<EncounterSO>(CombatRouteContent.EasyResourcePath.Insert(0,"Assets/Project/Resources/")+".asset")));
                Assert.That(room.encounter.RollQuota(room.encounter.ChamberQuota).Count,Is.EqualTo(8));
                Assert.That(room.exitWall,Is.Not.Null);
                Assert.That(((BoxCollider2D)room.exitWall).size.x,Is.GreaterThanOrEqualTo(6f));
                if (i>0)
                {
                    Assert.That(room.entranceWall,Is.EqualTo(stage.Chambers[i-1].exitWall));
                    Assert.That(room.enterTrigger,Is.Not.Null);
                    Assert.That(room.enterTrigger.isTrigger,Is.True);
                }
                for (int n=0;n<room.spawnSlots.Length;n++)
                {
                    Transform slot=room.spawnSlots[n];
                    Assert.That(slot.name,Is.EqualTo($"Spawn_{n}"));
                    Assert.That(slot.IsChildOf(room.spawnRoot),Is.True);
                    Vector2 feet=slot.position;
                    if (!repairedCombat)
                        Assert.That(Vector2.Distance(feet,LevelDesignSpawnBuilder.SpawnPosition(level,i,n)),Is.LessThan(.001f));
                    Rect body=new Rect(feet+new Vector2(-.9f,.01f),new Vector2(1.8f,2.7f));
                    bool supported=false;
                    var solids=repairedCombat
                        ? root.transform.Find("Terrain").GetComponentsInChildren<BoxCollider2D>().Where(b=>b.enabled).Select(LayoutRoadAuthoring.WorldRect)
                        : plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r));
                    foreach (Rect solid in solids)
                    {
                        Assert.That(solid.Overlaps(body),Is.False,$"LV{level} Room{i+1} {slot.name} is inside terrain");
                        if (Mathf.Abs(feet.y-solid.yMax-.05f)<.001f && feet.x>=solid.xMin+.9f && feet.x<=solid.xMax-.9f) supported=true;
                    }
                    Assert.That(supported,Is.True,$"LV{level} Room{i+1} {slot.name} needs a supporting floor");
                    foreach (Collider2D wall in new[] { room.exitWall }.Concat(room.additionalExitWalls ?? new Collider2D[0]))
                    {
                        var box=(BoxCollider2D)wall;
                        Rect bounds=new Rect((Vector2)box.transform.position-box.size*.5f,box.size);
                        Assert.That(bounds.Overlaps(body),Is.False,$"LV{level} Room{i+1} {slot.name} intersects a locked gate");
                    }
                }
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void SharedBypassBarriersStayClosedBetweenRoomsAndOpenAfterTheirLastOwner()
    {
        var go=new GameObject("Stage Release Test");
        var first=new GameObject("Lower Gate"); var second=new GameObject("Upper Gate");
        try
        {
            var stage=go.AddComponent<CombatStageController>();
            var lower=first.AddComponent<BoxCollider2D>(); var upper=second.AddComponent<BoxCollider2D>();
            var chambers=new[] {
                new CombatStageController.Chamber { additionalExitWalls=new Collider2D[] {lower,upper} },
                new CombatStageController.Chamber { exitWall=lower,additionalExitWalls=new Collider2D[] {upper} },
                new CombatStageController.Chamber()
            };
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(CombatStageController).GetField("chambers",flags).SetValue(stage,chambers);
            var index=typeof(CombatStageController).GetField("currentIndex",flags);
            var release=typeof(CombatStageController).GetMethod("ReleaseAdditionalExitWalls",flags);
            index.SetValue(stage,0); release.Invoke(stage,null);
            Assert.That(lower.enabled,Is.True); Assert.That(upper.enabled,Is.True);
            index.SetValue(stage,1); release.Invoke(stage,null);
            Assert.That(upper.enabled,Is.False);
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
    }

    [Test]
    public void FinalEncounterSegmentReleasesAdditionalBarriers()
    {
        var go=new GameObject("Final Chamber Release Test");
        var barrier=new GameObject("Final Additional Gate");
        try
        {
            var stage=go.AddComponent<CombatStageController>();
            go.AddComponent<BoxCollider2D>();
            var waves=go.AddComponent<RoomWaveManager>();
            var wall=barrier.AddComponent<BoxCollider2D>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(CombatStageController).GetField("chambers",flags).SetValue(stage,new[] {
                new CombatStageController.Chamber { additionalExitWalls=new Collider2D[] {wall} }
            });
            typeof(CombatStageController).GetField("currentIndex",flags).SetValue(stage,0);
            typeof(RoomWaveManager).GetField("stageController",flags).SetValue(waves,stage);
            // Isolate gate release from rewards, route offers and save/progression side effects.
            typeof(RoomWaveManager).GetField("roomCleared",flags).SetValue(waves,true);
            typeof(RoomWaveManager).GetMethod("FinishCurrentEncounterSegment",flags).Invoke(waves,null);
            Assert.That(wall.enabled,Is.False);
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(barrier); }
    }

    [Test]
    public void DifficultyStillControlsEnemyQuotaRatherThanTheNumberOfMarkers()
    {
        var easy=CombatRouteContent.LoadPalette(RouteDifficulty.Easy);
        var medium=CombatRouteContent.LoadPalette(RouteDifficulty.Medium);
        var hard=CombatRouteContent.LoadPalette(RouteDifficulty.Hard);
        Assert.That(easy.ChamberQuota,Is.EqualTo(8));
        Assert.That(medium.ChamberQuota,Is.EqualTo(12));
        Assert.That(hard.ChamberQuota,Is.EqualTo(16));
    }
}
#endif
