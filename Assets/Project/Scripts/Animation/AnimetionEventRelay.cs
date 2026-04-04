using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerBasicAttack combat;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (combat == null)
            combat = GetComponentInParent<PlayerBasicAttack>();
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

    public void OnImpactStart()
    {
        if (combat != null)
            combat.OnImpactStart();
    }

    public void OnRecoveryStart()
    {
        if (combat != null)
            combat.OnRecoveryStart();
    }

    public void OnAttackEnd()
    {
        if (combat != null)
            combat.OnAttackEnd();
    }
}