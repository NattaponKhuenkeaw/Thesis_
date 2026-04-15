using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCombat combat;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (combat == null)
            combat = GetComponentInParent<PlayerCombat>();
    }

    // =========================================================
    // Animation Event Methods
    // =========================================================
    public void EnableHitbox()
    {
        if (combat != null)
            combat.EnableHitbox();
    }

    public void DisableHitbox()
    {
        if (combat != null)
            combat.DisableHitbox();
    }

    public void ResetCombo()
    {
        if (combat != null)
            combat.ResetCombo();
    }

    public void OnImpactStart() {}

    public void OnRecoveryStart() {}

    public void OnAttackEnd() {}

    public void ReleaseGrabbedEnemy()
    {
        if (combat != null)
            combat.ReleaseGrabbedEnemy();
    }
}
