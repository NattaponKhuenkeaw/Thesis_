using System;
using System.Linq;
using Project.Core.ZoneState;
using Project.Parallax;
using UnityEditor;
using UnityEngine;

public static class SafeRoomBackgroundAuthoring
{
    public const string BackgroundName = "CombatBackground_B1";

    [MenuItem("Tools/Project/Level Design/Use Shared Backgrounds In Rest And Level")]
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
                var behaviors = root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(b => !(b is RoomBiomeEnvironment) && !(b is ParallaxBackground)).ToArray();
                string[] bindings = behaviors.Select(b => EditorJsonUtility.ToJson(b)).ToArray();
                Apply(root, kind);
                if (physics != BiomeLayoutAuthoring.PhysicsSnapshot(root)
                    || !bindings.SequenceEqual(behaviors.Select(b => EditorJsonUtility.ToJson(b))))
                    throw new InvalidOperationException("Shared background changed room gameplay or UI bindings: " + kind);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        SafeRoomBiomeAuthoring.CaptureAll();
        Debug.Log("[Safe Room Backgrounds] Rest and Level now use the shared B1/B2 parallax prefabs, including B1 VeryFar. Legacy flattened artwork disabled; gameplay, displays and floors preserved.");
    }

    public static void Apply(GameObject root, string kind)
    {
        if (kind != "Rest" && kind != "Level") throw new ArgumentException("Only Rest and Level are supported.", nameof(kind));
        var firstTheme = RoomBackgroundAuthoring.BiomeOneRoot(root);
        var background = firstTheme.Find(BackgroundName);
        if (background == null)
            background = ((GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(B1VeryFarBackground.PrefabPath), firstTheme)).transform;
        var floor = firstTheme.Find(SafeRoomFloorAuthoring.BiomeOneGroup);
        var market = root.GetComponentInChildren<RoomBiomeEnvironment>(true).transform.Find(kind + "Environment_B2");
        foreach (var image in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!SafeRoomBiomeAuthoring.IsOriginalArtwork(image)
                || image.transform.IsChildOf(background) || image.transform.IsChildOf(floor) || image.transform.IsChildOf(market)) continue;
            // Interaction markers and gameplay-bearing objects are not scenery.
            if (image.GetComponent<Collider2D>() != null || image.GetComponents<MonoBehaviour>().Any(b => !(b is ParallaxBackground))) continue;
            image.enabled = false;
            var parallax = image.GetComponent<ParallaxBackground>();
            if (parallax != null) { parallax.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(parallax); }
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }

        float floorY = SafeRoomBiomeAuthoring.FloorHeight(new[] { root });
        var near = background.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name.StartsWith("place2_0", StringComparison.Ordinal)).ToArray();
        var ground = root.GetComponentsInChildren<BoxCollider2D>(true).Where(b => b.name == "Ground" && !b.isTrigger)
            .OrderByDescending(b => LayoutRoadAuthoring.WorldRect(b).width).First();
        Rect bounds = LayoutRoadAuthoring.WorldRect(ground);
        float center = (near.Min(r => r.bounds.min.x) + near.Max(r => r.bounds.max.x)) * .5f;
        background.position += new Vector3(bounds.center.x - center, floorY - near.Min(r => r.bounds.min.y), 0f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(background);
        foreach (var layer in background.GetComponentsInChildren<ParallaxBackground>(true))
            if (SkyBackgroundRepair.IsSky(layer)) SkyBackgroundRepair.Configure(layer, bounds.width * 2f + 100f);
        // B2 already contains the same shared market background used by combat rooms.
        foreach (var layer in market.GetComponentsInChildren<ParallaxBackground>(true))
            if (SkyBackgroundRepair.IsSky(layer)) SkyBackgroundRepair.Configure(layer, bounds.width * 2f + 100f);
        SafeRoomBiomeAuthoring.RefreshBindings(root, kind);
        BiomeMaterialAuthoring.ApplyIfPrepared(root);
    }
}
