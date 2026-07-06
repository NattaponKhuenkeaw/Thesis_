using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private TMP_Text critDamageText;

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

    [Header("Attack Warning")]
    [SerializeField] private GameObject attackWarningIcon;
    [SerializeField] private float defaultAttackWarningDuration = 0.5f;

    [Header("Impact Scaling")]
    [Tooltip("Text scale for BasicCombo tier hits (smallest).")]
    [SerializeField] private float basicComboScale = 1.0f;
    [Tooltip("Text scale for Skill tier hits (medium).")]
    [SerializeField] private float skillScale = 1.4f;
    [Tooltip("Text scale for Ultimate tier hits (biggest).")]
    [SerializeField] private float ultimateScale = 2.0f;
    [Tooltip("Overshoot multiplier for the punch-pop effect at start.")]
    [SerializeField] private float punchScaleOvershoot = 1.02f;

    [Header("Critical Hit Styling")]
    [Tooltip("Extra scale multiplier applied when the hit is a crit.")]
    [SerializeField] private float critScaleMultiplier = 1.15f;
    [Tooltip("Color used for crit damage text.")]
    [SerializeField] private Color critTextColor = new Color(1f, 0.85f, 0.2f);
    [Tooltip("Random rotation range applied to crit damage popups.")]
    [SerializeField] private float critRotationRange = 15f;

    private RectTransform damageTextRect;
    private RectTransform critDamageTextRect;
    private List<TMP_Text> damageTextPool = new List<TMP_Text>();
    private List<TMP_Text> critDamageTextPool = new List<TMP_Text>();
    private Coroutine healthLerpCoroutine;
    private Coroutine attackWarningCoroutine;
    private Color damageTextBaseColor = Color.white;
    private Color critDamageTextBaseColor = Color.white;
    private Vector3 baseLocalScale = Vector3.one;



    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        baseLocalScale = new Vector3(
            Mathf.Abs(transform.localScale.x),
            Mathf.Abs(transform.localScale.y),
            Mathf.Abs(transform.localScale.z));

        if (damageText != null)
        {
            damageTextRect = damageText.rectTransform;
            damageTextBaseColor = damageText.color;
            damageText.gameObject.SetActive(false);
            damageTextPool.Add(damageText);
        }

        if (critDamageText != null)
        {
            critDamageTextRect = critDamageText.rectTransform;
            critDamageTextBaseColor = critDamageText.color;
            critDamageText.gameObject.SetActive(false);
            critDamageTextPool.Add(critDamageText);
        }

        if (attackWarningIcon != null)
            attackWarningIcon.SetActive(false);

        UpdateHealthBar();
    }

    private void OnEnable()
    {
        SubscribeToEnemyEvents();
        UpdateHealthBar();
    }

    private void OnDisable()
    {
        HideAttackWarning();
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

        if (critDamageText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null
                    && text != damageText
                    && text.name.Contains("Crit"))
                {
                    critDamageText = text;
                    break;
                }
            }
        }

        if (critDamageText != null)
            critDamageTextRect = critDamageText.rectTransform;
    }

    /// <summary>
    /// Counter-rotate so UI always faces the camera regardless of parent Y-rotation.
    /// </summary>
    private void KeepCanvasFacingForward()
    {
        // Force the UI to always stay upright in world space.
        // This handles both parent rotation and parent scale-flipping without jitter.
        transform.rotation = Quaternion.identity;

        Transform parent = transform.parent;
        if (parent == null)
        {
            transform.localScale = baseLocalScale;
            return;
        }

        Vector3 parentLossyScale = parent.lossyScale;
        transform.localScale = new Vector3(
            baseLocalScale.x * (parentLossyScale.x < 0f ? -1f : 1f),
            baseLocalScale.y * (parentLossyScale.y < 0f ? -1f : 1f),
            baseLocalScale.z);
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
        if (data.damage <= 0f)
        {
            return;
        }

        ShowDamageText(data.damage, data.impactTier, data.elementType, data.isCriticalHit);
    }

    private void HandleDied()
    {
        HideAttackWarning();
        UpdateHealthBar();

        if (hideCanvasOnDeath)
            gameObject.SetActive(false);
    }

    public void ShowAttackWarning(float duration = -1f)
    {
        if (attackWarningIcon == null)
            return;

        float resolvedDuration = duration > 0f ? duration : defaultAttackWarningDuration;

        if (attackWarningCoroutine != null)
            StopCoroutine(attackWarningCoroutine);

        attackWarningCoroutine = StartCoroutine(AttackWarningRoutine(resolvedDuration));
    }

    public void HideAttackWarning()
    {
        if (attackWarningCoroutine != null)
        {
            StopCoroutine(attackWarningCoroutine);
            attackWarningCoroutine = null;
        }

        if (attackWarningIcon != null)
            attackWarningIcon.SetActive(false);
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

    private IEnumerator AttackWarningRoutine(float duration)
    {
        attackWarningIcon.SetActive(true);
        yield return new WaitForSeconds(duration);
        attackWarningIcon.SetActive(false);
        attackWarningCoroutine = null;
    }

    private void ShowDamageText(float damage, DamageImpactTier impactTier, ElementType elementType, bool isCriticalHit)
    {
        if (damage <= 0f) return;

        TMP_Text t = GetPooledText(isCriticalHit);
        if (t == null) return;

        RectTransform r = t.rectTransform;
        
        // Ensure new text is rendered on top
        r.SetAsLastSibling();

        // --- Random start position ---
        Vector2 randomStart = new Vector2(
            Random.Range(damageTextRandomRangeX.x, damageTextRandomRangeX.y),
            Random.Range(damageTextRandomRangeY.x, damageTextRandomRangeY.y));

        // --- Impact tier scale ---
        float targetScale = impactTier switch
        {
            DamageImpactTier.Medium   => skillScale,
            DamageImpactTier.Heavy    => skillScale * 1.3f, 
            DamageImpactTier.Ultimate => ultimateScale,
            _                         => basicComboScale
        };

        // --- Elemental Color ---
        Color elementColor = Color.white;
        var db = ElementDatabase.Load();
        if (db != null)
        {
            elementColor = db.GetDamageColor(elementType);
        }

        if (isCriticalHit)
        {
            targetScale *= critScaleMultiplier;
            elementColor = critDamageText != null ? critDamageTextBaseColor : critTextColor;
            t.text = Mathf.RoundToInt(damage).ToString();
            t.fontStyle = FontStyles.Bold;
        }
        else
        {
            t.text = Mathf.RoundToInt(damage).ToString();
            t.fontStyle = FontStyles.Normal;
        }

        t.color = elementColor;
        r.anchoredPosition = randomStart;
        r.localRotation = isCriticalHit
            ? Quaternion.Euler(0f, 0f, Random.Range(-critRotationRange, critRotationRange))
            : Quaternion.identity;
        t.gameObject.SetActive(true);

        StartCoroutine(DamageTextRoutine(t, randomStart, targetScale, elementColor));
    }

    private TMP_Text GetPooledText(bool isCriticalHit)
    {
        bool useCritText = isCriticalHit && critDamageText != null;
        List<TMP_Text> targetPool = useCritText ? critDamageTextPool : damageTextPool;
        TMP_Text template = useCritText ? critDamageText : damageText;

        for (int i = 0; i < targetPool.Count; i++)
        {
            if (!targetPool[i].gameObject.activeSelf)
                return targetPool[i];
        }

        // Expand pool if needed (cap at 15 for safety)
        if (targetPool.Count < 15 && template != null)
        {
            TMP_Text newText = Instantiate(template, template.transform.parent);
            targetPool.Add(newText);
            return newText;
        }

        return null;
    }

    private IEnumerator DamageTextRoutine(TMP_Text textInstance, Vector2 startPos, float targetScale, Color baseColor)
    {
        RectTransform rect = textInstance.rectTransform;
        float punchStart = targetScale * punchScaleOvershoot;
        Color color = baseColor;
        
        // --- STAGE 1: Impact Pop ---
        float elapsed = 0f;
        while (elapsed < damagePopDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damagePopDuration);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, punchStart, t);
            rect.anchoredPosition = startPos;
            yield return null;
        }

        // --- STAGE 2: Scale Down ---
        elapsed = 0f;
        while (elapsed < damageScaleDownDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageScaleDownDuration);
            rect.localScale = Vector3.one * Mathf.Lerp(punchStart, targetScale, EaseOutBack(t));
            rect.anchoredPosition = startPos;
            yield return null;
        }

        // --- STAGE 3: Freeze ---
        rect.localScale = Vector3.one * targetScale;
        rect.anchoredPosition = startPos;
        yield return new WaitForSeconds(damageFreezeDuration);

        // --- STAGE 4: Fade Away Upward ---
        elapsed = 0f;
        Vector2 endPos = startPos + damageTextFloatOffset;
        while (elapsed < damageFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageFadeDuration);
            
            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            
            color.a = 1f - t;
            textInstance.color = color;
            
            // Subtle shrink during fade
            rect.localScale = Vector3.one * targetScale * Mathf.Lerp(1f, 0.8f, t);
            
            yield return null;
        }

        textInstance.fontStyle = FontStyles.Normal;
        rect.localRotation = Quaternion.identity;
        textInstance.gameObject.SetActive(false);
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
