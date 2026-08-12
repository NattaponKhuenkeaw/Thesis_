using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// Screen-space boss HP bar (top-center). Bound by BossPatternController when a boss spawns.
    /// Builds its panel at runtime so combat_1 does not need a pre-authored prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossHealthHUD : MonoBehaviour
    {
        private const int CanvasSortingOrder = 400;
        private const string TransitionCanvasName = "TransitionCanvas";

        public static BossHealthHUD Instance { get; private set; }

        [SerializeField] private float healthLerpDelay = 0.25f;
        [SerializeField] private float healthLerpDuration = 0.5f;

        private EnemyController boundBoss;
        private GameObject panelRoot;
        private Slider healthSlider;
        private Slider damageLerpSlider;
        private TMP_Text nameText;
        private Coroutine healthLerpCoroutine;
        private bool uiBuilt;

        public bool IsBound => boundBoss != null;
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureUiBuilt();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            UnbindInternal(clearOnly: true);

            if (panelRoot != null)
            {
                Destroy(panelRoot);
                panelRoot = null;
            }
        }

        public static BossHealthHUD EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            BossHealthHUD existing = FindFirstObjectByType<BossHealthHUD>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject go = new GameObject("BossHealthHUD");
            return go.AddComponent<BossHealthHUD>();
        }

        public void Bind(EnemyController boss, string displayName)
        {
            if (boss == null)
            {
                Debug.LogWarning("[BossHealthHUD] Bind called with null boss.", this);
                return;
            }

            EnsureUiBuilt();

            if (boundBoss != null && boundBoss != boss)
                UnbindInternal(clearOnly: false);

            boundBoss = boss;
            boundBoss.OnDamaged -= HandleDamaged;
            boundBoss.OnDamaged += HandleDamaged;
            boundBoss.OnDied -= HandleDied;
            boundBoss.OnDied += HandleDied;

            if (nameText != null)
                nameText.text = string.IsNullOrWhiteSpace(displayName) ? boss.name : displayName;

            UpdateHealthBar(immediateLerp: true);
            SetVisible(boss.IsAlive);
        }

        public void RefreshIfBound(EnemyController boss)
        {
            if (boss == null || boundBoss != boss)
            {
                return;
            }

            UpdateHealthBar(immediateLerp: true);
            SetVisible(boss.IsAlive);
        }

        public void Unbind(EnemyController boss)
        {
            if (boss == null || boundBoss != boss)
                return;

            UnbindInternal(clearOnly: false);
        }

        public void Clear()
        {
            UnbindInternal(clearOnly: false);
        }

        private void UnbindInternal(bool clearOnly)
        {
            if (boundBoss != null)
            {
                boundBoss.OnDamaged -= HandleDamaged;
                boundBoss.OnDied -= HandleDied;
                boundBoss = null;
            }

            StopHealthLerp();

            if (!clearOnly)
                SetVisible(false);
        }

        private void HandleDamaged(HitReactionData _)
        {
            UpdateHealthBar(immediateLerp: false);
        }

        private void HandleDied()
        {
            UpdateHealthBar(immediateLerp: false);
            SetVisible(false);
        }

        private void UpdateHealthBar(bool immediateLerp)
        {
            if (healthSlider == null || boundBoss == null)
                return;

            float normalizedHp = boundBoss.maxHP > 0f
                ? Mathf.Clamp01(boundBoss.currentHP / boundBoss.maxHP)
                : 0f;

            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value = normalizedHp;

            if (damageLerpSlider == null)
                return;

            damageLerpSlider.minValue = 0f;
            damageLerpSlider.maxValue = 1f;

            StopHealthLerp();

            if (immediateLerp)
            {
                damageLerpSlider.value = normalizedHp;
                return;
            }

            healthLerpCoroutine = StartCoroutine(HealthLerpRoutine(normalizedHp));
        }

        private IEnumerator HealthLerpRoutine(float targetValue)
        {
            yield return new WaitForSeconds(healthLerpDelay);

            float elapsed = 0f;
            float startValue = damageLerpSlider.value;

            while (elapsed < healthLerpDuration)
            {
                elapsed += Time.deltaTime;
                damageLerpSlider.value = Mathf.Lerp(startValue, targetValue, elapsed / healthLerpDuration);
                yield return null;
            }

            damageLerpSlider.value = targetValue;
            healthLerpCoroutine = null;
        }

        private void StopHealthLerp()
        {
            if (healthLerpCoroutine == null)
                return;

            StopCoroutine(healthLerpCoroutine);
            healthLerpCoroutine = null;
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot != null)
                panelRoot.SetActive(visible);
        }

        private void EnsureUiBuilt()
        {
            if (uiBuilt && panelRoot != null)
                return;

            Transform canvasTransform = ResolveOrCreateCanvas();
            BuildPanel(canvasTransform);
            uiBuilt = true;
        }

        private Transform ResolveOrCreateCanvas()
        {
            Canvas preferred = null;
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                if (canvas.name == TransitionCanvasName || canvas.name.Contains("Transition"))
                    continue;
                if (canvas.name == "AdminCanvas")
                {
                    preferred = canvas;
                    break;
                }

                if (preferred == null)
                    preferred = canvas;
            }

            if (preferred != null)
                return preferred.transform;

            GameObject canvasGo = new GameObject("BossHealthHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas created = canvasGo.AddComponent<Canvas>();
            created.renderMode = RenderMode.ScreenSpaceOverlay;
            created.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo.transform;
        }

        private void BuildPanel(Transform parent)
        {
            if (panelRoot != null)
                Destroy(panelRoot);

            panelRoot = CreateRect("BossHealthBar", parent);
            RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -28f);
            panelRect.sizeDelta = new Vector2(920f, 72f);

            Image panelBg = panelRoot.AddComponent<Image>();
            panelBg.color = new Color(0.05f, 0.05f, 0.07f, 0.72f);
            panelBg.raycastTarget = false;

            nameText = CreateText("BossName", panelRoot.transform, "Boss", 22f, FontStyles.Bold);
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -4f);
            nameRect.sizeDelta = new Vector2(-24f, 28f);
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = new Color(0.95f, 0.9f, 0.82f, 1f);

            GameObject barRoot = CreateRect("HpBar", panelRoot.transform);
            RectTransform barRect = barRoot.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 10f);
            barRect.sizeDelta = new Vector2(-36f, 22f);

            GameObject track = CreateRect("Track", barRoot.transform);
            RectTransform trackRect = track.GetComponent<RectTransform>();
            StretchToParent(trackRect);
            Image trackImage = track.AddComponent<Image>();
            trackImage.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);
            trackImage.raycastTarget = false;

            damageLerpSlider = CreateBarSlider("HealthLerp", barRoot.transform, new Color(0.75f, 0.55f, 0.2f, 0.95f));
            healthSlider = CreateBarSlider("Health", barRoot.transform, new Color(0.78f, 0.12f, 0.14f, 1f));
        }

        private static Slider CreateBarSlider(string name, Transform parent, Color fillColor)
        {
            GameObject root = CreateRect(name, parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            StretchToParent(rootRect);

            Slider slider = root.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            GameObject fillArea = CreateRect("Fill Area", root.transform);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            StretchToParent(fillAreaRect);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            GameObject fill = CreateRect("Fill", fillArea.transform);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            StretchToParent(fillRect);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;

            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float size, FontStyles style)
        {
            GameObject go = CreateRect(name, parent);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
