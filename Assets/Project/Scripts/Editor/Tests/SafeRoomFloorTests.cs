#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;

public class SafeRoomFloorTests
{
    [TestCase("Rest")] [TestCase("Level")]
    public void RoomsNoLongerReferenceProceduralConcreteEvenOnHiddenRenderers(string kind)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SafeRoomBiomeAuthoring.LayoutPath(kind));
        foreach (var image in prefab.GetComponentsInChildren<SpriteRenderer>(true))
            Assert.That(image.sharedMaterial == null || image.sharedMaterial.shader.name != "Project/CityConcreteGround", Is.True, image.name);
        Assert.That(prefab.transform.Find("EnvironmentDependency/Ground").GetComponent<SpriteRenderer>().enabled, Is.False);
    }

    [TestCase("Rest",0)] [TestCase("Rest",1)] [TestCase("Rest",2)] [TestCase("Rest",3)]
    [TestCase("Level",0)] [TestCase("Level",1)] [TestCase("Level",2)] [TestCase("Level",3)]
    public void BiomeFloorsSwitchTogetherAndTheirOpaqueTopMatchesTheActualCollider(string kind,int round)
    {
        var root = PrefabUtility.LoadPrefabContents(SafeRoomBiomeAuthoring.LayoutPath(kind));
        try
        {
            var environment = root.GetComponentInChildren<RoomBiomeEnvironment>(true);
            environment.Apply(Resources.Load<MapSO>("MapSO/Test_MapSO"),round);
            int biome = round%2;
            var b1 = RoomBackgroundAuthoring.BiomeOneRoot(root).Find(SafeRoomFloorAuthoring.BiomeOneGroup);
            var b2 = environment.transform.Find(kind+"Environment_B2");
            Assert.That(b1,Is.Not.Null);
            var first = b1.Find("Road_B1").GetComponent<SpriteRenderer>();
            var second = b2.Find("Road_B2").GetComponent<SpriteRenderer>();
            Assert.That(first.enabled && first.gameObject.activeInHierarchy,Is.EqualTo(biome==0));
            Assert.That(second.enabled && second.gameObject.activeInHierarchy,Is.EqualTo(biome==1));
            var road = biome==0 ? first : second;
            var reflection = (biome==0 ? b1 : b2).Find(biome==0 ? "StreetReflection_B1" : "StreetReflection_B2").GetComponent<SpriteRenderer>();
            var ground = LayoutRoadAuthoring.WorldRect(root.transform.Find("EnvironmentDependency/Ground").GetComponent<BoxCollider2D>());
            Assert.That(AssetDatabase.GetAssetPath(road.sprite),Is.EqualTo(biome==0 ? LayoutRoadAuthoring.BiomeOneRoad : LayoutRoadAuthoring.BiomeTwoRoad));
            Assert.That(AssetDatabase.GetAssetPath(reflection.sprite),Is.EqualTo(biome==0 ? LevelDesignBuildingDress.ReflectionSource : BiomeLayoutAuthoring.B2Art+"B2_Parallax_Ground.PNG"));
            float top = road.transform.position.y + (LayoutRoadAuthoring.OpaqueTop(road.sprite)-road.sprite.pivot.y)/road.sprite.pixelsPerUnit*Mathf.Abs(road.transform.lossyScale.y);
            Assert.That(top,Is.EqualTo(ground.yMax).Within(.001f));
            Assert.That(road.bounds.size.x,Is.EqualTo(ground.width).Within(.001f));
            Assert.That(reflection.bounds.size.x,Is.EqualTo(ground.width).Within(.001f));
            Assert.That(road.size.y,Is.EqualTo(road.sprite.bounds.size.y));
            Assert.That(reflection.size.y,Is.EqualTo(reflection.sprite.bounds.size.y));
            float height = BiomeLayoutAuthoring.OpaqueBounds(AssetDatabase.GetAssetPath(road.sprite)).height/road.sprite.pixelsPerUnit*Mathf.Abs(road.transform.lossyScale.y);
            Assert.That(reflection.bounds.max.y,Is.EqualTo(ground.yMax-height).Within(.001f));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("Rest")] [TestCase("Level")]
    public void ReauthoringFloorArtworkIsIdempotentAndPreservesPhysicsAndDisplays(string kind)
    {
        var root = PrefabUtility.LoadPrefabContents(SafeRoomBiomeAuthoring.LayoutPath(kind));
        try
        {
            string collision = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var behaviors = root.GetComponentsInChildren<MonoBehaviour>(true).Where(b=>!(b is RoomBiomeEnvironment)).ToArray();
            string[] bindings = behaviors.Select(b=>EditorJsonUtility.ToJson(b)).ToArray();
            SafeRoomFloorAuthoring.Apply(root,kind);
            SafeRoomFloorAuthoring.Apply(root,kind);
            Assert.That(RoomBackgroundAuthoring.BiomeOneRoot(root).Find(SafeRoomFloorAuthoring.BiomeOneGroup).GetComponentsInChildren<SpriteRenderer>(true).Length,Is.EqualTo(2));
            Assert.That(RoomBackgroundAuthoring.BiomeOneRoot(root).Find(SafeRoomFloorAuthoring.BiomeOneGroup).GetComponentsInChildren<Collider2D>(true),Is.Empty);
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root),Is.EqualTo(collision));
            Assert.That(behaviors.Select(b=>EditorJsonUtility.ToJson(b)),Is.EqualTo(bindings));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
