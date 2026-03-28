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

    [Header("Damage Text")]
    [SerializeField] private Vector2 damageTextStartOffset = new Vector2(0f, 0f);
    [SerializeField] private Vector2 damageTextFloatOffset = new Vector2(0f, 36f);
    [SerializeField] private float damageTextDuration = 0.5f;
    [SerializeField] private bool hideCanvasOnDeath = true;

    private RectTransform damageTextRect;
    private Coroutine damageTextCoroutine;
    private Color damageTextBaseColor = Color.white;
    private Vector3 baseLocalScale = Vector3.one;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        baseLocalScale = transform.localScale;

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

    private void KeepCanvasFacingForward()
    {
        Transform parentTransform = transform.parent;
        if (parentTransform == null)
            return;

        float parentSignX = Mathf.Sign(parentTransform.lossyScale.x);
        if (Mathf.Approximately(parentSignX, 0f))
            parentSignX = 1f;

        Vector3 correctedScale = baseLocalScale;
        correctedScale.x = Mathf.Abs(baseLocalScale.x) * parentSignX;
        transform.localScale = correctedScale;
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
        ShowDamageText(data.damage);
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

    private void ShowDamageText(float damage)
    {
        if (damageText == null)
            return;

        if (damageTextCoroutine != null)
            StopCoroutine(damageTextCoroutine);

        if (damageTextRect == null)
            damageTextRect = damageText.rectTransform;

        damageText.text = Mathf.RoundToInt(damage).ToString();
        damageText.color = damageTextBaseColor;
        damageTextRect.anchoredPosition = damageTextStartOffset;
        damageText.gameObject.SetActive(true);

        damageTextCoroutine = StartCoroutine(DamageTextRoutine());
    }

    private IEnumerator DamageTextRoutine()
    {
        float elapsed = 0f;
        Vector2 start = damageTextStartOffset;
        Vector2 end = damageTextStartOffset + damageTextFloatOffset;

        while (elapsed < damageTextDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageTextDuration);

            if (damageTextRect != null)
                damageTextRect.anchoredPosition = Vector2.Lerp(start, end, t);

            Color color = damageTextBaseColor;
            color.a = 1f - t;
            damageText.color = color;

            yield return null;
        }

        damageText.color = damageTextBaseColor;
        damageText.gameObject.SetActive(false);
        damageTextCoroutine = null;
    }
}
