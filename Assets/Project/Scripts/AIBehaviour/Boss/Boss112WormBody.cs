using System.Collections.Generic;
using UnityEngine;

namespace Project.AIBehaviour.Boss
{
    [DefaultExecutionOrder(30)]
    [DisallowMultipleComponent]
    public sealed class Boss112WormBody : MonoBehaviour
    {
        [SerializeField] private Boss112Controller boss;
        [SerializeField] private EnemyColorVariants colors;
        [SerializeField] private SpriteRenderer[] segments;
        [SerializeField] private CircleCollider2D groundHurtbox;
        [SerializeField, Range(0f, 1f)] private float segmentDamageMultiplier = .65f;

        private const float SegmentSpacing = 2.05f;
        private const float MaxJointDistance = 2.7f;
        private const float GroundHurtboxHeight = 1.7f;
        private const float SegmentArtRadiusPixels = 288f;
        private static readonly Color HitTint = new Color(1f, .12f, .1f);
        private readonly List<Vector3> headTrail = new List<Vector3>(256);
        private SpriteRenderer head;
        private float segmentRadius;
        private Vector3[] hangingPositions;
        private Vector3[] pose;
        private Vector3[] followVelocity;
        private Vector2 authoredHurtboxOffset;
        private CircleCollider2D headHurtbox;
        private Vector2 authoredHeadOffset;
        private float trailWeight;
        private CircleCollider2D[] segmentHurtboxes;
        private float[] segmentFlashUntil;
        private float tailX;
        private float braceWeight, braceSide;
        private float beamPoseWeight, beamSide, beamLaneX;

        public CircleCollider2D GroundHurtbox => groundHurtbox;
        public float SegmentDamageMultiplier => segmentDamageMultiplier;

        public IReadOnlyList<CircleCollider2D> SegmentHurtboxes => segmentHurtboxes;

        public void FlashSegment(int index) => segmentFlashUntil[index] = Time.time + .12f;

        public bool HitsHead(Collider2D attack, Collider2D contact)
        {
            if (headHurtbox == null || !headHurtbox.enabled) return false;
            if (contact == headHurtbox) return true;
            var separation = attack != null && attack.enabled ? attack.Distance(headHurtbox) : default;
            return separation.isValid && separation.distance <= 0f;
        }

        public int GetSegmentContacts(Collider2D attack, Collider2D contact = null, Collider2D excluded = null)
        {
            int contacts = 0;
            for (int i = 0; i < segmentHurtboxes.Length; i++)
            {
                var segment = segmentHurtboxes[i];
                if (segment == null || !segment.enabled || segment == excluded) continue;
                var separation = attack != null && attack.enabled ? attack.Distance(segment) : default;
                if (segment == contact || (separation.isValid && separation.distance <= 0f))
                    contacts |= 1 << i;
            }
            return contacts;
        }

        public void Configure(Boss112Controller owner, EnemyColorVariants variants,
            SpriteRenderer[] bodySegments, CircleCollider2D hurtbox)
        {
            boss = owner;
            colors = variants;
            segments = bodySegments;
            groundHurtbox = hurtbox;
        }

        private void Awake()
        {
            var visual = transform.Find("Visual");
            if (visual != null) head = visual.GetComponent<SpriteRenderer>();
            headHurtbox = boss.Enemy.bodyCollider as CircleCollider2D;
            if (headHurtbox != null) authoredHeadOffset = headHurtbox.offset;
            segmentHurtboxes = new CircleCollider2D[segments?.Length ?? 0];
            segmentFlashUntil = new float[segmentHurtboxes.Length];
            for (int i = 0; i < segmentHurtboxes.Length; i++)
                if (segments[i] != null) segmentHurtboxes[i] = segments[i].GetComponent<CircleCollider2D>();
            if (segments != null && segments.Length > 0 && segments[0] != null && segments[0].sprite != null)
                segmentRadius = SegmentArtRadiusPixels / segments[0].sprite.pixelsPerUnit;
            if (groundHurtbox != null) authoredHurtboxOffset = groundHurtbox.offset;
        }

