using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>Prefab-authored HUD with the next boss health color visible beneath the current layer.</summary>
    [DisallowMultipleComponent]
    public sealed class BossHealthHUD : MonoBehaviour
    {
        private const string PrefabResourcePath = "UI/BossHealthHUD";
        public static BossHealthHUD Instance { get; private set; }

        [Header("Prefab References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider damageLerpSlider;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image nextLayerFill;
        [SerializeField] private Image damageTrailFill;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text remainingBarsText;

        [Header("Health Layers (last layer first)")]
        [SerializeField] private Color[] layerColors =
        {
            new Color(.9f, .18f, .24f),
            new Color(1f, .48f, .16f),
            new Color(1f, .8f, .22f),
            new Color(.3f, .85f, .42f),
            new Color(.18f, .8f, .9f)
        };
        [SerializeField] private float healthLerpDelay = .25f;
        [SerializeField] private float healthLerpDuration = .5f;

        private EnemyController boundBoss;
        private Coroutine healthLerpCoroutine;
        private int totalBars = 5;
        private int displayedBars;

        public bool IsBound => boundBoss != null;
        public bool IsVisible => panelRoot != null && panelRoot.activeInHierarchy;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnbindInternal();
        }

        private void OnDisable() => StopHealthLerp();

        private void OnEnable()
        {
            if (boundBoss != null) RefreshIfBound(boundBoss);
        }

        public static BossHealthHUD EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<BossHealthHUD>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            var prefab = Resources.Load<BossHealthHUD>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[BossHealthHUD] Missing Resources/UI/BossHealthHUD.prefab. No runtime UI will be generated.");
                return null;
            }
            Instance = Instantiate(prefab);
            return Instance;
        }

        public void Bind(EnemyController boss, string displayName, int healthBars = 5)
        {
            if (boss == null) return;
            UnbindInternal();
            boundBoss = boss;
            totalBars = Mathf.Max(1, healthBars);
            boss.OnDamaged += HandleDamaged;
            boss.OnDied += HandleDied;
            if (nameText != null)
                nameText.text = string.IsNullOrWhiteSpace(displayName) ? boss.name : displayName;
            RefreshIfBound(boss);
        }

        public void RefreshIfBound(EnemyController boss)
        {
            if (boss == null || boundBoss != boss) return;
            UpdateHealthBar(true);
            SetVisible(boss.IsAlive);
        }

        public void Unbind(EnemyController boss)
        {
            if (boss != null && boundBoss == boss) UnbindInternal();
        }

        public void Clear() => UnbindInternal();

        private void UnbindInternal()
        {
            if (boundBoss != null)
            {
                boundBoss.OnDamaged -= HandleDamaged;
                boundBoss.OnDied -= HandleDied;
                boundBoss = null;
            }
            StopHealthLerp();
            SetVisible(false);
        }

        private void HandleDamaged(HitReactionData _) => UpdateHealthBar(false);

        private void HandleDied()
        {
            UpdateHealthBar(true);
            SetVisible(false);
        }

        public static void CalculateLayers(float currentHp, float maxHp, int count, out int remaining, out float fill)
        {
            remaining = 0;
            fill = 0f;
            if (maxHp <= 0f || currentHp <= 0f) return;
            count = Mathf.Max(1, count);
            float layers = Mathf.Clamp(currentHp, 0f, maxHp) / (maxHp / count);
            remaining = Mathf.Clamp(Mathf.CeilToInt(layers), 1, count);
            fill = Mathf.Clamp01(layers - (remaining - 1));
        }

        private void UpdateHealthBar(bool immediateLerp)
        {
            if (healthSlider == null || boundBoss == null) return;
            CalculateLayers(boundBoss.currentHP, boundBoss.maxHP, totalBars, out int remaining, out float fill);
            bool changedLayer = remaining != displayedBars;
            displayedBars = remaining;
            healthSlider.SetValueWithoutNotify(fill);
            if (remainingBarsText != null) remainingBarsText.text = $"\u00d7{remaining}";
            bool hasColors = layerColors != null && layerColors.Length > 0;
            if (healthFill != null && hasColors)
                healthFill.color = LayerColor(remaining);
            if (nextLayerFill != null)
            {
                nextLayerFill.enabled = remaining > 1 && hasColors;
                if (nextLayerFill.enabled) nextLayerFill.color = LayerColor(remaining - 1);
            }
            // A white damage trail would hide the next color; keep it only on the final layer.
            if (damageTrailFill != null) damageTrailFill.enabled = remaining == 1;
            StopHealthLerp();
            if (damageLerpSlider == null) return;
            // A depleted layer immediately reveals the next color; never tween across a refill.
            if (immediateLerp || changedLayer || damageLerpSlider.value < fill
                || !Application.isPlaying || !isActiveAndEnabled)
                damageLerpSlider.SetValueWithoutNotify(fill);
            else
                healthLerpCoroutine = StartCoroutine(HealthLerpRoutine(fill));
        }

        private Color LayerColor(int remaining) => layerColors[Mathf.Clamp(remaining - 1, 0, layerColors.Length - 1)];

        private IEnumerator HealthLerpRoutine(float targetValue)
        {
            yield return new WaitForSeconds(healthLerpDelay);
            float start = damageLerpSlider.value;
            float elapsed = 0f;
            while (elapsed < healthLerpDuration)
            {
                elapsed += Time.deltaTime;
                damageLerpSlider.SetValueWithoutNotify(Mathf.Lerp(start, targetValue, elapsed / healthLerpDuration));
                yield return null;
            }
            damageLerpSlider.SetValueWithoutNotify(targetValue);
            healthLerpCoroutine = null;
        }

        private void StopHealthLerp()
        {
            if (healthLerpCoroutine == null) return;
            StopCoroutine(healthLerpCoroutine);
            healthLerpCoroutine = null;
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot != null) panelRoot.SetActive(visible);
        }
    }
}
