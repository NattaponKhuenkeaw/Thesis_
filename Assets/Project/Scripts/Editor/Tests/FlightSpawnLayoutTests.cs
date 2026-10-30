#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class FlightSpawnLayoutTests
{
    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void EveryAuthoredSlotKeepsTheFlyerConnectedToItsOwnFloor(int level)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        var flyer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab");
        var fixture = new GameObject("Flight Spawn LV" + level);
        fixture.transform.position = new Vector3(1000, 0, 0);
        try
        {
            var terrain = Object.Instantiate(source.transform.Find("Terrain").gameObject, fixture.transform, false);
            terrain.GetComponent<CompositeCollider2D>().GenerateGeometry();
            Object.Instantiate(source.transform.Find("OneWayPlatforms").gameObject, fixture.transform, false);
            foreach (var original in source.transform.Find("Boundaries").GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (!original.enabled) continue;
                var wall = new GameObject(original.name); wall.transform.SetParent(fixture.transform, false);
                wall.layer = original.gameObject.layer;
                wall.transform.position = fixture.transform.position + original.transform.position;
                var box = wall.AddComponent<BoxCollider2D>();
                box.offset = Vector2.Scale(original.offset, original.transform.lossyScale);
                box.size = Vector2.Scale(original.size, original.transform.lossyScale);
            }
            foreach (var chamber in source.GetComponentInChildren<CombatStageController>().Chambers)
                foreach (var slot in chamber.spawnSlots)
                {
                    var actor = Object.Instantiate(flyer, fixture.transform, false);
                    try
                    {
                        actor.transform.position = fixture.transform.position + slot.position;
                        Physics2D.SyncTransforms();
                        var flight = actor.GetComponent<EnemyLocomotion>();
                        // EditMode instantiation does not run the runtime mask/gravity initialization.
                        typeof(EnemyLocomotion).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flight, null);
                        Assert.That(flight.PrepareSpawnFlight(), Is.True, $"LV{level} {chamber.name} {slot.name} at {slot.position}");
                        Physics2D.SyncTransforms();
                        var body = actor.GetComponent<EnemyController>().bodyCollider;
                        var filter = new ContactFilter2D();
                        filter.SetLayerMask(LayerMask.GetMask("Ground", "OneWayPlatform")); filter.useTriggers = false;
                        var hits = new RaycastHit2D[32];
                        // Sweep the full body from the authored walking plane to its final airborne position.
                        Vector2 start = new Vector2(body.bounds.center.x, slot.position.y + body.bounds.extents.y + .04f);
                        int count = Physics2D.BoxCast(start, body.bounds.size, 0f, Vector2.up, filter, hits,
                            Mathf.Max(0f, body.bounds.center.y - start.y));
                        Assert.That(hits.Take(count).Any(h => h.collider != null && h.normal.y < -.1f), Is.False,
                            $"LV{level} {chamber.name} {slot.name}: a ceiling separates the flyer from its authored ground.");
                    }
                    finally { Object.DestroyImmediate(actor); }
                }
        }
        finally { Object.DestroyImmediate(fixture); }
    }
}
#endif
