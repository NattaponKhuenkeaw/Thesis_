using System;
using System.Linq;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;

public static class SafeRoomFloorAuthoring
{
    public const string BiomeOneGroup = "FloorArtwork_B1";

    [MenuItem("Tools/Project/Level Design/Use Biome Asset Floors In Rest And Level")]
    public static void ApplyAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        foreach (string kind in new[] { "Rest", "Level" })
        {
            string path = SafeRoomBiomeAuthoring.LayoutPath(kind);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
                var behaviors = root.GetComponentsInChildren<MonoBehaviour>(true).Where(b => !(b is RoomBiomeEnvironment)).ToArray();
                string[] bindings = behaviors.Select(b => EditorJsonUtility.ToJson(b)).ToArray();
                Apply(root, kind);
                if (physics != BiomeLayoutAuthoring.PhysicsSnapshot(root)
                    || !bindings.SequenceEqual(behaviors.Select(b => EditorJsonUtility.ToJson(b))))
                    throw new InvalidOperationException("Floor art changed room collision or interaction bindings: " + kind);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        SafeRoomBiomeAuthoring.CaptureAll();
        Debug.Log("[Safe Room Floors] Rest and Level use biome road/reflection assets. Procedural concrete material removed from these rooms; collision and displays preserved.");
    }

    public static void Apply(GameObject root, string kind)
    {
        var ground = root.GetComponentsInChildren<BoxCollider2D>(true)
            .Where(b => b.name == "Ground" && !b.isTrigger)
            .OrderByDescending(b => LayoutRoadAuthoring.WorldRect(b).width).First();
        var bounds = LayoutRoadAuthoring.WorldRect(ground);
        foreach (var image in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (image.sharedMaterial != null && image.sharedMaterial.shader != null
                && image.sharedMaterial.shader.name == "Project/CityConcreteGround")
            {
                image.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
                image.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            }
        var firstTheme = RoomBackgroundAuthoring.BiomeOneRoot(root);
        var group = firstTheme.Find(BiomeOneGroup);
        if (group == null)
        {
            group = new GameObject(BiomeOneGroup).transform;
            group.SetParent(firstTheme, false);
        }
        SetFloor(GetOrCreate(group,"Road_B1"),GetOrCreate(group,"StreetReflection_B1"),
            LayoutRoadAuthoring.RoadSprite(1),AssetDatabase.LoadAssetAtPath<Sprite>(LevelDesignBuildingDress.ReflectionSource),bounds);

        SafeRoomBiomeAuthoring.RefreshBindings(root,kind);
        var environment = root.GetComponentInChildren<RoomBiomeEnvironment>(true).transform.Find(kind+"Environment_B2");
        SetFloor(environment.Find("Road_B2").GetComponent<SpriteRenderer>(),environment.Find("StreetReflection_B2").GetComponent<SpriteRenderer>(),
            LayoutRoadAuthoring.RoadSprite(6),BiomeLayoutAuthoring.Load("B2_Parallax_Ground"),bounds);
        BiomeMaterialAuthoring.ApplyIfPrepared(root);
    }

    private static SpriteRenderer GetOrCreate(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(parent,false);
            return child.gameObject.AddComponent<SpriteRenderer>();
        }
        return child.GetComponent<SpriteRenderer>();
    }

    private static void SetFloor(SpriteRenderer road, SpriteRenderer reflection, Sprite roadSprite, Sprite reflectionSprite, Rect bounds)
    {
        if (roadSprite == null || reflectionSprite == null) throw new InvalidOperationException("Missing biome floor sprite.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        road.sprite = roadSprite; road.sharedMaterial = material; road.enabled = true;
        road.drawMode = SpriteDrawMode.Tiled; road.tileMode = SpriteTileMode.Continuous;
        road.transform.localScale = Vector3.one;
        float top = LayoutRoadAuthoring.OpaqueTop(roadSprite);
        road.size = new Vector2(bounds.width/Mathf.Abs(road.transform.lossyScale.x),roadSprite.bounds.size.y);
        road.transform.position = new Vector3(bounds.center.x,
            bounds.yMax + (roadSprite.pivot.y-top)/roadSprite.pixelsPerUnit*Mathf.Abs(road.transform.lossyScale.y),0f);
        road.sortingOrder = 18;
        float opaqueHeight = BiomeLayoutAuthoring.OpaqueBounds(AssetDatabase.GetAssetPath(roadSprite)).height;
        float bottom = bounds.yMax-opaqueHeight/roadSprite.pixelsPerUnit*Mathf.Abs(road.transform.lossyScale.y);
        reflection.sprite = reflectionSprite; reflection.sharedMaterial = material; reflection.enabled = true;
        reflection.drawMode = SpriteDrawMode.Tiled; reflection.tileMode = SpriteTileMode.Continuous;
        reflection.transform.localScale = Vector3.one*1.4f;
        reflection.size = new Vector2(bounds.width/Mathf.Abs(reflection.transform.lossyScale.x),reflectionSprite.bounds.size.y);
        reflection.transform.position = new Vector3(bounds.center.x,bottom-reflectionSprite.bounds.max.y*Mathf.Abs(reflection.transform.lossyScale.y),0f);
        reflection.sortingOrder = 17;
        foreach (var image in new[] { road,reflection })
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image.transform);
        }
    }
}
