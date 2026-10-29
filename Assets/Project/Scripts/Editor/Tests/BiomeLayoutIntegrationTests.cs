#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.Parallax;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public class BiomeLayoutIntegrationTests
{
    [TestCase(0,0)] [TestCase(1,1)] [TestCase(2,0)] [TestCase(3,1)] [TestCase(4,0)] [TestCase(5,1)]
    public void CatalogOffersOnlyFiveLayoutsOfTheActiveAuthoredBiome(int current,int expected)
    {
        var map=Resources.Load<MapSO>("MapSO/Test_MapSO");
        var candidates=new List<RoomData>();
        map.CollectOfferCandidates(RoomKind.Combat,null,candidates,current);
        Assert.That(candidates.Count,Is.EqualTo(5));
        Assert.That(candidates.Select(r=>r.RoomId).Distinct().Count(),Is.EqualTo(5));
        Assert.That(candidates.All(r=>r.BiomeIndex==expected),Is.True);
        string exclude=candidates[0].RoomId;
        map.CollectOfferCandidates(RoomKind.Combat,exclude,candidates,current);
        Assert.That(candidates.Count,Is.EqualTo(4));
        Assert.That(candidates.Any(r=>r.RoomId==exclude),Is.False);
        map.CollectOfferCandidates(RoomKind.Rest,null,candidates,current);
        Assert.That(candidates.Count,Is.GreaterThan(0),"Shared safe rooms must remain available in both biomes.");
    }

    [Test]
    public void RepeatedBossClearsAlternateThemesWithoutResettingDifficulty()
    {
        var root=new GameObject("Biome route test");
        var previous=RunProgressionManager.Instance;
        var instance=typeof(RunProgressionManager).GetField("<Instance>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
        var random=Random.state;
        try
        {
            instance.SetValue(null,null);
            var progression=root.AddComponent<RunProgressionManager>();
            instance.SetValue(null,progression);
            var director=root.AddComponent<RoomChoiceDirector>();
            for (int biome=0;biome<6;biome++)
            {
                Assert.That(progression.CurrentBiomeIndex,Is.EqualTo(biome));
                Assert.That(progression.EnemyBasicStatMultiplier,Is.EqualTo(1f+biome*.2f).Within(.0001f));
                for (int n=0;n<40;n++)
                {
                    var offers=director.GenerateOffers(null,false);
                    Assert.That(offers.Count,Is.EqualTo(3));
                    Assert.That(offers.All(o=>o.Room.RoomKind==RoomKind.Combat && o.Room.BiomeIndex==biome%2),Is.True);
                }
                var postBoss=director.GenerateOffersAfterClear("Boss_01");
                Assert.That(postBoss.Any(o=>o.Room.RoomKind==RoomKind.Rest),Is.True);
                Assert.That(postBoss.Where(o=>o.Room.RoomKind==RoomKind.Combat)
                    .All(o=>o.Room.BiomeIndex==(biome+1)%2),Is.True);
            }
            Assert.That(progression.CurrentBiomeIndex,Is.EqualTo(6));
        }
        finally { Object.DestroyImmediate(root); instance.SetValue(null,previous); Random.state=random; }
    }

    [TestCase(0,0)] [TestCase(1,1)] [TestCase(2,0)] [TestCase(3,1)] [TestCase(4,0)] [TestCase(5,1)]
    public void DirectNextRoomHelperAlsoRespectsTheActiveBiome(int current,int expected)
    {
        var root=new GameObject("Direct biome route test");
        var previous=RunProgressionManager.Instance;
        var instance=typeof(RunProgressionManager).GetField("<Instance>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
        var random=Random.state;
        try
        {
            instance.SetValue(null,null);
            var progression=root.AddComponent<RunProgressionManager>();
            progression.currentBiomeIndex=current;
            var map=Resources.Load<MapSO>("MapSO/Test_MapSO");
            for (int n=0;n<40;n++)
            {
                var room=map.GetRoomById(progression.GetNextRoomId());
                Assert.That(room,Is.Not.Null);
                Assert.That(room.BiomeIndex,Is.EqualTo(expected));
            }
        }
        finally { Object.DestroyImmediate(root); instance.SetValue(null,previous); Random.state=random; }
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void EveryLayoutHasDistinctDressingAndValidGameplayGeometry(int level)
    {
        var root=PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            var plan=LevelDesignLayoutBuilder.GetPlan(level);
            LevelDesignLayoutBuilder.ValidateGeometry(root,plan);
            var dressing=root.transform.Find(BiomeLayoutDressing.GroupName);
            Assert.That(dressing,Is.Not.Null);
            Assert.That(dressing.childCount,Is.EqualTo(1));
            Assert.That(dressing.GetComponentsInChildren<SpriteRenderer>().Length,Is.GreaterThan(10));
            Assert.That(dressing.GetComponentsInChildren<Collider2D>(true),Is.Empty);
            foreach (var image in root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.sprite!=null || r.maskInteraction==SpriteMaskInteraction.VisibleInsideMask))
            {
                Assert.That(image.sprite,Is.Not.Null,image.name);
                Assert.That(image.sharedMaterial,Is.Not.Null,image.name);
            }
            foreach (var image in root.transform.Find("OneWayPlatforms").GetComponentsInChildren<SpriteRenderer>().Where(r=>r.GetComponent<PlatformEffector2D>()!=null))
            {
                Assert.That(AssetDatabase.GetAssetPath(image.sprite),Does.StartWith("Assets/Project/Art/BG/"),"No white placeholder platform artwork.");
                Assert.That(image.sharedMaterial,Is.EqualTo(AssetDatabase.LoadAssetAtPath<Material>(
                    BiomeMaterialAuthoring.MaterialPath(level<=5 ? 1 : 2,"Platform"))));
            }
            if (level<=5)
                Assert.That(dressing.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name.StartsWith("FacadeWindow_")),Is.True);
            var room=Resources.Load<MapSO>("MapSO/Test_MapSO").GetRoomById($"Combat_{level:00}");
            Assert.That(room.LayoutPrefab,Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level))));
            Assert.That(room.BiomeIndex,Is.EqualTo((level-1)/5));
            Assert.That(room.SceneName,Is.EqualTo("combat_1"));
            if (level>5)
            {
                Assert.That(root.transform.Find("CombatBackground_B1"),Is.Null);
                Assert.That(root.transform.Find(B1ForegroundDressing.GroupName),Is.Null);
                Assert.That(root.transform.Find("CombatBackground_B2"),Is.Not.Null);
                foreach (var layer in root.GetComponentsInChildren<ParallaxBackground>())
                    Assert.That(AssetDatabase.GetAssetPath(layer.GetComponent<SpriteRenderer>().sprite),Does.StartWith(BiomeLayoutAuthoring.B2Art));
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void DistrictCompositionIsDifferentForAllTenLayouts()
    {
        var names=new HashSet<string>();
        var signatures=new HashSet<string>();
        for (int level=1;level<=10;level++)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
            var district=root.transform.Find(BiomeLayoutDressing.GroupName).GetChild(0);
            Assert.That(names.Add(district.name),Is.True);
            string signature=string.Join("|",district.GetComponentsInChildren<SpriteRenderer>()
                .Select(r=>r.sprite.name+":"+r.transform.localPosition+":"+r.flipX));
            Assert.That(signatures.Add(signature),Is.True,"Do not stamp an identical prop arrangement into another layout.");
        }
    }

    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void NewLayoutsHaveSafeSpawnSlotsAndFullHeightEntrySensors(int level)
    {
        var tests=new LevelDesignSpawnTests();
        tests.EntrySensorsCoverAirborneCrossingsWithoutMovingTheAuthoredFloorOrSpawnSlots(level);
        tests.EveryRoomHasAtLeastFiveSafeSlotsAndWiredProgression(level);
        tests.StageExitIsFlushWithTheMapEdgeAndReachableBeforeTheWall(level);
        tests.ProductionCombatCatalogUsesNewLayoutWithoutDuplicatingDraftOffers(level);
        new LevelDesignTraversalTests().FirstArenaFitsTheUnmodifiedCombatCameraRange(level);
        new LevelDesignLayoutTests().NativeBodySweepsHitEveryThickRoomBarrier(level);
    }

    [TestCase(5)] [TestCase(10)]
    public void RevisedFifthLayoutUsesFiveForwardOnlyEncounters(int level)
    {
        var root=AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        var stage=root.GetComponentInChildren<CombatStageController>();
        Assert.That(stage.ChamberCount,Is.EqualTo(5));
        Assert.That(stage.Chambers.Last().exitWall.name,Is.EqualTo("StageRightWall"));
        Assert.That(root.transform.Find("Boundaries/ChamberGates").childCount,Is.EqualTo(4));
        Assert.That(root.transform.Find("Boundaries/UpperExitSeal"),Is.Null);
        var rooms=LevelDesignSpawnBuilder.Rooms(level);
        Assert.That(rooms.Select(r=>r.entry.x),Is.Ordered.Ascending);
        var plan=LevelDesignLayoutBuilder.GetPlan(level);
        Assert.That(plan.solids.All(r=>Mathf.Abs(r.yMax-plan.baseline)<.01f || r==plan.solids[0]),Is.True);
    }

    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void NewBackgroundsCoverTheCameraAtBothEndsAndAtTheHighestRoof(int level)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level)));
        var host=new GameObject("Biome background camera");
        try
        {
            var plan=LevelDesignLayoutBuilder.GetPlan(level);
            float left=LevelDesignLayoutBuilder.WorldX(plan,108)+10f,right=LevelDesignLayoutBuilder.WorldX(plan,1812)-10f;
            float roof=plan.solids.Max(r=>LevelDesignLayoutBuilder.ToWorld(plan,r).yMax);
            host.transform.position=new Vector3(left,6f,-100f);
            var layers=root.GetComponentsInChildren<ParallaxBackground>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            foreach (var layer in layers)
            {
                layer.targetCamera=host.transform;
                typeof(ParallaxBackground).GetMethod("Start",flags).Invoke(layer,null);
            }
            foreach (var point in new[] { new Vector2(left,6f),new Vector2(right,roof+6f) })
            {
                host.transform.position=new Vector3(point.x,point.y,-100f);
                foreach (var layer in layers)
                {
                    typeof(ParallaxBackground).GetMethod("LateUpdate",flags).Invoke(layer,null);
                    var bounds=layer.GetComponent<SpriteRenderer>().bounds;
                    Assert.That(bounds.min.x,Is.LessThan(point.x-18f),layer.name+" left edge");
                    Assert.That(bounds.max.x,Is.GreaterThan(point.x+18f),layer.name+" right edge");
                }
                var sky=root.transform.Find("CombatBackground_B2/Sky").GetComponent<SpriteRenderer>().bounds;
                Assert.That(sky.min.y,Is.LessThan(point.y-9f));
                Assert.That(sky.max.y,Is.GreaterThan(point.y+9f));
            }
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(host); }
    }
}
#endif
