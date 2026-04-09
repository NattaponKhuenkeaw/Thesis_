using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyWorldUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyController enemyController;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text damageText;

    [Header("Damage Text — Random Position")]
    [Tooltip("Min/Max X offset for random spawn position on the canvas.")]
    [SerializeField] private Vector2 damageTextRandomRangeX = new Vector2(-40f, 40f);
    [Tooltip("Min/Max Y offset for random spawn position on the canvas.")]
    [SerializeField] private Vector2 damageTextRandomRangeY = new Vector2(-10f, 30f);
    [Tooltip("Float direction applied on top of the random start position.")]
    [SerializeField] private Vector2 damageTextFloatOffset = new Vector2(0f, 50f);
    [SerializeField] private float damageTextDuration = 0.5f;
    [SerializeField] private bool hideCanvasOnDeath = true;

    [Header("Impact Scaling")]
    [Tooltip("Text scale for BasicCombo tier hits (smallest).")]
    [SerializeField] private float basicComboScale = 1.0f;
    [Tooltip("Text scale for Skill tier hits (medium).")]
    [SerializeField] private float skillScale = 1.4f;
    [Tooltip("Text scale for Ultimate tier hits (biggest).")]
    [SerializeField] private float ultimateScale = 2.0f;
    [Tooltip("Overshoot multiplier for the punch-pop effect at start.")]
    [SerializeField] private float punchScaleOvershoot = 1.3f;

    private RectTransform damageTextRect;
    private Coroutine damageTextCoroutine;
    private Color damageTextBaseColor = Color.white;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();

        if (damageText != null)
        {
            damageTextRect = damageText.rectTransform;
            damageTextBaseColor = damageText.color;
            damageText.gameObject.SetActive(false);
        }

        UpdateHealthBar();
    }

    private void OnEnable()
    {
        SubscribeToEnemyEvents();
        UpdateHealthBar();
    }

    private void OnDisable()
    {
        UnsubscribeFromEnemyEvents();
    }

    private void LateUpdate()
    {
        KeepCanvasFacingForward();
    }

    private void CacheReferences()
    {
        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();

        if (healthSlider == null)
            healthSlider = GetComponentInChildren<Slider>(true);

        if (damageText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0)
                damageText = texts[0];
        }

        if (damageText != null)
            damageTextRect = damageText.rectTransform;
    }

    /// <summary>
    /// Counter-rotate so UI always faces the camera regardless of parent Y-rotation.
    /// </summary>
    private void KeepCanvasFacingForward()
    {
        // Force the UI to always stay upright and non-mirrored in world space
        transform.rotation = Quaternion.identity;
    }

    private void SubscribeToEnemyEvents()
    {
        if (enemyController == null)
            return;

        enemyController.OnDamaged -= HandleDamaged;
        enemyController.OnDamaged += HandleDamaged;
        enemyController.OnDied -= HandleDied;
        enemyController.OnDied += HandleDied;
    }

    private void UnsubscribeFromEnemyEvents()
    {
        if (enemyController == null)
            return;

        enemyController.OnDamaged -= HandleDamaged;
        enemyController.OnDied -= HandleDied;
    }

    private void HandleDamaged(HitReactionData data)
    {
        UpdateHealthBar();
        ShowDamageText(data.damage, data.impactTier);
    }

    private void HandleDied()
    {
        UpdateHealthBar();

        if (hideCanvasOnDeath)
            gameObject.SetActive(false);
    }

    private void UpdateHealthBar()
    {
        if (healthSlider == null || enemyController == null)
            return;

        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;

        float normalizedHp = enemyController.maxHP > 0f
            ? Mathf.Clamp01(enemyController.currentHP / enemyController.maxHP)
            : 0f;

        healthSlider.value = normalizedHp;
    }

    private void ShowDamageText(float damage, DamageImpactTier impactTier)
    {
        if (damageText == null)
            return;

        if (damageTextCoroutine != null)
            StopCoroutine(damageTextCoroutine);

        if (damageTextRect == null)
            damageTextRect = damageText.rectTransform;

        // --- Random start position ---
        Vector2 randomStart = new Vector2(
            Random.Range(damageTextRandomRangeX.x, damageTextRandomRangeX.y),
            Random.Range(damageTextRandomRangeY.x, damageTextRandomRangeY.y));

        // --- Impact tier scale ---
        float targetScale = impactTier switch
        {
            DamageImpactTier.Skill    => skillScale,
            DamageImpactTier.Ultimate => ultimateScale,
            _                         => basicComboScale
        };

        damageText.text = Mathf.RoundToInt(damage).ToString();
        damageText.color = damageTextBaseColor;
        damageTextRect.anchoredPosition = randomStart;
        damageText.gameObject.SetActive(true);

        damageTextCoroutine = StartCoroutine(DamageTextRoutine(randomStart, targetScale));
    }

    private IEnumerator DamageTextRoutine(Vector2 startPos, float targetScale)
    {
        float elapsed = 0f;
        Vector2 endPos = startPos + damageTextFloatOffset;

        // Start with punch overshoot and ease down to target scale
        float punchStart = targetScale * punchScaleOvershoot;

        while (elapsed < damageTextDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageTextDuration);

            // Position: lerp from random start to float end
            if (damageTextRect != null)
                damageTextRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            // Scale: punch pop ease-out (overshoot → target → shrink away)
            float scaleT = Mathf.Clamp01(t * 3f); // fast punch in first third
            float currentScale = Mathf.Lerp(punchStart, targetScale, EaseOutBack(scaleT));
            // Shrink slightly toward end
            float shrink = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01((t - 0.5f) * 2f));
            damageTextRect.localScale = Vector3.one * currentScale * shrink;

            // Alpha: fade out
            Color color = damageTextBaseColor;
            color.a = 1f - t;
            damageText.color = color;

            yield return null;
        }

        damageText.color = damageTextBaseColor;
        damageTextRect.localScale = Vector3.one;
        damageText.gameObject.SetActive(false);
        damageTextCoroutine = null;
    }

    /// <summary>
    /// Ease-out-back curve for satisfying punch pop.
    /// </summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
