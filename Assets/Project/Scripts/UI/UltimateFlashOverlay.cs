using System.Collections;
using UnityEngine;

/// <summary>
/// Background flash VFX for Ultimate activations and Ultimate hit impacts.
/// Renders BEHIND all game sprites (characters, enemies, projectiles, VFX)
/// using a SpriteRenderer on a low sorting order, so only the background
/// flashes white — just like KOF2002UM.
///
/// Scene Setup:
///   Create a child GameObject under CameraSetUp (or any persistent root):
///     - Add SpriteRenderer with a plain white 1x1 sprite
///     - Set Sorting Layer to "Default" (or your background layer)
///     - Set Order in Layer to a very low value (e.g., -9999)
///     - Attach this component
///     - The sprite will be auto-scaled to fill the camera view
/// </summary>
public class UltimateFlashOverlay : MonoBehaviour
{
    public static UltimateFlashOverlay Instance { get; private set; }

    [Header("References")]
    [SerializeField] private SpriteRenderer flashRenderer;
    [SerializeField] private Camera targetCamera;

    [Header("Activation Flash Timing (Real-Time Seconds)")]
    [Tooltip("How fast the background snaps to white on Ultimate activation.")]
    [SerializeField] private float activationFadeInDuration = 0.03f;
    [Tooltip("How long the background holds at peak white.")]
    [SerializeField] private float activationHoldDuration = 0.15f;
    [Tooltip("How fast the white shifts to dark/black.")]
    [SerializeField] private float activationDarkenDuration = 0.12f;
    [Tooltip("How long the black/dark screen holds before fading out.")]
    [SerializeField] private float activationBlackHoldDuration = 0.5f;
    [Tooltip("How fast the dark fades back to transparent.")]
    [SerializeField] private float activationFadeOutDuration = 0.25f;

    [Header("Hit Flash Settings")]
    [Tooltip("Duration of the single-frame hit flash (real-time seconds).")]
    [SerializeField] private float hitFlashDuration = 0.04f;

    [Header("Colors")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private Color darkColor = new Color(0.05f, 0.02f, 0.1f, 1f);

    public event System.Action OnBlackScreenStarted;
    public event System.Action OnSequenceEnded;

    private Coroutine activationCoroutine;
    private Coroutine hitFlashCoroutine;

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
            Color c = flashColor;
            c.a = 0f;
            flashRenderer.color = c;
        }

        FitToCamera();
    }

    private void LateUpdate()
    {
        FollowCamera();
    }

    /// <summary>
    /// Scales the sprite to fill the entire camera viewport.
    /// </summary>
    private void FitToCamera()
    {
        if (targetCamera == null || flashRenderer == null || flashRenderer.sprite == null)
        {
            return;
        }

        float cameraHeight = targetCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * targetCamera.aspect;

        Vector2 spriteSize = flashRenderer.sprite.bounds.size;
        transform.localScale = new Vector3(
            cameraWidth / spriteSize.x * 1.2f,   // slight overscale to avoid edge gaps
            cameraHeight / spriteSize.y * 1.2f,
            1f);
    }

    /// <summary>
    /// Keeps the flash quad centered on the camera.
    /// </summary>
    private void FollowCamera()
    {
        if (targetCamera == null) return;

        Vector3 camPos = targetCamera.transform.position;
        transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);
    }

    // =========================================================
    // Public API
    // =========================================================

    /// <summary>
    /// Plays the full Ultimate activation flash sequence (KOF-style).
    /// White flash → hold → darken → fade out.
    /// Uses unscaledDeltaTime so it animates during Time.timeScale = 0.
    /// </summary>
    public void PlayActivationFlash()
    {
        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
        }

        // Cancel any hit flash in progress
        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        activationCoroutine = StartCoroutine(ActivationFlashRoutine());
    }

    /// <summary>
    /// Plays a single-frame white background flash (for Ultimate hits).
    /// Does not interrupt the activation flash if one is playing.
    /// </summary>
    public void PlayHitFlash()
    {
        // Don't interrupt the activation flash
        if (activationCoroutine != null) return;

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
        }

        hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    // =========================================================
    // Coroutines
    // =========================================================

    private IEnumerator ActivationFlashRoutine()
    {
        if (flashRenderer == null) yield break;

        // Reset
        SetAlpha(0f);
        flashRenderer.color = WithAlpha(flashColor, 0f);

        // 1. Fast flash to white
        float elapsed = 0f;
        while (elapsed < activationFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / activationFadeInDuration);
            flashRenderer.color = WithAlpha(flashColor, t);
            yield return null;
        }
        flashRenderer.color = WithAlpha(flashColor, 1f);

        // 2. Hold at peak white
        yield return new WaitForSecondsRealtime(activationHoldDuration);

        // 3. Shift white → dark
        elapsed = 0f;
        while (elapsed < activationDarkenDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / activationDarkenDuration);
            flashRenderer.color = Color.Lerp(flashColor, darkColor, t);
            yield return null;
        }
        flashRenderer.color = darkColor;

        // NEW: Trigger event when the screen is fully dark
        OnBlackScreenStarted?.Invoke();

        // 3.5 Hold while dark
        yield return new WaitForSecondsRealtime(activationBlackHoldDuration);

        // 4. Fade dark → transparent
        elapsed = 0f;
        while (elapsed < activationFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / activationFadeOutDuration);
            flashRenderer.color = WithAlpha(darkColor, 1f - t);
            yield return null;
        }
        SetAlpha(0f);

        activationCoroutine = null;
        OnSequenceEnded?.Invoke();
    }

    private IEnumerator HitFlashRoutine()
    {
        if (flashRenderer == null) yield break;

        // Instant white flash
        flashRenderer.color = WithAlpha(flashColor, 1f);

        // Hold for ~1 frame equivalent
        yield return new WaitForSecondsRealtime(hitFlashDuration);

        // Snap back to transparent
        SetAlpha(0f);

        hitFlashCoroutine = null;
    }

    // =========================================================
    // Helpers
    // =========================================================

    private void SetAlpha(float alpha)
    {
        if (flashRenderer == null) return;
        Color c = flashRenderer.color;
        c.a = alpha;
        flashRenderer.color = c;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
