using System.Collections;
using UnityEngine;

/// <summary>
/// Horizontal look-ahead proxy target for Cinemachine.
/// Attach to "CameraLookAheadTarget" and set it as the CinemachineCamera's Tracking Target.
///
/// Horizontal: follows the player with a facing-direction offset and room confinement.
/// Vertical: mirrors player Y; normal follow smoothing belongs to Cinemachine.
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
    private bool swapCenterActive;
    private float swapCenterUntil;

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

        bool holdSwapX = swapCenterActive && Time.unscaledTime < swapCenterUntil;
        if (swapCenterActive && !holdSwapX)
        {
            swapCenterActive = false;
            // Keep the new character centered after the handoff. Normal look-ahead resumes
            // on the next facing change instead of being reapplied immediately on this frame.
        }
        if (!holdSwapX) CheckFacingDirection();

        Vector2 playerPos = playerRigidbody.position;

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
        if (holdSwapX)
        {
            currentProxyX = targetX;
            proxyVelocityX = 0f;
        }
        else
        {
            currentProxyX = Mathf.SmoothDamp(
                currentProxyX,
                targetX,
                ref proxyVelocityX,
                Mathf.Max(horizontalSmoothTime, 0.01f)
            );
        }

        transform.position = new Vector3(
            currentProxyX,
            playerPos.y,
            transform.position.z
        );
    }

<<<<<<< Updated upstream
=======
    private bool TryResolvePlayerRigidbody()
    {
        if (playerRigidbody == null)
        {
            if (Time.unscaledTime < nextPlayerSearchTime)
                return false;

            nextPlayerSearchTime = Time.unscaledTime + PlayerSearchInterval;
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                PlayerMovement movement = playerObject.GetComponent<PlayerMovement>()
                    ?? playerObject.GetComponentInChildren<PlayerMovement>(true);
                Rigidbody2D body = movement != null ? movement.RB : null;
                BindPlayerRigidbody(body != null ? body : playerObject.GetComponent<Rigidbody2D>());
            }

            if (playerRigidbody == null)
            {
                PlayerMovement movement = Object.FindFirstObjectByType<PlayerMovement>();
                if (movement != null)
                    BindPlayerRigidbody(movement.RB);
            }
        }

        return playerRigidbody != null;
    }

    public void BindPlayerRigidbody(Rigidbody2D playerBody)
    {
        if (playerBody == null)
            return;

        playerRigidbody = playerBody;
        if (initializedPlayerRigidbody != playerRigidbody)
        {
            initializedPlayerRigidbody = playerRigidbody;
            currentProxyX = playerRigidbody.position.x;
            proxyVelocityX = 0f;
            CheckFacingDirection(true);
        }
    }

>>>>>>> Stashed changes
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
        transform.position = new Vector3(
            currentProxyX,
            playerRigidbody.position.y,
            transform.position.z);
    }

    public void ResetHorizontalForCharacterSwap(Rigidbody2D incomingBody, float centerSeconds)
    {
        if (incomingBody == null) return;
<<<<<<< Updated upstream
        playerRigidbody = incomingBody;
=======
        BindPlayerRigidbody(incomingBody);
>>>>>>> Stashed changes
        if (tweenId >= 0 && LeanTween.isTweening(tweenId)) LeanTween.cancel(tweenId);
        tweenId = -1;
        if (directionalCueRoutine != null) StopCoroutine(directionalCueRoutine);
        directionalCueRoutine = null;
        directionalCueOffset = 0f;
        currentHorizontalOffset = 0f;
        currentFacingDirection = GetFacingDirection(incomingBody.transform);
        swapCenterUntil = Time.unscaledTime + Mathf.Max(Time.unscaledDeltaTime, centerSeconds);
        swapCenterActive = true;
        currentProxyX = confineHorizontal
            ? Mathf.Clamp(incomingBody.position.x, confineLeft, confineRight)
            : incomingBody.position.x;
        proxyVelocityX = 0f;
        // Keep the vertical position continuous until the next follow update.
        transform.position = new Vector3(currentProxyX, transform.position.y, transform.position.z);
    }

    private static int GetFacingDirection(Transform target)
    {
        float yRot = target.eulerAngles.y % 360f;
        if (yRot < 0f) yRot += 360f;
        return yRot > 90f && yRot < 270f ? -1 : 1;
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
