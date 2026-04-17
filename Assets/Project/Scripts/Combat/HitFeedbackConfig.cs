using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Hit Feedback Config")]
public class HitFeedbackConfig : ScriptableObject
{
    [Header("Default Hit SFX by Impact Tier")]
    public AudioClip[] lowImpactSFX;
    public AudioClip[] mediumImpactSFX;
    public AudioClip[] heavyImpactSFX;
    public AudioClip[] ultimateImpactSFX;
    
    [Header("Default Hit VFX by Impact Tier")]
    public Project.Combat.VFXType lowImpactVFXType = Project.Combat.VFXType.HitSpark_Light;
    public Project.Combat.VFXType mediumImpactVFXType = Project.Combat.VFXType.HitSpark_Medium;
    public Project.Combat.VFXType heavyImpactVFXType = Project.Combat.VFXType.HitSpark_Heavy;
    public Project.Combat.VFXType ultimateImpactVFXType = Project.Combat.VFXType.HitSpark_Ultimate;

    public GameObject lowImpactVFX;
    public GameObject mediumImpactVFX;
    public GameObject heavyImpactVFX;
    public GameObject ultimateImpactVFX;
    
    [Header("Guard / Whiff")]
    public AudioClip[] guardSFX;
    public AudioClip[] whiffSFX;
    public Project.Combat.VFXType guardVFXType = Project.Combat.VFXType.GuardSpark;
    public GameObject guardVFX;

    // Helpers to get random clip
    public AudioClip GetSFX(DamageImpactTier tier)
    {
        AudioClip[] array = tier switch
        {
            DamageImpactTier.Low => lowImpactSFX,
            DamageImpactTier.Medium => mediumImpactSFX,
            DamageImpactTier.Heavy => heavyImpactSFX,
            DamageImpactTier.Ultimate => ultimateImpactSFX,
            _ => lowImpactSFX
        };
        
        if (array == null || array.Length == 0) return null;
        return array[Random.Range(0, array.Length)];
    }

    public Project.Combat.VFXType GetVFXType(DamageImpactTier tier)
    {
        return tier switch
        {
            DamageImpactTier.Low => lowImpactVFXType,
            DamageImpactTier.Medium => mediumImpactVFXType,
            DamageImpactTier.Heavy => heavyImpactVFXType,
            DamageImpactTier.Ultimate => ultimateImpactVFXType,
            _ => lowImpactVFXType
        };
    }

    public GameObject GetVFX(DamageImpactTier tier)
    {
        return tier switch
        {
            DamageImpactTier.Low => lowImpactVFX,
            DamageImpactTier.Medium => mediumImpactVFX,
            DamageImpactTier.Heavy => heavyImpactVFX,
            DamageImpactTier.Ultimate => ultimateImpactVFX,
            _ => lowImpactVFX
        };
    }

    public AudioClip GetGuardSFX()
    {
        if (guardSFX == null || guardSFX.Length == 0) return null;
        return guardSFX[Random.Range(0, guardSFX.Length)];
    }
}
