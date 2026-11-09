using System.Collections.Generic;
using UnityEngine;

namespace Project.AIBehaviour.Boss
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class BossGroundWave : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Sprite[] frames;
        private EnemyController owner;
        private Rigidbody2D body;
        private Collider2D hitbox;
        private PolygonCollider2D spriteHitbox;
        private Sprite hitboxFrame;
        private readonly List<Vector2> shape = new List<Vector2>();
        private HashSet<int> hitTargets;
        private float speed, remaining, direction, clock;
        private bool initialized;
        private readonly RaycastHit2D[] probes = new RaycastHit2D[8];

        internal void DrawInFrontOf(SpriteRenderer other)
        {
            if (visual == null) return;
            if (visual.sortingLayerID != other.sortingLayerID)
                visual.sortingLayerID = other.sortingLayerID;
            int order = other.sortingOrder + 1;
            if (visual.sortingOrder != order) visual.sortingOrder = order;
        }

        public void Initialize(EnemyController source, Vector2 groundPosition, float facing,
            float travelSpeed, float distance, HashSet<int> sharedTargets)
        {
            owner = source;
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<Collider2D>();
            spriteHitbox = hitbox as PolygonCollider2D;
            hitTargets = sharedTargets;
            direction = facing < 0f ? -1f : 1f;
            speed = Mathf.Max(1f, travelSpeed);
            remaining = Mathf.Max(1f, distance);
            transform.position = groundPosition + Vector2.up * .02f;
            if (visual != null) visual.flipX = direction < 0f;
            hitboxFrame = null;
            ApplyFrame(0);
            initialized = true;
        }

        private void FixedUpdate()
        {
            if (!initialized) return;
            if (owner == null || !owner.IsCombatReady || owner.IsGrabbed || remaining <= 0f)
            {
                Cancel();
                return;
            }
            float step = Mathf.Min(remaining, speed * Time.fixedDeltaTime);
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(LayerMask.GetMask("Ground"));
            int count = hitbox.Cast(Vector2.right * direction, filter, probes, step);
            for (int i = 0; i < count; i++)
                if (probes[i].normal.x * direction < -.5f)
                {
                    Cancel();
                    return;
                }
            Vector2 next = body.position + Vector2.right * (direction * step);
            int groundCount = Physics2D.Raycast(next + Vector2.up, Vector2.down, filter, probes, 2f);
            bool supported = false;
            for (int i = 0; i < groundCount; i++)
                if (probes[i].normal.y >= .5f)
                {
                    next.y = probes[i].point.y + .02f;
                    supported = true;
                    break;
                }
            if (!supported)
            {
                Cancel();
                return;
            }
            body.MovePosition(next);
            remaining -= step;
            clock += Time.fixedDeltaTime;
            ApplyFrame((int)(clock * 10f));
        }

        private void ApplyFrame(int frameIndex)
        {
            if (visual == null || frames == null || frames.Length == 0) return;
            Sprite frame = frames[frameIndex % frames.Length];
            visual.sprite = frame;
            if (spriteHitbox == null || hitboxFrame == frame) return;

            hitboxFrame = frame;
            spriteHitbox.pathCount = frame != null ? frame.GetPhysicsShapeCount() : 0;
            for (int path = 0; path < spriteHitbox.pathCount; path++)
            {
                frame.GetPhysicsShape(path, shape);
                // SpriteRenderer.flipX does not flip colliders.
                if (visual.flipX)
                {
                    for (int i = 0; i < shape.Count; i++)
                        shape[i] = new Vector2(-shape[i].x, shape[i].y);
                    shape.Reverse();
                }
                spriteHitbox.SetPath(path, shape);
            }
        }

        private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);
        private void OnTriggerStay2D(Collider2D other) => TryDamage(other);

        private void TryDamage(Collider2D other)
        {
            if (!initialized || owner == null || !owner.IsCombatReady) return;
            PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
            if (player == null) return;
            // Do not treat a speculative trigger contact as overlap with the visible crest.
            if (spriteHitbox != null && !spriteHitbox.Distance(other).isOverlapped) return;
            if (!hitTargets.Add(player.transform.root.gameObject.GetInstanceID())) return;
            if (player.TryCounter(owner.gameObject)) return;
            var context = IncomingAttackContext.FromEnemy(owner, EnemyAttackProperty.Normal, true);
            if (player.TakeDamage(Mathf.Max(0f, owner.attackDamage), context))
                CameraManager.instance?.ShakeByImpact(DamageImpactTier.Low);
        }

        public void Cancel()
        {
            initialized = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
