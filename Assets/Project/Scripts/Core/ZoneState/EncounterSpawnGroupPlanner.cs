using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>Plans a complete compact batch on supported, connected ground before any enemy is created.</summary>
    public sealed class EncounterSpawnGroupPlanner
    {
        public struct Placement
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Bounds Body;
        }

        private struct Footprint
        {
            public Bounds Bounds;
            public float FlightHeight;
            public int CollisionMask;
            public bool Ranged;
        }

        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[32];
        private readonly Collider2D[] overlaps = new Collider2D[64];
        private readonly RaycastHit2D[] casts = new RaycastHit2D[64];
        private readonly List<Placement>[] options = new List<Placement>[5];
        private readonly Placement[] working = new Placement[5];
        private readonly int[] order = new int[5];
        private readonly Footprint[] footprints = new Footprint[5];
        private int searchBudget;
        private float separation;
        private float width;

        public bool TryPlan(IReadOnlyList<GameObject> prefabs, Transform[] slots, Vector3 player,
            bool hasPlayer, float minimumSeparation, float clusterWidth, float playerClearance,
            bool allowGroundSearch, List<Placement> result)
        {
            result.Clear();
            if (prefabs == null || prefabs.Count == 0 || prefabs.Count > 5 || slots == null) return false;
            separation = Mathf.Max(.1f, minimumSeparation);
            width = Mathf.Max(1f, clusterWidth);
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (!TryFootprint(prefabs[i], out footprints[i])) return false;
                order[i] = i;
                if (options[i] == null) options[i] = new List<Placement>();
            }
            // Reserve space for larger ground bodies first, then place ranged enemies behind them.
            System.Array.Sort(order, 0, prefabs.Count, Comparer<int>.Create((a, b) =>
            {
                int role = footprints[a].Ranged.CompareTo(footprints[b].Ranged);
                return role != 0 ? role : footprints[b].Bounds.size.x.CompareTo(footprints[a].Bounds.size.x);
            }));
            var anchors = new List<Transform>();
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                anchors.Add(slot);
                left = Mathf.Min(left, slot.position.x);
                right = Mathf.Max(right, slot.position.x);
            }
            anchors.Sort((a, b) => Score(a.position, player).CompareTo(Score(b.position, player)));
            Physics2D.SyncTransforms();
            foreach (var anchor in anchors)
            {
                if (!TryGround(anchor.position.x, anchor.position.y, out float floor)) continue;
                foreach (float shift in new[] { 0f, width * .125f, -width * .125f, width * .25f, -width * .25f })
                {
                    float center = anchor.position.x + shift;
                    float min = Mathf.Max(left, center - width * .5f);
                    float max = Mathf.Min(right, center + width * .5f);
                    bool complete = true;
                    for (int i = 0; i < prefabs.Count; i++)
                    {
                        var list = options[i];
                        list.Clear();
                        if (allowGroundSearch)
                        {
                            for (float x = min; x <= max + .001f; x += .5f)
                                AddCandidate(list, footprints[i], x, anchor, floor, player, hasPlayer,
                                    playerClearance, center - width * .5f, center + width * .5f);
                        }
                        foreach (var slot in slots)
                            if (slot != null && slot.position.x >= min && slot.position.x <= max
                                && Mathf.Abs(slot.position.y - anchor.position.y) < .4f)
                                AddCandidate(list, footprints[i], slot.position.x, anchor, floor, player,
                                    hasPlayer, playerClearance, center - width * .5f, center + width * .5f);
                        list.Sort((a, b) =>
                        {
                            float da = Mathf.Abs(a.Body.center.x - player.x), db = Mathf.Abs(b.Body.center.x - player.x);
                            return footprints[i].Ranged ? db.CompareTo(da) : da.CompareTo(db);
                        });
                        if (list.Count == 0) { complete = false; break; }
                    }
                    if (!complete) continue;
                    searchBudget = 12000;
                    if (!Place(0, prefabs.Count)) continue;
                    for (int i = 0; i < prefabs.Count; i++) result.Add(working[i]);
                    return true;
                }
            }
            return false;
        }

        private static float Score(Vector3 point, Vector3 player)
            => Mathf.Abs(point.x - player.x) + Mathf.Abs(point.y - player.y) * 4f;

        private bool Place(int depth, int count)
        {
            if (depth == count) return true;
            if (--searchBudget <= 0) return false;
            int index = order[depth];
            foreach (var candidate in options[index])
            {
                bool clear = true;
                float min = candidate.Body.min.x, max = candidate.Body.max.x;
                for (int prior = 0; prior < depth; prior++)
                {
                    var body = working[order[prior]].Body;
                    float required = Mathf.Max(separation, (body.size.x + candidate.Body.size.x) * .5f + .15f);
                    if (Mathf.Abs(body.center.x - candidate.Body.center.x) < required) { clear = false; break; }
                    min = Mathf.Min(min, body.min.x); max = Mathf.Max(max, body.max.x);
                }
                if (!clear || max - min > width + .001f) continue;
                working[index] = candidate;
                if (Place(depth + 1, count)) return true;
            }
            return false;
        }

        private void AddCandidate(List<Placement> list, Footprint shape, float x, Transform anchor, float floor,
            Vector3 player, bool hasPlayer, float playerClearance, float min, float max)
        {
            if (hasPlayer && Mathf.Abs(x + shape.Bounds.center.x - player.x) < playerClearance) return;
            if (!TryGround(x + shape.Bounds.center.x, floor, out float ground) || Mathf.Abs(ground - floor) > .3f) return;
            for (float step = Mathf.Min(x, anchor.position.x); step <= Mathf.Max(x, anchor.position.x); step += .5f)
                if (!TryGround(step, floor, out float support) || Mathf.Abs(support - floor) > .3f) return;
            float half = shape.Bounds.size.x * .4f;
            if (!TryGround(x + shape.Bounds.center.x - half, floor, out float a)
                || !TryGround(x + shape.Bounds.center.x + half, floor, out float b)
                || Mathf.Abs(a - floor) > .3f || Mathf.Abs(b - floor) > .3f) return;
            Vector3 origin = new Vector3(x, ground + .01f - shape.Bounds.min.y, anchor.position.z);
            Bounds body = new Bounds(origin + shape.Bounds.center, shape.Bounds.size);
            if (body.min.x < min - .001f || body.max.x > max + .001f || !BodyClear(body, shape.CollisionMask)) return;
            var anchorBody = body;
            anchorBody.center += Vector3.right * (anchor.position.x - x);
            if (!HorizontalPathClear(anchorBody, x - anchor.position.x, shape.CollisionMask)) return;
            if (shape.FlightHeight > 0f)
            {
                bool clear = false;
                for (float lift = shape.FlightHeight; lift >= .1f; lift -= .25f)
                {
                    var airborne = body; airborne.center += Vector3.up * lift;
                    if (!BodyClear(airborne, shape.CollisionMask) || !LiftClear(body, lift, shape.CollisionMask)) continue;
                    body = airborne; clear = true; break;
                }
                if (!clear) return;
            }
            foreach (var previous in list)
                if (Mathf.Abs(previous.Position.x - origin.x) < .01f) return;
            list.Add(new Placement { Position = origin, Rotation = anchor.rotation, Body = body });
        }

        private bool TryGround(float x, float y, out float floor)
        {
            floor = 0f;
            var filter = new ContactFilter2D();
            filter.SetLayerMask(LayerMask.GetMask("Ground", "OneWayPlatform"));
            filter.useTriggers = false;
            int count = Physics2D.Raycast(new Vector2(x, y + .6f), Vector2.down, filter, groundHits, 1.2f);
            if (count == groundHits.Length) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (IsActor(hit.collider) || hit.normal.y < .5f || hit.distance >= nearest) continue;
                floor = hit.point.y; nearest = hit.distance;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        private bool BodyClear(Bounds body, int mask)
        {
            var filter = new ContactFilter2D(); filter.SetLayerMask(mask); filter.useTriggers = false;
            int count = Physics2D.OverlapBox(body.center, Vector2.Max(Vector2.one * .01f, (Vector2)body.size - Vector2.one * .06f), 0f, filter, overlaps);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!IsActor(overlaps[i])) return false;
            return true;
        }

        private bool LiftClear(Bounds body, float height, int mask)
        {
            var filter = new ContactFilter2D(); filter.SetLayerMask(mask); filter.useTriggers = false;
            int count = Physics2D.BoxCast((Vector2)body.center + Vector2.up * .03f,
                (Vector2)body.size - Vector2.one * .02f, 0f, Vector2.up, filter, casts, height);
            if (count == casts.Length) return false;
            for (int i = 0; i < count; i++) if (!IsActor(casts[i].collider) && casts[i].normal.y <= .1f) return false;
            return true;
        }

        private bool HorizontalPathClear(Bounds body, float distance, int mask)
        {
            if (Mathf.Abs(distance) < .01f) return true;
            var filter = new ContactFilter2D(); filter.SetLayerMask(mask); filter.useTriggers = false;
            int count = Physics2D.BoxCast(body.center, (Vector2)body.size - Vector2.one * .06f, 0f,
                Vector2.right * Mathf.Sign(distance), filter, casts, Mathf.Abs(distance));
            if (count == casts.Length) return false;
            for (int i = 0; i < count; i++) if (!IsActor(casts[i].collider) && Mathf.Abs(casts[i].normal.x) > .5f) return false;
            return true;
        }

        private static bool IsActor(Collider2D collider) => collider == null
            || collider.GetComponentInParent<EnemyController>() != null || collider.GetComponentInParent<PlayerCharacter>() != null;

        private static bool TryFootprint(GameObject prefab, out Footprint shape)
        {
            shape = default;
            var enemy = prefab != null ? prefab.GetComponent<EnemyController>() : null;
            var collider = enemy != null ? enemy.bodyCollider : null;
            if (collider == null) return false;
            Vector2 size;
            if (collider is BoxCollider2D box) size = box.size + Vector2.one * box.edgeRadius * 2f;
            else if (collider is CapsuleCollider2D capsule) size = capsule.size;
            else if (collider is CircleCollider2D circle) size = Vector2.one * circle.radius * 2f;
            else return false;
            var matrix = Matrix4x4.TRS(Vector3.zero, prefab.transform.localRotation, prefab.transform.localScale)
                * prefab.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            Vector3 first = matrix.MultiplyPoint3x4(collider.offset - size * .5f);
            Bounds bounds = new Bounds(first, Vector3.zero);
            bounds.Encapsulate(matrix.MultiplyPoint3x4(collider.offset + size * .5f));
            bounds.Encapsulate(matrix.MultiplyPoint3x4(collider.offset + new Vector2(size.x, -size.y) * .5f));
            bounds.Encapsulate(matrix.MultiplyPoint3x4(collider.offset + new Vector2(-size.x, size.y) * .5f));
            var flight = prefab.GetComponent<EnemyLocomotion>();
            shape = new Footprint { Bounds = bounds, Ranged = enemy.UsesRangedAttack,
                FlightHeight = flight != null && flight.UsesFixedHeightFlight ? flight.SpawnFlightHeight : 0f,
                CollisionMask = Physics2D.GetLayerCollisionMask(collider.gameObject.layer) };
            return true;
        }
    }
}
