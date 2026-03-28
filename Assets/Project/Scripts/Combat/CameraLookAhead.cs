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
    public float maxFallOffset = 3f;

    [Tooltip("How fast the vertical offset returns to zero when not falling (SmoothDamp time).")]
    public float verticalReturnSmoothTime = 0.25f;

    // =========================================================
    // Horizontal Smoothing
    // =========================================================
    [Header("Horizontal Smoothing")]
    [Tooltip("How fast the proxy follows the player horizontally. 0.05 = very tight.")]
    public float horizontalSmoothTime = 0.05f;

    // =========================================================
    // Private Runtime Data
    // =========================================================
    private float currentOffsetY     = 0f;
    private float offsetVelocityY    = 0f;
    private float currentProxyX      = 0f;
    private float proxyVelocityX     = 0f;

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
            currentProxyX = playerRigidbody.position.x;
    }

    private void LateUpdate()
    {
        if (playerRigidbody == null)
            return;

        Vector2 playerPos = playerRigidbody.position;
        float   vy        = playerRigidbody.linearVelocity.y;

        // --- Horizontal: mirror player X tightly (no look-ahead) ---
        currentProxyX = Mathf.SmoothDamp(
            currentProxyX,
            playerPos.x,
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

        currentOffsetY = Mathf.SmoothDamp(
            currentOffsetY,
            targetOffsetY,
            ref offsetVelocityY,
            Mathf.Max(verticalReturnSmoothTime, 0.01f)
        );

        transform.position = new Vector3(
            currentProxyX,
            playerPos.y + currentOffsetY,
            transform.position.z
        );
    }
}