        private void LateUpdate()
        {
            if (boss == null || segments == null || segments.Length == 0) return;

            bool diving = boss.Phase is Boss112Phase.SlamTracking or Boss112Phase.SlamLocked or Boss112Phase.SlamFalling
                or Boss112Phase.SlamFeint or Boss112Phase.SlamCommit or Boss112Phase.SlamImpact;
            bool bracing = boss.Phase is Boss112Phase.SlamTracking or Boss112Phase.SlamLocked;
            bool tailPlanted = (diving && boss.Phase != Boss112Phase.SlamTracking) || boss.Phase == Boss112Phase.Recovery;
            bool aiming = boss.Phase is Boss112Phase.BeamTracking or Boss112Phase.BeamLocked or Boss112Phase.BeamActive
                or Boss112Phase.SweepPositioning or Boss112Phase.SweepCharging or Boss112Phase.SweepActive;
            bool visible = boss.Enemy.IsAlive;
            float height = transform.position.y - boss.FloorY;
            if (headHurtbox != null)
            {
                Vector2 offset = boss.IsStaggered ? new Vector2(authoredHeadOffset.x, 3.2f) : authoredHeadOffset;
                if (headHurtbox.offset != offset) headHurtbox.offset = offset;
            }
            bool canHitSegments = visible && boss.Enemy.IsCombatReady
                && (boss.Phase == Boss112Phase.Recovery || (!diving && height > 4f));
            foreach (var hurtbox in segmentHurtboxes)
                if (hurtbox != null) hurtbox.enabled = canHitSegments;

            if (!visible)
            {
                foreach (var segment in segments) if (segment != null) segment.enabled = false;
                ResetTrail();
                return;
            }

            Color tint = colors != null && colors.SelectedIndex == 1
                ? new Color(.04f, .67f, .58f)
                : colors != null && colors.SelectedIndex == 2
                    ? new Color(.7f, .77f, .06f)
                    : new Color(.68f, .05f, .61f);
            Vector3 top = head != null && head.sprite != null
                ? head.transform.TransformPoint(head.sprite.bounds.center)
                : transform.position + Vector3.up * 3.5f;
            bool seed = headTrail.Count == 0;
            if (seed)
            {
                hangingPositions ??= new Vector3[segments.Length];
                pose ??= new Vector3[segments.Length];
                followVelocity ??= new Vector3[segments.Length];
                for (int i = segments.Length; i >= 0; i--)
                    headTrail.Add(top - Vector3.up * (i * SegmentSpacing));
            }
            else if ((top - headTrail[headTrail.Count - 1]).sqrMagnitude >= .04f * .04f)
                headTrail.Add(top);

            if (bracing && braceWeight == 0f)
                braceSide = top.x > (boss.ArenaLeft + boss.ArenaRight) * .5f ? -1f : 1f;
            braceWeight = Mathf.MoveTowards(braceWeight, bracing ? 1f : 0f, 8f * Time.deltaTime);
            bool followingDive = diving && !bracing;
            trailWeight = Mathf.MoveTowards(trailWeight, followingDive ? 1f : 0f, (followingDive ? 16f : 8f) * Time.deltaTime);
            float swayWeight = diving || tailPlanted ? 0f : 1f;
            float impactAge = boss.Phase switch
            {
                Boss112Phase.SlamImpact => boss.PhaseElapsed,
                Boss112Phase.Recovery => boss.SlamImpactDuration + boss.PhaseElapsed,
                Boss112Phase.SlamTracking when boss.Pattern == Boss112Pattern.TripleSlam && boss.SlamImpactCount > 0
                    => boss.SlamImpactDuration + boss.PhaseElapsed,
                _ => -1f
            };
            float grounded = 1f - Mathf.SmoothStep(0f, 1f, height / 4f);
            int last = segments.Length - 1;
            SpriteRenderer tail = segments[last];
            float tailY = boss.FloorY + GroundHurtboxHeight;
            if (tail != null && tail.sprite != null)
            {
                tail.transform.localScale = Vector3.one * 1.05f;
                tailY = boss.FloorY + segmentRadius * tail.transform.lossyScale.y;
            }
            Vector3 bottom = new Vector3(transform.position.x - .7f, tailY, transform.position.z);
            Vector3 trace = top;
            int trailIndex = headTrail.Count - 1;
            for (int i = 0; i < segments.Length; i++)
            {
                trace = SampleBehind(trace, ref trailIndex);
                float t = (i + 1f) / segments.Length;
                Vector3 target = Vector3.Lerp(top, bottom, t);
                target.x += Mathf.Sin(t * Mathf.PI) * 1.7f
                    + Mathf.Sin(Time.time * 2.4f - i * .8f) * Mathf.Lerp(.12f, .22f, t) * swayWeight;
                target.y += Mathf.Sin(Time.time * 2.4f - i * .8f) * .08f * (1f - t) * swayWeight;
                Vector3 resting = top + new Vector3(1.4f * (i + 1f), 0f);
                resting.y = boss.FloorY + .9f + Mathf.Sin(t * Mathf.PI) * .7f;
                target = Vector3.Lerp(target, resting, grounded);
                hangingPositions[i] = seed ? target : Vector3.SmoothDamp(hangingPositions[i], target,
                    ref followVelocity[i], Mathf.Lerp(.06f, .2f, t));
                // Blend into the head's recorded path during the dive. Keep a ground-level target while hovering.
                float weight = Mathf.Lerp(.3f * (1f - t) * (1f - grounded), 1f, trailWeight);
                pose[i] = Vector3.Lerp(hangingPositions[i], trace, weight);
                pose[i].y = Mathf.Max(boss.FloorY + .55f, pose[i].y);
            }

            // Solve toward a floor-pinned tail while keeping each joint close enough to reach the next.
            float reach = segments.Length * MaxJointDistance;
            float vertical = top.y - tailY;
            float horizontalReach = Mathf.Sqrt(Mathf.Max(0f, reach * reach - vertical * vertical));
            if (aiming)
            {
                if (beamPoseWeight == 0f)
                    beamSide = top.x > (boss.ArenaLeft + boss.ArenaRight) * .5f ? -1f : 1f;
                beamLaneX = boss.Phase is Boss112Phase.BeamTracking or Boss112Phase.SweepPositioning
                    ? transform.position.x : boss.Beam.LaneX;
            }
            beamPoseWeight = Mathf.MoveTowards(beamPoseWeight, aiming ? 1f : 0f, 8f * Time.deltaTime);
            float followX = hangingPositions[last].x;
            if (beamPoseWeight > 0f && tail != null && segments[0] != null)
            {
                // Raise the first joint above the mouth, then route the rest around the full firing lane.
                float sideX = beamLaneX + beamSide * (boss.Beam.LaneHalfWidth + segmentRadius * tail.transform.lossyScale.x + .15f);
                float shoulderY = Mathf.Max(top.y, boss.Beam.MouthY + segmentRadius * segments[0].transform.lossyScale.y + .15f);
                Vector3 shoulder = new Vector3(top.x + beamSide * 2.35f, shoulderY, top.z);
                pose[0] = Vector3.Lerp(pose[0], shoulder, beamPoseWeight);
                for (int i = 1; i < last; i++)
                    pose[i] = Vector3.Lerp(pose[i], new Vector3(sideX, Mathf.Lerp(shoulderY, tailY, (float)i / last), top.z), beamPoseWeight);
                followX = Mathf.Lerp(followX, sideX, beamPoseWeight);
            }
            if (!tailPlanted || seed)
                tailX = Mathf.Clamp(followX, top.x - horizontalReach, top.x + horizontalReach);
            else
            {
                // Resume following from the planted world position instead of jumping on release.
                hangingPositions[last].x = tailX;
                followVelocity[last].x = 0f;
            }
            Vector3 tailAnchor = new Vector3(tailX, tailY, top.z);
            Vector3 previous = top;
            for (int i = 0; i < segments.Length; i++)
            {
                float pulse = i < last ? ImpactPulse(impactAge, i) : 0f;
                if (i < last)
                {
                    float t = (i + 1f) / segments.Length;
                    Vector3 brace = Vector3.Lerp(top, tailAnchor, t);
                    brace.x += braceSide * Mathf.Sin(Mathf.PI * t) * 1.15f;
                    Vector3 desired = Vector3.Lerp(pose[i], brace, braceWeight);
                    desired.x += braceSide * .3f * pulse;
                    previous = FitJoint(previous, desired, tailAnchor, last - i);
                }
                else previous = tailAnchor;
                SpriteRenderer segment = segments[i];
                if (segment == null) continue;
                segment.enabled = true;
                segment.transform.position = previous;
                segment.transform.localScale = i == last ? Vector3.one * 1.05f
                    : new Vector3(1f + .16f * pulse, 1f - .24f * pulse, 1f) * .9f;
                segment.color = Time.time < segmentFlashUntil[i] ? HitTint : tint;
            }
            if (groundHurtbox != null && groundHurtbox.enabled)
            {
                // Keep ground attacks in reach independently of the sprite's bottom edge.
                Vector3 correction = Vector3.up * (boss.FloorY + GroundHurtboxHeight - tailAnchor.y);
                groundHurtbox.offset = authoredHurtboxOffset + (Vector2)groundHurtbox.transform.InverseTransformVector(correction);
            }
            if (trailIndex > 0) headTrail.RemoveRange(0, trailIndex);
        }

