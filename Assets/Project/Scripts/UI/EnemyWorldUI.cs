using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
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
    private Graphic attackWarningGraphic;
    private SpriteRenderer attackWarningSprite;
    private Transform followTarget;
    private Vector3 worldFollowOffset;
    private Vector3 worldFollowScale = Vector3.one;
    private bool detachedFromEnemy;
    private Canvas worldCanvas;
    private GraphicRaycaster graphicRaycaster;
    private Image healthFillImage;
    private Image lerpFillImage;
    private float displayedHealth;
    private float displayedLerp;
    private static Sprite solidFillSprite;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        PrepareHealthBarVisuals();
        HideTextTemplates();

        if (attackWarningIcon != null)
        {
            attackWarningIcon.SetActive(false);
        }

        ApplyHealthBarVisibility();
        SnapHealthVisuals();
    }

    private void Start()
    {
        DetachFromEnemyFacing();
    }

    private void OnEnable()
    {
        SubscribeToEnemyEvents();
        ApplyHealthBarVisibility();
        SnapHealthVisuals();
    }

    private void OnDisable()
    {
        HideAttackWarning();
        UnsubscribeFromEnemyEvents();
    }

    private void LateUpdate()
    {
        KeepCanvasFacingForward();
        ApplyHealthVisuals(ReadNormalizedHp(), displayedLerp);
    }

    private void CacheReferences()
    {
        if (enemyController == null)
        {
            enemyController = GetComponentInParent<EnemyController>();
        }

        if (worldCanvas == null)
        {
            worldCanvas = GetComponent<Canvas>();
        }

        if (graphicRaycaster == null)
        {
            graphicRaycaster = GetComponent<GraphicRaycaster>();
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

    private void PrepareHealthBarVisuals()
    {
        UnnestLerpSlider();

        healthFillImage = GetFillImage(healthSlider);
        lerpFillImage = GetFillImage(damageLerpSlider);
        ConvertToFilledImage(healthFillImage);
        ConvertToFilledImage(lerpFillImage);

        if (healthSlider != null)
            healthSlider.enabled = false;
        if (damageLerpSlider != null)
            damageLerpSlider.enabled = false;

        if (graphicRaycaster != null)
            graphicRaycaster.ignoreReversedGraphics = false;

        displayedHealth = ReadNormalizedHp();
        displayedLerp = displayedHealth;
    }

    private void UnnestLerpSlider()
    {
        if (damageLerpSlider == null || healthSlider == null)
            return;

        Transform lerpTransform = damageLerpSlider.transform;
        Transform healthTransform = healthSlider.transform;
        if (!lerpTransform.IsChildOf(healthTransform))
            return;

        Transform canvasRoot = healthTransform.parent;
        lerpTransform.SetParent(canvasRoot, false);

        RectTransform healthRect = healthTransform as RectTransform;
        RectTransform lerpRect = lerpTransform as RectTransform;
        if (healthRect == null || lerpRect == null)
            return;

        lerpRect.anchorMin = healthRect.anchorMin;
        lerpRect.anchorMax = healthRect.anchorMax;
        lerpRect.pivot = healthRect.pivot;
        lerpRect.anchoredPosition = healthRect.anchoredPosition;
        lerpRect.sizeDelta = healthRect.sizeDelta;
        lerpRect.localScale = Vector3.one;
        lerpRect.localRotation = Quaternion.identity;
        lerpRect.SetSiblingIndex(healthRect.GetSiblingIndex());
    }

    private static Image GetFillImage(Slider slider)
    {
        if (slider == null)
            return null;

        if (slider.fillRect != null)
        {
            Image fillImage = slider.fillRect.GetComponent<Image>();
            if (fillImage != null)
                return fillImage;
        }

        return slider.GetComponentInChildren<Image>(true);
    }

    private static void ConvertToFilledImage(Image image)
    {
        if (image == null)
            return;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.pivot = new Vector2(0.5f, 0.5f);

        // Builtin UISprite/Knob is 9-sliced; Filled clips those borders into black fades.
        image.sprite = GetSolidFillSprite();
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        // Remaining HP stays on screen-left; empty grows from the right (ซ้าย ← ขวา).
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillClockwise = true;
        image.fillAmount = 1f;
    }

    private static Sprite GetSolidFillSprite()
    {
        if (solidFillSprite != null)
            return solidFillSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        solidFillSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        solidFillSprite.hideFlags = HideFlags.HideAndDontSave;
        return solidFillSprite;
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

    private void DetachFromEnemyFacing()
    {
        if (detachedFromEnemy)
            return;

        followTarget = enemyController != null ? enemyController.transform : transform.parent;
        if (followTarget == null || followTarget == transform)
            return;

        Vector3 delta = transform.position - followTarget.position;
        worldFollowOffset = new Vector3(0f, delta.y, delta.z);
        worldFollowScale = transform.lossyScale;
        worldFollowScale.x = Mathf.Abs(worldFollowScale.x);
        worldFollowScale.y = Mathf.Abs(worldFollowScale.y);
        worldFollowScale.z = Mathf.Abs(worldFollowScale.z);

        transform.SetParent(null, true);
        detachedFromEnemy = true;
        KeepCanvasFacingForward();
    }

    private void KeepCanvasFacingForward()
    {
        if (followTarget == null)
        {
            if (detachedFromEnemy)
                Destroy(gameObject);
            return;
        }

        Camera cam = Camera.main;
        Quaternion rotation = Quaternion.identity;
        if (cam != null)
        {
            // Face the camera's front so World Canvas is not drawn mirrored from behind.
            rotation = cam.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            if (worldCanvas != null)
                worldCanvas.worldCamera = cam;
        }

        transform.SetPositionAndRotation(followTarget.position + worldFollowOffset, rotation);
        transform.localScale = worldFollowScale;
    }

    private float ReadNormalizedHp()
    {
        if (enemyController == null || enemyController.maxHP <= 0f)
            return 0f;

        return Mathf.Clamp01(enemyController.currentHP / enemyController.maxHP);
    }

    private void ApplyHealthVisuals(float health, float lerp)
    {
        displayedHealth = Mathf.Clamp01(health);
        displayedLerp = Mathf.Max(displayedHealth, Mathf.Clamp01(lerp));

        if (healthFillImage != null)
            healthFillImage.fillAmount = displayedHealth;
        if (lerpFillImage != null)
            lerpFillImage.fillAmount = displayedLerp;
    }

    private void SnapHealthVisuals()
    {
        float hp = ReadNormalizedHp();
        displayedLerp = hp;
        ApplyHealthVisuals(hp, hp);
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
            if (detachedFromEnemy)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }
    }

    public void ShowAttackWarning(float duration = -1f)
    {
        ShowAttackWarning(duration, AttackWarningStyle.Normal);
    }

    public void ShowAttackWarning(float duration, AttackWarningStyle style)
    {
        if (enemyController != null && !enemyController.IsAlive)
            return;

        if (!EnsureCanvasCanRun())
            return;

        CacheReferences();
        if (attackWarningIcon == null)
            return;

        float resolvedDuration = duration > 0f ? duration : defaultAttackWarningDuration;
        ApplyWarningStyle(style);

        if (attackWarningCoroutine != null)
        {
            StopCoroutine(attackWarningCoroutine);
            attackWarningCoroutine = null;
        }

        attackWarningCoroutine = StartCoroutine(AttackWarningRoutine(resolvedDuration));
    }

    public void HideAttackWarning()
    {
        if (attackWarningCoroutine != null && isActiveAndEnabled)
            StopCoroutine(attackWarningCoroutine);

        attackWarningCoroutine = null;

        if (attackWarningIcon != null)
            attackWarningIcon.SetActive(false);
    }

    private bool EnsureCanvasCanRun()
    {
        if (enemyController != null && !enemyController.IsAlive)
            return false;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        return isActiveAndEnabled;
    }

    public void RefreshHealthDisplay()
    {
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (!showHealthBar || enemyController == null)
        {
            return;
        }

        float normalizedHp = ReadNormalizedHp();
        displayedHealth = normalizedHp;
        ApplyHealthVisuals(displayedHealth, displayedLerp);

        if (lerpFillImage == null)
        {
            displayedLerp = normalizedHp;
            return;
        }

        if (EnsureCanvasCanRun())
        {
            if (healthLerpCoroutine != null)
                StopCoroutine(healthLerpCoroutine);

            healthLerpCoroutine = StartCoroutine(HealthLerpRoutine(normalizedHp));
        }
        else
        {
            displayedLerp = normalizedHp;
            ApplyHealthVisuals(normalizedHp, normalizedHp);
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
        float startValue = displayedLerp;

        while (elapsed < healthLerpDuration)
        {
            elapsed += Time.deltaTime;
            displayedLerp = Mathf.Lerp(startValue, targetValue, elapsed / healthLerpDuration);
            ApplyHealthVisuals(displayedHealth, displayedLerp);
            yield return null;
        }

        displayedLerp = targetValue;
        ApplyHealthVisuals(displayedHealth, displayedLerp);
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
            isCritical = data.isCriticalHit,
            sourceStats = data.sourceStats,
            damageRatio = data.damageRatio,
            sourceStatValue = data.sourceStatValue
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
