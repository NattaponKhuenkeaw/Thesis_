
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Simple wrapper around CinemachineImpulseSource for screen shake.
/// Now also supports DamageImpactTier-mapped shake intensities.
/// Delegates to CameraManager when available; falls back to local impulse source.
/// </summary>
public class CameraShakeController : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public CinemachineImpulseSource impulseSource;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // =========================================================
    // Public Methods
    // =========================================================

    /// <summary>
    /// Fire a raw-strength shake. Prefers CameraManager's central impulse;
    /// falls back to this component's own impulse source.
    /// </summary>
    public void Shake(float strength)
    {
        if (strength <= 0f) return;

        if (GameImpulseController.Instance != null)
        {
            GameImpulseController.Instance.PlayOneShot(strength);
            return;
        }

        // Fallback: local impulse source
        if (impulseSource == null)
        {
            Debug.LogWarning("CameraShakeController: impulseSource is null");
            return;
        }

        impulseSource.GenerateImpulse(strength);
    }

    /// <summary>
    /// Fire a shake mapped to the DamageImpactTier.
    /// Designed to be called during the HitStop routine for dramatic effect.
    /// </summary>
    public void ShakeByImpact(DamageImpactTier tier)
    {
        if (GameImpulseController.Instance != null)
        {
            GameImpulseController.Instance.PlayImpact(tier);
            return;
        }

        // Fallback: use default strengths
        float strength = tier switch
        {
            DamageImpactTier.Low      => 0.15f,
            DamageImpactTier.Medium   => 0.4f,
            DamageImpactTier.Heavy    => 0.65f,
            DamageImpactTier.Ultimate => 1.0f,
            _                         => 0.15f
        };
        Shake(strength);
    }
}