        private static float ImpactPulse(float age, int joint)
        {
            // One compression and release per impact, travelling outward without a rebound.
            float t = (age - joint * .04f) / .1f;
            return t > 0f && t < 1f ? 4f * t * (1f - t) : 0f;
        }

        private static Vector3 FitJoint(Vector3 previous, Vector3 desired, Vector3 tail, int jointsLeft)
        {
            Vector3 candidate = Vector3.MoveTowards(previous, desired, MaxJointDistance);
            float remainingReach = jointsLeft * MaxJointDistance;
            float reachSquared = remainingReach * remainingReach;
            if ((candidate - tail).sqrMagnitude <= reachSquared) return candidate;

            Vector3 reachable = Vector3.Lerp(previous, tail, 1f / (jointsLeft + 1));
            for (int i = 0; i < 8; i++)
            {
                Vector3 midpoint = (reachable + candidate) * .5f;
                if ((midpoint - tail).sqrMagnitude <= reachSquared) reachable = midpoint;
                else candidate = midpoint;
            }
            return reachable;
        }

        private Vector3 SampleBehind(Vector3 position, ref int index)
        {
            float remaining = SegmentSpacing;
            while (index >= 0)
            {
                Vector3 point = headTrail[index];
                float distance = Vector3.Distance(position, point);
                if (distance >= remaining) return Vector3.Lerp(position, point, remaining / distance);
                remaining -= distance;
                position = point;
                index--;
            }
            return position;
        }

        private void OnDisable()
        {
            if (headHurtbox != null) headHurtbox.offset = authoredHeadOffset;
            foreach (var hurtbox in segmentHurtboxes)
                if (hurtbox != null) hurtbox.enabled = false;
            ResetTrail();
        }

        private void ResetTrail()
        {
            headTrail.Clear();
            trailWeight = 0f;
            System.Array.Clear(segmentFlashUntil, 0, segmentFlashUntil.Length);
            braceWeight = 0f;
            beamPoseWeight = 0f;
            if (followVelocity != null) System.Array.Clear(followVelocity, 0, followVelocity.Length);
        }
    }
}
