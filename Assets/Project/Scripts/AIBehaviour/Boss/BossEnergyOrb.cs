using System;
using UnityEngine;

namespace Project.AIBehaviour.Boss
{
    /// <summary>
    /// Homing reward orb spawned on boss death. Pops outward, then lerps to the player.
    /// </summary>
    public sealed class BossEnergyOrb : MonoBehaviour
    {
        public event Action OnAbsorbed;

        [Header("Burst")]
        [SerializeField] private float burstDuration = 0.28f;
        [SerializeField] private float burstDistance = 1.15f;
        [SerializeField] private float burstHeight = 0.85f;

        [Header("Homing")]
        [SerializeField] private float homingDuration = 0.7f;
        [SerializeField] private float arriveDistance = 0.35f;
        [SerializeField] private AnimationCurve homingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Optional Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float spinDegreesPerSecond = 360f;
        [SerializeField] private float pulseAmplitude = 0.12f;
        [SerializeField] private float pulseSpeed = 8f;

        private Transform target;
        private Vector3 startPosition;
        private Vector3 burstPeak;
        private float elapsed;
        private bool absorbed;
        private Phase phase = Phase.Burst;
        private Vector3 baseScale = Vector3.one;

        private enum Phase
        {
            Burst,
            Homing
        }

        public void Launch(Transform homingTarget, Vector3 spawnPosition)
        {
            target = homingTarget;
            transform.position = spawnPosition;
            startPosition = spawnPosition;

            Vector2 randomDir = UnityEngine.Random.insideUnitCircle;
            if (randomDir.sqrMagnitude < 0.01f)
                randomDir = Vector2.up;
            randomDir.Normalize();

            burstPeak = spawnPosition
                + new Vector3(randomDir.x * burstDistance, Mathf.Abs(randomDir.y) * burstHeight + burstHeight * 0.35f, 0f);

            elapsed = 0f;
            absorbed = false;
            phase = Phase.Burst;
            baseScale = transform.localScale;

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            EnsureFallbackVisual();
        }

        public void ConfigureTiming(float burstSeconds, float homingSeconds, float burstRange, float height)
        {
            burstDuration = Mathf.Max(0.05f, burstSeconds);
            homingDuration = Mathf.Max(0.05f, homingSeconds);
            burstDistance = Mathf.Max(0.1f, burstRange);
            burstHeight = Mathf.Max(0.1f, height);
        }

        private void Update()
        {
            if (absorbed)
                return;

            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            if (spinDegreesPerSecond != 0f)
                transform.Rotate(0f, 0f, spinDegreesPerSecond * dt, Space.Self);

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude;
            transform.localScale = baseScale * pulse;

            if (phase == Phase.Burst)
            {
                float t = Mathf.Clamp01(elapsed / burstDuration);
                // Ease-out pop.
                float eased = 1f - (1f - t) * (1f - t);
                transform.position = Vector3.LerpUnclamped(startPosition, burstPeak, eased);

                if (t >= 1f)
                {
                    phase = Phase.Homing;
                    elapsed = 0f;
                    startPosition = transform.position;
                }

                return;
            }

            if (target == null)
            {
                Absorb();
                return;
            }

            float homingT = Mathf.Clamp01(elapsed / homingDuration);
            float curveT = homingCurve != null ? homingCurve.Evaluate(homingT) : homingT;
            Vector3 destination = target.position;
            transform.position = Vector3.LerpUnclamped(startPosition, destination, curveT);

            if (homingT >= 1f || (transform.position - destination).sqrMagnitude <= arriveDistance * arriveDistance)
                Absorb();
        }

        private void Absorb()
        {
            if (absorbed)
                return;

            absorbed = true;
            OnAbsorbed?.Invoke();
            Destroy(gameObject);
        }

        private void EnsureFallbackVisual()
        {
            if (spriteRenderer != null)
                return;

            GameObject visual = new GameObject("OrbSprite");
            visual.transform.SetParent(transform, false);
            spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateFallbackSprite();
            spriteRenderer.color = new Color(1f, 0.85f, 0.25f, 0.95f);
            spriteRenderer.sortingOrder = 40;
            transform.localScale = Vector3.one * 0.55f;
            baseScale = transform.localScale;
        }

        private static Sprite CreateFallbackSprite()
        {
            const int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color fill = Color.white;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    tex.SetPixel(x, y, dist <= radius ? fill : clear);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
