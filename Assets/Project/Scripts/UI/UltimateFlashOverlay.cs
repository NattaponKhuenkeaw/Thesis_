using System.Collections;
using UnityEngine;

/// <summary>
/// Background flash VFX for Ultimate activations and Ultimate hit impacts.
/// Renders behind gameplay sprites using a fullscreen SpriteRenderer.
/// </summary>
public class UltimateFlashOverlay : MonoBehaviour
{
    public static UltimateFlashOverlay Instance { get; private set; }

    [Header("References")]
    [SerializeField] private SpriteRenderer flashRenderer;
    public SpriteRenderer FlashRenderer => flashRenderer;
    [SerializeField] private Camera targetCamera;

    [Header("Default Activation Sequence")]
    [SerializeField] private UltimateOverlaySequenceSettings defaultSequenceSettings = new UltimateOverlaySequenceSettings();

    [Header("Hit Flash Settings")]
    [Tooltip("Duration of the single-frame hit flash (real-time seconds).")]
    [SerializeField] private float hitFlashDuration = 0.04f;

    private Coroutine activationCoroutine;
    private Coroutine hitFlashCoroutine;
    private Coroutine impactFlashCoroutine;
    private System.Action blackScreenStartedCallback;
    private System.Action sequenceEndedCallback;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (flashRenderer == null)
        {
            flashRenderer = GetComponent<SpriteRenderer>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Initialize()
    {
        if (flashRenderer != null)
        {
            Color c = GetDefaultFlashColor();
            c.a = 0f;
            flashRenderer.color = c;
        }

        FitToCamera();
    }

    private void LateUpdate()
    {
        FollowCamera();
    }

    private void FitToCamera()
    {
        if (targetCamera == null || flashRenderer == null || flashRenderer.sprite == null)
        {
            return;
        }

        // Ensure the overlay stays super large by basing the size on a large value (minimum 15f)
        // or a large multiplier of the current orthographic size, so it covers everything even when zoomed.
        float baseSize = Mathf.Max(targetCamera.orthographicSize * 2.5f, 15f);
        float cameraHeight = baseSize * 2f;
        float cameraWidth = cameraHeight * targetCamera.aspect;

        Vector2 spriteSize = flashRenderer.sprite.bounds.size;
        transform.localScale = new Vector3(
            cameraWidth / spriteSize.x,
            cameraHeight / spriteSize.y,
            1f);
    }

    private void FollowCamera()
    {
        if (targetCamera == null)
        {
            return;
        }

        Vector3 camPos = targetCamera.transform.position;
        transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);
    }

    public void PlayActivationFlash()
    {
        PlaySequence(defaultSequenceSettings);
    }

    public void PlaySequence(
        UltimateOverlaySequenceSettings settings,
        System.Action onBlackScreenStarted = null,
        System.Action onSequenceEnded = null)
    {
        UltimateOverlaySequenceSettings resolvedSettings = settings ?? defaultSequenceSettings ?? new UltimateOverlaySequenceSettings();

        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
            activationCoroutine = null;
        }

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        if (impactFlashCoroutine != null)
        {
            StopCoroutine(impactFlashCoroutine);
            impactFlashCoroutine = null;
        }

        blackScreenStartedCallback = onBlackScreenStarted;
        sequenceEndedCallback = onSequenceEnded;
        
        // Fit to camera at the start of the sequence using the super large calculation
        FitToCamera();
        
        activationCoroutine = StartCoroutine(ActivationFlashRoutine(resolvedSettings));
    }

    public void StopSequence(bool invokeEndedCallback = false)
    {
        bool impactFlashIsActive = impactFlashCoroutine != null;

        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
            activationCoroutine = null;
        }

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        if (!impactFlashIsActive)
        {
            SetAlpha(0f);
        }

        System.Action ended = sequenceEndedCallback;
        blackScreenStartedCallback = null;
        sequenceEndedCallback = null;

        if (invokeEndedCallback)
        {
            ended?.Invoke();
        }
    }

    public void PlayHitFlash()
    {
        if (activationCoroutine != null || impactFlashCoroutine != null)
        {
            return;
        }

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
        }

        hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    public void PlayImpactFlash(BackgroundOverlayFlashSettings settings)
    {
        if (flashRenderer == null || activationCoroutine != null)
        {
            return;
        }

        settings ??= new BackgroundOverlayFlashSettings();

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        if (impactFlashCoroutine != null)
        {
            StopCoroutine(impactFlashCoroutine);
            impactFlashCoroutine = null;
        }

        impactFlashCoroutine = StartCoroutine(ImpactFlashRoutine(settings));
    }

    private IEnumerator ActivationFlashRoutine(UltimateOverlaySequenceSettings settings)
    {
        if (flashRenderer == null)
        {
            yield break;
        }

        SetAlpha(0f);
        flashRenderer.color = WithAlpha(settings.flashColor, 0f);

        float elapsed = 0f;
        while (elapsed < settings.fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = settings.fadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / settings.fadeInDuration);
            flashRenderer.color = WithAlpha(settings.flashColor, t);
            yield return null;
        }
        flashRenderer.color = WithAlpha(settings.flashColor, 1f);

        if (settings.holdDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(settings.holdDuration);
        }

        elapsed = 0f;
        while (elapsed < settings.darkenDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = settings.darkenDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / settings.darkenDuration);
            flashRenderer.color = Color.Lerp(settings.flashColor, settings.darkColor, t);
            yield return null;
        }
        flashRenderer.color = settings.darkColor;

        blackScreenStartedCallback?.Invoke();

        if (settings.blackHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(settings.blackHoldDuration);
        }

        elapsed = 0f;
        while (elapsed < settings.fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = settings.fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / settings.fadeOutDuration);
            flashRenderer.color = WithAlpha(settings.darkColor, 1f - t);
            yield return null;
        }

        SetAlpha(0f);
        activationCoroutine = null;

        System.Action ended = sequenceEndedCallback;
        blackScreenStartedCallback = null;
        sequenceEndedCallback = null;
        ended?.Invoke();
    }

    private IEnumerator HitFlashRoutine()
    {
        if (flashRenderer == null)
        {
            yield break;
        }

        flashRenderer.color = WithAlpha(GetDefaultFlashColor(), 1f);
        yield return new WaitForSecondsRealtime(hitFlashDuration);
        SetAlpha(0f);

        hitFlashCoroutine = null;
    }

    private IEnumerator ImpactFlashRoutine(BackgroundOverlayFlashSettings settings)
    {
        if (flashRenderer == null)
        {
            impactFlashCoroutine = null;
            yield break;
        }

        Color flashColor = settings.flashColor;
        float peakAlpha = Mathf.Clamp01(settings.peakAlpha);
        int fadeOutFrames = Mathf.Max(1, settings.fadeOutFrames);
        float fadeDuration = fadeOutFrames / (float)CombatCore.FrameDataConstants.LOGIC_FPS;

        flashRenderer.color = WithAlpha(flashColor, peakAlpha);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            flashRenderer.color = WithAlpha(flashColor, peakAlpha * (1f - t));
        }

        SetAlpha(0f);
        impactFlashCoroutine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (flashRenderer == null)
        {
            return;
        }

        Color c = flashRenderer.color;
        c.a = alpha;
        flashRenderer.color = c;
    }

    private Color GetDefaultFlashColor()
    {
        return defaultSequenceSettings != null ? defaultSequenceSettings.flashColor : Color.white;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
