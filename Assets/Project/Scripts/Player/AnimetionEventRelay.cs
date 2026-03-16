using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public BasicAttack basicAttack;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (basicAttack == null)
            basicAttack = GetComponentInParent<BasicAttack>();
    }

    // =========================================================
    // Animation Event Methods
    // =========================================================
    public void EnableHitbox()
    {
        if (basicAttack != null)
            basicAttack.EnableHitbox();
    }

    public void DisableHitbox()
    {
        if (basicAttack != null)
            basicAttack.DisableHitbox();
    }

    public void ResetCombo()
    {
        if (basicAttack != null)
            basicAttack.ResetCombo();
    }
}