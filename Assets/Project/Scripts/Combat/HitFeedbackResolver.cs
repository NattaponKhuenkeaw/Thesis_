using UnityEngine;

public class HitFeedbackResolver : MonoBehaviour
{
    public static HitFeedbackResolver Instance { get; private set; }
    public HitFeedbackConfig defaultConfig;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        HitFeedbackEvents.FeedbackRequested += HandleFeedbackRequest;
    }

    private void OnDisable()
    {
        HitFeedbackEvents.FeedbackRequested -= HandleFeedbackRequest;
    }

    private void HandleFeedbackRequest(HitFeedbackRequest request)
    {
        PlayHitFeedback(request.Hitbox, request.Tier, request.HitPoint, request.HitDirection, request.ProjectileData, request.PlayAudio);
    }

    public void PlayHitFeedback(HitboxBase hitbox, DamageImpactTier tier, Vector2 hitPoint, Vector2 hitDirection,
        ProjectileData projectileData = null, bool playAudio = true)
    {
        if (playAudio) PlayHitAudio(hitbox, tier, projectileData);

        if (hitbox != null && hitbox.suppressContactVfx) return;

        // 2. Resolve VFX
        Project.Combat.VFXType vfxType = Project.Combat.VFXType.None;
        GameObject vfxPrefab = null;
        Vector2 hitVFXOffset = Vector2.zero;

        // Determine which VFX to play (prioritize Library Type, then Prefab)
        if (hitbox is NormalHitbox nHitbox)
        {
            vfxType = nHitbox.hitVFXType;
            vfxPrefab = nHitbox.hitVFXPrefab;
            hitVFXOffset = nHitbox.hitVFXOffset;
        }
        else if (hitbox is GrabHitbox gHitbox)
        {
            vfxType = gHitbox.hitVFXType;
            vfxPrefab = gHitbox.hitVFXPrefab;
            hitVFXOffset = gHitbox.hitVFXOffset;
        }

        // If no specific VFX set from hitbox, check projectile data, then default config
        if (vfxType == Project.Combat.VFXType.None && vfxPrefab == null)
        {
            if (projectileData != null)
            {
                vfxType = projectileData.impactVFXType;
            }
            
            if (vfxType == Project.Combat.VFXType.None && defaultConfig != null)
            {
                vfxType = defaultConfig.GetVFXType(tier);
                vfxPrefab = defaultConfig.GetVFX(tier);
            }
        }

        // --- Execute Spawning ---
        // Mirror the offset based on hit direction
        if (hitDirection.x < 0) hitVFXOffset.x = -Mathf.Abs(hitVFXOffset.x);
        else hitVFXOffset.x = Mathf.Abs(hitVFXOffset.x);

        Vector3 spawnPos = (Vector3)(hitPoint + hitVFXOffset);

        // 1. Try Library-driven VFX (Optimized)
        if (vfxType != Project.Combat.VFXType.None && Project.Combat.VFXManager.Instance != null)
        {
            // For hitsparks, we pass the flip parameter based on hit direction
            bool flipX = hitDirection.x < 0;
            Project.Combat.VFXManager.Instance.Spawn(vfxType, spawnPos, flipX, 0f); // Using 0 angle as flip handle everything now
        }
        // 2. Fallback to Prefab (Legacy)
        else if (vfxPrefab != null)
        {
            float angle = Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            GameObject vfxInstance = Instantiate(vfxPrefab, spawnPos, rotation);
            
            var ps = vfxInstance.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }
            else
            {
                Destroy(vfxInstance, 3f);
            }
        }
    }

    private void PlayHitAudio(HitboxBase hitbox, DamageImpactTier tier, ProjectileData projectileData)
    {
        AudioClip sfxToPlay = null;
        if (hitbox is NormalHitbox normalHitbox && normalHitbox.hitSFX != null)
        {
            sfxToPlay = normalHitbox.hitSFX;
        }
        else if (hitbox is GrabHitbox grabHitbox && grabHitbox.hitSFX != null)
        {
            sfxToPlay = grabHitbox.hitSFX;
        }
        else if (projectileData != null && projectileData.impactAudio != null)
        {
            sfxToPlay = projectileData.impactAudio;
        }
        else if (defaultConfig != null)
        {
            sfxToPlay = defaultConfig.GetSFX(tier);
        }

        else
        {
            Debug.LogWarning("HitFeedbackResolver: defaultConfig is null! Please assign a HitFeedbackConfig ScriptableObject.", this);
        }

        if (sfxToPlay != null)
        {
            if (GameAudioManager.Instance != null)
            {
                float volume = 1f;
                float basePitch = 1f;
                if (hitbox is NormalHitbox normal)
                {
                    volume = normal.hitSFXVolume;
                    basePitch = normal.hitSFXPitch;
                }
                else if (hitbox is GrabHitbox grab)
                {
                    volume = grab.hitSFXVolume;
                    basePitch = grab.hitSFXPitch;
                }

                // Preserve the existing variation around the authored pitch.
                float pitch = Mathf.Clamp(basePitch * Random.Range(0.95f, 1.05f), 0.1f, 3f);
                GameAudioManager.Instance.PlayHitSFX(sfxToPlay, GameAudioManager.ClampSFXGain(volume), pitch);
            }
            else
            {
                Debug.LogWarning("HitFeedbackResolver: GameAudioManager.Instance is null! Cannot play hit SFX.", this);
            }
        }
    }
}
