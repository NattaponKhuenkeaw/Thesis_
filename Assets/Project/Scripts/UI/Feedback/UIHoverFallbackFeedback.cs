using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIHoverFallbackFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float transitionDuration = 0.1f;
    [SerializeField] private Color glowColor = new Color(1f, 0.85f, 0.2f, 0.85f);
    [SerializeField] private Vector2 glowDistance = new Vector2(3f, 3f);

    [Header("Click")]
    [SerializeField] private bool playClickGlint = true;
    [SerializeField] private Color clickBurstColor = new Color(1f, 0.9f, 0.45f, 0.8f);

    private RectTransform rectTransform;
    private Graphic graphic;
    private Outline outline;
    private UIFeedbackDriver feedbackDriver;
    private Coroutine transitionCoroutine;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        baseScale = transform.localScale;
        SetGlow(0f);
    }

    public void Configure(float scale, Color glow, float duration)
    {
        hoverScale = scale;
        glowColor = glow;
        transitionDuration = duration;
        CacheReferences();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayTransition(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PlayTransition(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!playClickGlint || rectTransform == null)
        {
            return;
        }

        feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
        if (feedbackDriver == null)
        {
            return;
        }

        feedbackDriver.PlayGlint(rectTransform, glowColor, transitionDuration);
        feedbackDriver.PlayRadialBurst(rectTransform, clickBurstColor, 6, 24f, transitionDuration);
    }

    private void CacheReferences()
    {
        rectTransform = rectTransform != null ? rectTransform : GetComponent<RectTransform>();
        graphic = graphic != null ? graphic : GetComponent<Graphic>();
        feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);

        if (graphic == null)
        {
            return;
        }

        outline = outline != null ? outline : GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }

        outline.effectDistance = glowDistance;
        outline.useGraphicAlpha = true;
    }

    private void PlayTransition(bool hovering)
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(TransitionRoutine(hovering));
    }

    private IEnumerator TransitionRoutine(bool hovering)
    {
        float resolvedDuration = Mathf.Max(0.01f, transitionDuration);
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = hovering ? baseScale * hoverScale : baseScale;
        float startGlow = outline != null ? outline.effectColor.a : 0f;
        float endGlow = hovering ? glowColor.a : 0f;

        while (elapsed < resolvedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / resolvedDuration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            transform.localScale = Vector3.Lerp(startScale, endScale, eased);
            SetGlow(Mathf.Lerp(startGlow, endGlow, t));
            yield return null;
        }

        transform.localScale = endScale;
        SetGlow(endGlow);
        transitionCoroutine = null;
    }

    private void SetGlow(float alpha)
    {
        if (outline == null)
        {
            return;
        }

        Color color = glowColor;
        color.a = alpha;
        outline.effectColor = color;
        outline.enabled = alpha > 0.001f;
    }
}
