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

    [Header("HP Lerp Settings")]
    [SerializeField] private float healthLerpDelay = 0.25f;
    [SerializeField] private float healthLerpDuration = 0.5f;

    [SerializeField] private bool hideCanvasOnDeath = true;
    [Tooltip("When false, world HP sliders stay hidden (boss uses screen HUD instead). Attack warning still works.")]
    [SerializeField] private bool showHealthBar = true;

    [Header("Attack Warning")]
    [SerializeField] private GameObject attackWarningIcon;
    [SerializeField] private float defaultAttackWarningDuration = 0.5f;
    [SerializeField] private Color normalWarningColor = Color.white;
    [SerializeField] private Color undodgeableWarningColor = new Color(0.72f, 0.25f, 1f, 1f);

    private Coroutine healthLerpCoroutine;
    private Coroutine attackWarningCoroutine;
    private Vector3 baseLocalScale = Vector3.one;
    private Graphic attackWarningGraphic;
    private SpriteRenderer attackWarningSprite;

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

        HideTextTemplates();

        if (attackWarningIcon != null)
        {
            attackWarningIcon.SetActive(false);
        }

        ApplyHealthBarVisibility();
        UpdateHealthBar();
    }

    private void OnEnable()
    {
        SubscribeToEnemyEvents();
        ApplyHealthBarVisibility();
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
        {
            enemyController = GetComponentInParent<EnemyController>();
        }

        if (healthSlider == null)
        {
            healthSlider = GetComponentInChildren<Slider>(true);
        }

        if (damageLerpSlider == null)
        {
            Slider[] sliders = GetComponentsInChildren<Slider>(true);
            foreach (Slider slider in sliders)
            {
                if (slider == healthSlider)
                {
                    continue;
                }

                if (slider.name.Contains("Lerp") || slider.name.Contains("Ghost"))
                {
                    damageLerpSlider = slider;
                    break;
                }
            }

            if (damageLerpSlider == null && sliders.Length > 1)
            {
                damageLerpSlider = sliders[1];
            }
        }

        if (damageText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || text.name.Contains("Crit"))
                {
                    continue;
                }

                damageText = text;
                break;
            }
        }
    }

    private void HideTextTemplates()
    {
        if (damageText != null)
        {
            damageText.gameObject.SetActive(false);
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && text.name.Contains("Crit"))
            {
                text.gameObject.SetActive(false);
            }
        }
    }

    private void KeepCanvasFacingForward()
    {
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
        {
            return;
        }

        enemyController.OnDamaged -= HandleDamaged;
        enemyController.OnDamaged += HandleDamaged;
        enemyController.OnDied -= HandleDied;
        enemyController.OnDied += HandleDied;
    }

    private void UnsubscribeFromEnemyEvents()
    {
        if (enemyController == null)
        {
            return;
        }

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

        ShowDamageText(data);
    }

    private void HandleDied()
    {
        HideAttackWarning();
        UpdateHealthBar();

        if (hideCanvasOnDeath)
        {
            gameObject.SetActive(false);
        }
    }

    public void ShowAttackWarning(float duration = -1f)
    {
        ShowAttackWarning(duration, AttackWarningStyle.Normal);
    }

    public void ShowAttackWarning(float duration, AttackWarningStyle style)
    {
        if (attackWarningIcon == null)
        {
            return;
        }

        float resolvedDuration = duration > 0f ? duration : defaultAttackWarningDuration;
        ApplyWarningStyle(style);

        if (attackWarningCoroutine != null)
        {
            StopCoroutine(attackWarningCoroutine);
        }

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
        {
            attackWarningIcon.SetActive(false);
        }
    }

    public void RefreshHealthDisplay()
    {
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (!showHealthBar || healthSlider == null || enemyController == null)
        {
            return;
        }

        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;

        float normalizedHp = enemyController.maxHP > 0f
            ? Mathf.Clamp01(enemyController.currentHP / enemyController.maxHP)
            : 0f;

        healthSlider.value = normalizedHp;

        if (damageLerpSlider != null)
        {
            damageLerpSlider.minValue = 0f;
            damageLerpSlider.maxValue = 1f;

            if (healthLerpCoroutine != null)
            {
                StopCoroutine(healthLerpCoroutine);
            }

            healthLerpCoroutine = StartCoroutine(HealthLerpRoutine(normalizedHp));
        }
    }

    private void ApplyHealthBarVisibility()
    {
        if (healthSlider != null)
        {
            healthSlider.gameObject.SetActive(showHealthBar);
        }

        if (damageLerpSlider != null)
        {
            damageLerpSlider.gameObject.SetActive(showHealthBar);
        }
    }

    /// <summary>
    /// Bosses use screen-space HUD; call with false to hide world HP while keeping attack warning.
    /// </summary>
    public void SetShowHealthBar(bool visible)
    {
        showHealthBar = visible;
        ApplyHealthBarVisibility();
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

    private void ApplyWarningStyle(AttackWarningStyle style)
    {
        Color color = style == AttackWarningStyle.Undodgeable
            ? undodgeableWarningColor
            : normalWarningColor;

        if (attackWarningGraphic == null && attackWarningIcon != null)
            attackWarningGraphic = attackWarningIcon.GetComponentInChildren<Graphic>(true);

        if (attackWarningSprite == null && attackWarningIcon != null)
            attackWarningSprite = attackWarningIcon.GetComponentInChildren<SpriteRenderer>(true);

        if (attackWarningGraphic != null)
            attackWarningGraphic.color = color;

        if (attackWarningSprite != null)
            attackWarningSprite.color = color;
    }

    private IEnumerator AttackWarningRoutine(float duration)
    {
        attackWarningIcon.SetActive(true);
        yield return new WaitForSeconds(duration);
        attackWarningIcon.SetActive(false);
        attackWarningCoroutine = null;
    }

    private void ShowDamageText(HitReactionData data)
    {
        WorldTextUtility.Show(new WorldTextRequest
        {
            source = transform,
            canvasRoot = transform,
            template = damageText,
            type = ResolveTextType(data.damageDelivery),
            amount = data.damage,
            damageDelivery = data.damageDelivery,
            impactTier = data.textImpactTier,
            elementType = data.elementType,
            isCritical = data.isCriticalHit
        });
    }

    private static WorldTextType ResolveTextType(DamageDelivery delivery)
    {
        return delivery switch
        {
            DamageDelivery.Dot => WorldTextType.Dot,
            DamageDelivery.Additional => WorldTextType.Additional,
            _ => WorldTextType.Damage
        };
    }
}
