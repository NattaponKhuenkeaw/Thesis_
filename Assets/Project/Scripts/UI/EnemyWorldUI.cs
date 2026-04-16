using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyWorldUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyController enemyController;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider damageLerpSlider;
    [SerializeField] private TMP_Text damageText;

    [Header("Damage Text — Random Position")]
    [Tooltip("Min/Max X offset for random spawn position on the canvas.")]
    [SerializeField] private Vector2 damageTextRandomRangeX = new Vector2(-40f, 40f);
    [Tooltip("Min/Max Y offset for random spawn position on the canvas.")]
    [SerializeField] private Vector2 damageTextRandomRangeY = new Vector2(-10f, 30f);
    [Tooltip("Float direction applied on top of the random start position.")]
    [SerializeField] private Vector2 damageTextFloatOffset = new Vector2(0f, 50f);
    
    [Header("Damage Text Flow Timing")]
    [SerializeField] private float damagePopDuration = 0.1f;
    [SerializeField] private float damageScaleDownDuration = 0.15f;
    [SerializeField] private float damageFreezeDuration = 0.4f;
    [SerializeField] private float damageFadeDuration = 0.3f;
    
    [Header("HP Lerp Settings")]
    [SerializeField] private float healthLerpDelay = 0.25f;
    [SerializeField] private float healthLerpDuration = 0.5f;

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
    private Coroutine healthLerpCoroutine;
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

        if (damageLerpSlider == null)
        {
            // Try to find by name "HPLerp" or "Ghost" first
            Slider[] sliders = GetComponentsInChildren<Slider>(true);
            foreach (var s in sliders)
            {
                if (s == healthSlider) continue;
                if (s.name.Contains("Lerp") || s.name.Contains("Ghost"))
                {
                    damageLerpSlider = s;
                    break;
                }
            }

            // Fallback to second slider
            if (damageLerpSlider == null && sliders.Length > 1) 
                damageLerpSlider = sliders[1];
        }

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
        // Force the UI to always stay upright in world space.
        // This handles both parent rotation and parent scale-flipping without jitter.
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

        // Instant update for the main green bar
        healthSlider.value = normalizedHp;

        // Smooth lerp for the back red bar
        if (damageLerpSlider != null)
        {
            damageLerpSlider.minValue = 0f;
            damageLerpSlider.maxValue = 1f;

            if (healthLerpCoroutine != null) StopCoroutine(healthLerpCoroutine);
            healthLerpCoroutine = StartCoroutine(HealthLerpRoutine(normalizedHp));
        }
    }

    private IEnumerator HealthLerpRoutine(float targetValue)
    {
        // Short pause before the ghost bar starts following
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
            DamageImpactTier.Medium   => skillScale,
            DamageImpactTier.Heavy    => skillScale * 1.3f, // Medium -> Heavy boost
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
        float punchStart = targetScale * punchScaleOvershoot;
        Color color = damageTextBaseColor;
        
        // --- STAGE 1: Impact Pop ---
        float elapsed = 0f;
        while (elapsed < damagePopDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damagePopDuration);
            damageTextRect.localScale = Vector3.one * Mathf.Lerp(1f, punchStart, t);
            damageTextRect.anchoredPosition = startPos;
            yield return null;
        }

        // --- STAGE 2: Scale Down ---
        elapsed = 0f;
        while (elapsed < damageScaleDownDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageScaleDownDuration);
            damageTextRect.localScale = Vector3.one * Mathf.Lerp(punchStart, targetScale, EaseOutBack(t));
            damageTextRect.anchoredPosition = startPos;
            yield return null;
        }

        // --- STAGE 3: Freeze ---
        damageTextRect.localScale = Vector3.one * targetScale;
        damageTextRect.anchoredPosition = startPos;
        yield return new WaitForSeconds(damageFreezeDuration);

        // --- STAGE 4: Fade Away Upward ---
        elapsed = 0f;
        Vector2 endPos = startPos + damageTextFloatOffset;
        while (elapsed < damageFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageFadeDuration);
            
            damageTextRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            
            color.a = 1f - t;
            damageText.color = color;
            
            // Subtle shrink during fade
            damageTextRect.localScale = Vector3.one * targetScale * Mathf.Lerp(1f, 0.8f, t);
            
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
