using System.Collections.Generic;
using UnityEngine;

namespace Project.AIBehaviour.Boss
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class BossGroundWave : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Sprite[] frames;
        private EnemyController owner;
        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private HashSet<int> hitTargets;
        private float speed, remaining, direction, clock;
        private bool initialized;
        private readonly RaycastHit2D[] probes = new RaycastHit2D[8];

        public void Initialize(EnemyController source, Vector2 groundPosition, float facing,
            float travelSpeed, float distance, HashSet<int> sharedTargets)
        {
            owner = source;
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<BoxCollider2D>();
            hitTargets = sharedTargets;
            direction = facing < 0f ? -1f : 1f;
            speed = Mathf.Max(1f, travelSpeed);
            remaining = Mathf.Max(1f, distance);
            transform.position = groundPosition + Vector2.up * .02f;
            if (visual != null) visual.flipX = direction < 0f;
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
            if (visual != null && frames != null && frames.Length > 0)
                visual.sprite = frames[(int)(clock * 10f) % frames.Length];
        }

        private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);
        private void OnTriggerStay2D(Collider2D other) => TryDamage(other);

        private void TryDamage(Collider2D other)
        {
            if (!initialized || owner == null || !owner.IsCombatReady) return;
            PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
            if (player == null || !hitTargets.Add(player.transform.root.gameObject.GetInstanceID())) return;
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
