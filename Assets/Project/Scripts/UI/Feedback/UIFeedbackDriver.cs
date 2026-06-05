using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Code-only fallback feedback for UI juice when final art, shaders, or prefabs are not assigned.
/// </summary>
public class UIFeedbackDriver : MonoBehaviour
{
    private static Sprite fallbackSprite;

    public static UIFeedbackDriver GetOrAdd(GameObject host)
    {
        if (host == null)
        {
            return null;
        }

        if (!host.TryGetComponent(out UIFeedbackDriver driver))
        {
            driver = host.AddComponent<UIFeedbackDriver>();
        }

        return driver;
    }

    public Coroutine PlayPulse(Transform target, float peakScale = 1.2f, float duration = 0.2f)
    {
        if (target == null || !isActiveAndEnabled)
        {
            return null;
        }

        return StartCoroutine(PulseRoutine(target, peakScale, duration));
    }

    public Coroutine PlayWiggle(RectTransform target, Vector2 basePosition, float distance = 8f, float duration = 0.25f)
    {
        if (target == null || !isActiveAndEnabled)
        {
            return null;
        }

        return StartCoroutine(WiggleRoutine(target, basePosition, distance, duration));
    }

    public Coroutine PlayFlash(Graphic target, Color flashColor, float duration = 0.2f)
    {
        if (target == null || !isActiveAndEnabled)
        {
            return null;
        }

        return StartCoroutine(FlashRoutine(target, flashColor, duration));
    }

    public Coroutine PlayGlint(RectTransform target, Color color, float duration = 0.18f)
    {
        if (target == null || !isActiveAndEnabled)
        {
            return null;
        }

        return StartCoroutine(GlintRoutine(target, color, duration));
    }

    public Coroutine PlayRadialBurst(RectTransform parent, Color color, int count = 8, float distance = 42f, float duration = 0.18f)
    {
        if (parent == null || !isActiveAndEnabled)
        {
            return null;
        }

        return StartCoroutine(RadialBurstRoutine(parent, color, Mathf.Max(1, count), distance, duration));
    }

    private IEnumerator PulseRoutine(Transform target, float peakScale, float duration)
    {
        Vector3 originalScale = target.localScale;
        float resolvedDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < resolvedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / resolvedDuration);
            float wave = Mathf.Sin(t * Mathf.PI);
            target.localScale = originalScale * Mathf.Lerp(1f, peakScale, wave);
            yield return null;
        }

        target.localScale = originalScale;
    }

    private IEnumerator WiggleRoutine(RectTransform target, Vector2 basePosition, float distance, float duration)
    {
        float resolvedDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < resolvedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / resolvedDuration);
            float damping = 1f - t;
            float wiggle = Mathf.Sin(t * Mathf.PI * 6f) * distance * damping;
            target.anchoredPosition = basePosition + new Vector2(wiggle, 0f);
            yield return null;
        }

        target.anchoredPosition = basePosition;
    }

    private IEnumerator FlashRoutine(Graphic target, Color flashColor, float duration)
    {
        Color originalColor = target.color;
        float resolvedDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < resolvedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / resolvedDuration);
            target.color = Color.Lerp(flashColor, originalColor, t);
            yield return null;
        }

        target.color = originalColor;
    }

    private IEnumerator GlintRoutine(RectTransform target, Color color, float duration)
    {
        GameObject glintObject = new GameObject("Fallback_Glint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform glintRect = glintObject.GetComponent<RectTransform>();
        Image glintImage = glintObject.GetComponent<Image>();

        glintRect.SetParent(target, false);
        glintRect.anchorMin = new Vector2(0f, 0f);
        glintRect.anchorMax = new Vector2(0f, 1f);
        glintRect.pivot = new Vector2(0.5f, 0.5f);
        glintRect.sizeDelta = new Vector2(Mathf.Max(8f, target.rect.width * 0.18f), 0f);
        glintRect.localRotation = Quaternion.Euler(0f, 0f, -45f);

        glintImage.sprite = GetFallbackSprite();
        glintImage.raycastTarget = false;
        glintImage.color = color;

        float resolvedDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        float startX = -target.rect.width * 0.65f;
        float endX = target.rect.width * 0.65f;

        while (elapsed < resolvedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / resolvedDuration);
            glintRect.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, t), 0f);
            Color currentColor = color;
            currentColor.a *= Mathf.Sin(t * Mathf.PI);
            glintImage.color = currentColor;
            yield return null;
        }

        Destroy(glintObject);
    }

    private IEnumerator RadialBurstRoutine(RectTransform parent, Color color, int count, float distance, float duration)
    {
        RectTransform[] particles = new RectTransform[count];
        Image[] images = new Image[count];

        for (int i = 0; i < count; i++)
        {
            GameObject particle = new GameObject("Fallback_Burst", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = particle.GetComponent<RectTransform>();
            Image image = particle.GetComponent<Image>();

            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(6f, 6f);
            rect.anchoredPosition = Vector2.zero;

            image.sprite = GetFallbackSprite();
            image.raycastTarget = false;
            image.color = color;

            particles[i] = rect;
            images[i] = image;
        }

        float resolvedDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < resolvedDuration)
        {
            for (int i = 0; i < count; i++)
            {
                if (particles[i] == null)
                {
                    continue;
                }

                float angle = (Mathf.PI * 2f / count) * i;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                particles[i].anchoredPosition = direction * Mathf.Lerp(0f, distance, Mathf.Clamp01(elapsed / resolvedDuration));

                Color particleColor = color;
                particleColor.a *= 1f - Mathf.Clamp01(elapsed / resolvedDuration);
                images[i].color = particleColor;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (particles[i] != null)
            {
                Destroy(particles[i].gameObject);
            }
        }
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite == null)
        {
            fallbackSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        return fallbackSprite;
    }
}
