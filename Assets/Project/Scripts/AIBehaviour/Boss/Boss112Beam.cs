using UnityEngine;

namespace Project.AIBehaviour.Boss
{
    [DefaultExecutionOrder(30)]
    public sealed class Boss112Beam : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Transform sourcePoint;
        [SerializeField] private Sprite[] warningFrames;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private BoxCollider2D damageCollider;
        [SerializeField] private EnemyAttackHitbox damage;
        [SerializeField, Min(.05f)] private float warningWidth = .504f;
        [SerializeField, Min(.1f)] private float activeWidth = 2.8f;
        private bool damaging;
        private EnemyColorVariants appearance;
        private Vector2 groundAnchor;
        public bool IsDamaging => damaging && damageCollider != null && damageCollider.enabled;
        public BoxCollider2D DamageCollider => damageCollider;
        internal float LaneX => groundAnchor.x;
        internal float LaneHalfWidth => activeWidth * .5f;
        internal float MouthY => sourcePoint != null ? sourcePoint.position.y : visual.bounds.max.y;

        private void Awake()
        {
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>(true);
            if (sourcePoint == null) sourcePoint = transform.parent.Find("Visual/BeamMouth");
            if (damageCollider == null) damageCollider = GetComponent<BoxCollider2D>();
            if (damage == null) damage = GetComponent<EnemyAttackHitbox>();
            appearance = GetComponentInParent<EnemyColorVariants>();
            var frames = appearance != null ? appearance.SourceProjectileFrames : null;
            if (frames?.Length == 5)
            {
                if (warningFrames == null || warningFrames.Length == 0) warningFrames = frames[..4];
                if (activeSprite == null) activeSprite = frames[4];
            }
        }

        public void Show(Vector2 top, float floor, bool active, bool locked, float clock)
        {
            transform.position = new Vector3(top.x, floor, transform.position.z);
            groundAnchor = new Vector2(top.x, floor);
            float height = Mathf.Max(.1f, top.y - floor);
            Sprite sprite = active ? activeSprite : warningFrames != null && warningFrames.Length > 0
                ? warningFrames[(int)(clock * 8f) % warningFrames.Length] : activeSprite;
            if (appearance == null) appearance = GetComponentInParent<EnemyColorVariants>();
            if (appearance != null) sprite = appearance.ResolveProjectileSprite(sprite);
            if (visual != null && sprite != null)
            {
                visual.sprite = sprite;
                visual.enabled = true;
                visual.color = active ? Color.white : locked ? new Color(1f, .85f, .25f, .95f) : new Color(1f, 1f, 1f, .55f);
                visual.transform.localScale = new Vector3((active ? activeWidth : warningWidth) / sprite.bounds.size.x,
                    height / sprite.bounds.size.y, 1f);
                if (sourcePoint != null) MatchVisualTop(sourcePoint.position.y);
            }
            damageCollider.size = new Vector2(activeWidth, height);
            damageCollider.offset = Vector2.up * height * .5f;
            if (damaging != active)
            {
                damaging = active;
                damage.SetHitboxWindowActive(active);
            }
            damageCollider.enabled = active;
        }

        public void MatchVisualTop(float topY)
        {
            if (visual == null || !visual.enabled || visual.sprite == null) return;
            Vector3 scale = visual.transform.localScale;
            scale.y = Mathf.Max(.1f, topY - groundAnchor.y) / visual.sprite.bounds.size.y;
            visual.transform.localScale = scale;
        }

        public void Hide()
        {
            damaging = false;
            if (damage != null) damage.SetHitboxWindowActive(false);
            if (damageCollider != null) damageCollider.enabled = false;
            if (visual != null) visual.enabled = false;
        }

        private void OnDisable() => Hide();

        private void LateUpdate()
        {
            if (visual != null && visual.enabled)
            {
                transform.position = new Vector3(groundAnchor.x, groundAnchor.y, transform.position.z);
                if (sourcePoint != null) MatchVisualTop(sourcePoint.position.y);
            }
        }
    }
}
