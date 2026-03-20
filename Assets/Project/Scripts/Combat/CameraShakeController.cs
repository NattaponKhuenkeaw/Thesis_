
using Unity.Cinemachine;
using UnityEngine;

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
    public void Shake(float strength)
    {
        if (impulseSource == null)
        {
            Debug.LogWarning("CameraShakeController: impulseSource is null");
            return;
        }

        impulseSource.GenerateImpulse(strength);
    }
}