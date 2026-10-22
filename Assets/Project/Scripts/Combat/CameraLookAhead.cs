using System.Collections;
using UnityEngine;

/// <summary>
/// Vertical fall look-ahead proxy target for Cinemachine.
/// Attach to "CameraLookAheadTarget" and set it as the CinemachineCamera's Tracking Target.
///
/// Horizontal: proxy mirrors player X exactly (no horizontal offset).
/// Vertical:   when the player is FALLING (velocity.y < 0), the proxy is pushed
///             downward by (velocity.y * fallLookAheadTime), clamped to maxFallOffset.
///             This makes the camera lead the fall so the player never feels the
///             camera lagging downward on direction changes.
/// </summary>
public class CameraLookAhead : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    [Tooltip("The player's Rigidbody2D. Auto-found by 'Player' tag if left blank.")]
    public Rigidbody2D playerRigidbody;

    // =========================================================
    // Fall Look-Ahead Settings
    // =========================================================
    [Header("Fall Look-Ahead")]
    [Tooltip("Seconds of downward velocity to project. Higher = camera leads further ahead.")]
    public float fallLookAheadTime = 0.35f;

    [Tooltip("Maximum downward distance the proxy can sit below the player.")]
    public float maxFallOffset = 8f;

    [Tooltip("How quickly the proxy moves down to lead a fall. Lower values make the camera anticipate fast falls sooner.")]
    public float fallLookAheadSmoothTime = 0.08f;

    [Tooltip("How fast the vertical offset returns to zero when not falling (SmoothDamp time).")]
    public float verticalReturnSmoothTime = 0.25f;

    // =========================================================
    // Horizontal Look-Ahead
    // =========================================================
    [Header("Horizontal Look-Ahead")]
    [Tooltip("Distance the camera looks ahead horizontally.")]
    public float horizontalLookAheadDistance = 2f;

    [Tooltip("Duration of the horizontal offset tween when changing direction.")]
    public float horizontalLookAheadDuration = 0.5f;

    [Tooltip("How fast the proxy follows the target X position. 0.05 = very tight.")]
    public float horizontalSmoothTime = 0.05f;

    // =========================================================
    // Private Runtime Data
    // =========================================================
    private float currentOffsetY     = 0f;
    private float offsetVelocityY    = 0f;
    private float currentProxyX      = 0f;
    private float proxyVelocityX     = 0f;
    private float currentHorizontalOffset = 0f;
    private float directionalCueOffset = 0f;
    private bool confineHorizontal;
    private float confineLeft;
    private float confineRight;
    private float confineFade = 3f;
    private int currentFacingDirection = 1;
    private int tweenId = -1;
    private Coroutine directionalCueRoutine;

    // =========================================================
    // Unity Methods
    // =========================================================
    private void Awake()
    {
        if (playerRigidbody == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                playerRigidbody = playerObj.GetComponent<Rigidbody2D>();

            if (playerRigidbody == null)
                Debug.LogWarning("[CameraLookAhead] No Rigidbody2D found. Assign playerRigidbody in the inspector.");
        }

        if (playerRigidbody != null)
        {
            currentProxyX = playerRigidbody.position.x;
            CheckFacingDirection(true); // Initialize offset instantly
        }
    }

    private void CheckFacingDirection(bool instant = false)
    {
        // Calculate facing direction from Y rotation assuming 0 is right, 180 is left.
        float yRot = playerRigidbody.transform.eulerAngles.y % 360f;
        if (yRot < 0f) yRot += 360f;

        int facingDirection = (yRot > 90f && yRot < 270f) ? -1 : 1;

        if (facingDirection != currentFacingDirection || instant)
        {
            currentFacingDirection = facingDirection;
            float targetOffset = currentFacingDirection * horizontalLookAheadDistance;

            if (instant)
            {
                currentHorizontalOffset = targetOffset;
            }
            else
            {
                // Cancel previous tween if needed
                if (LeanTween.isTweening(tweenId))
                    LeanTween.cancel(tweenId);

                tweenId = LeanTween.value(gameObject, currentHorizontalOffset, targetOffset, horizontalLookAheadDuration)
                    .setEaseInOutSine()
                    .setOnUpdate((float val) => {
                        currentHorizontalOffset = val;
                    })
                    .id;
            }
        }
    }

    private void LateUpdate()
    {
        if (playerRigidbody == null)
            return;

        CheckFacingDirection();

        Vector2 playerPos = playerRigidbody.position;
        float   vy        = playerRigidbody.linearVelocity.y;

        // --- Horizontal: follow player X plus our look-ahead offset ---
        float lookAhead = currentHorizontalOffset;
        if (confineHorizontal)
        {
            float fade = confineFade;
            float distRight = confineRight - playerPos.x;
            float distLeft = playerPos.x - confineLeft;
            float scale = currentFacingDirection > 0
                ? Mathf.Clamp01(distRight / fade)
                : Mathf.Clamp01(distLeft / fade);
            lookAhead *= scale;
        }

        float targetX = playerPos.x + lookAhead + directionalCueOffset;
        if (confineHorizontal)
        {
            targetX = Mathf.Clamp(targetX, confineLeft, confineRight);
        }
        currentProxyX = Mathf.SmoothDamp(
            currentProxyX,
            targetX,
            ref proxyVelocityX,
            Mathf.Max(horizontalSmoothTime, 0.01f)
        );

        // --- Vertical: push ahead (downward) only while falling ---
        float targetOffsetY = 0f;
        if (vy < 0f)
        {
            // velocity is negative when falling; project downward
            float raw = vy * fallLookAheadTime;           // e.g. -8 * 0.35 = -2.8
            targetOffsetY = Mathf.Max(raw, -maxFallOffset); // clamp: must not exceed -maxFallOffset
        }

        float offsetSmoothTime = vy < 0f
            ? Mathf.Min(fallLookAheadSmoothTime, verticalReturnSmoothTime)
            : verticalReturnSmoothTime;
        currentOffsetY = Mathf.SmoothDamp(
            currentOffsetY,
            targetOffsetY,
            ref offsetVelocityY,
            Mathf.Max(offsetSmoothTime, 0.01f)
        );

        transform.position = new Vector3(
            currentProxyX,
            playerPos.y + currentOffsetY,
            transform.position.z
        );
    }

    /// <summary>
    /// Fade horizontal look-ahead as the player approaches a room wall so the
    /// tracking target does not pull the camera past the confiner.
    /// </summary>
    public void SetHorizontalConfine(float leftWorld, float rightWorld, float fadeDistance)
    {
        confineHorizontal = true;
        confineLeft = leftWorld;
        confineRight = rightWorld;
        confineFade = Mathf.Max(0.01f, fadeDistance);

        if (playerRigidbody != null && Mathf.Abs(currentProxyX - playerRigidbody.position.x) > 8f)
        {
            SnapToPlayer();
        }
    }

    public void SnapToPlayer()
    {
        if (playerRigidbody == null)
        {
            return;
        }

        float playerX = playerRigidbody.position.x;
        currentProxyX = confineHorizontal
            ? Mathf.Clamp(playerX, confineLeft, confineRight)
            : playerX;
        proxyVelocityX = 0f;
        currentOffsetY = 0f;
        offsetVelocityY = 0f;
        transform.position = new Vector3(
            currentProxyX,
            playerRigidbody.position.y,
            transform.position.z);
    }

    public void ClearHorizontalConfine()
    {
        confineHorizontal = false;
    }

    public void PlayDirectionalCue(float worldOffset, float duration)
    {
        if (directionalCueRoutine != null)
        {
            StopCoroutine(directionalCueRoutine);
        }

        directionalCueRoutine = StartCoroutine(DirectionalCueRoutine(Mathf.Max(0f, worldOffset), Mathf.Max(0.05f, duration)));
    }

    private IEnumerator DirectionalCueRoutine(float worldOffset, float duration)
    {
        float halfDuration = duration * 0.5f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / halfDuration);
            directionalCueOffset = Mathf.SmoothStep(0f, worldOffset, progress);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / halfDuration);
            directionalCueOffset = Mathf.SmoothStep(worldOffset, 0f, progress);
            yield return null;
        }

        directionalCueOffset = 0f;
        directionalCueRoutine = null;
    }
}
