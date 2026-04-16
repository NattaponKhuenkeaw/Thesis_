using UnityEngine;

public class HitFeedbackResolver : MonoBehaviour
{
    public HitFeedbackConfig defaultConfig;
    
    // We assume the game will have a master VFX Spawner or we can just Instantiate.
    // For simplicity, we just use simple Instantiate for the Hit FX,
    // though a pool is highly recommended in production.
    
    public void PlayHitFeedback(HitboxBase hitbox, DamageImpactTier tier, Vector2 hitPoint, Vector2 hitDirection)
    {
        // 1. Resolve Audio
        AudioClip sfxToPlay = null;
        if (hitbox is NormalHitbox normalHitbox && normalHitbox.hitSFX != null)
        {
            sfxToPlay = normalHitbox.hitSFX;
        }
        else if (hitbox is GrabHitbox grabHitbox && grabHitbox.hitSFX != null)
        {
            sfxToPlay = grabHitbox.hitSFX;
        }
        else if (defaultConfig != null)
        {
            sfxToPlay = defaultConfig.GetSFX(tier);
        }

        if (sfxToPlay != null && GameAudioManager.Instance != null)
        {
            // Add slight pitch randomization for variety
            float pitch = Random.Range(0.95f, 1.05f);
            GameAudioManager.Instance.PlayHitSFX(sfxToPlay, 1f, pitch);
        }

        // 2. Resolve VFX
        GameObject vfxPrefab = null;
        Vector2 hitVFXOffset = Vector2.zero;

        if (hitbox is NormalHitbox nHitbox && nHitbox.hitVFXPrefab != null)
        {
            vfxPrefab = nHitbox.hitVFXPrefab;
            hitVFXOffset = nHitbox.hitVFXOffset;
        }
        else if (hitbox is GrabHitbox gHitbox && gHitbox.hitVFXPrefab != null)
        {
            vfxPrefab = gHitbox.hitVFXPrefab;
            hitVFXOffset = gHitbox.hitVFXOffset;
        }
        else if (defaultConfig != null)
        {
            vfxPrefab = defaultConfig.GetVFX(tier);
        }

        if (vfxPrefab != null)
        {
            // Spawn VFX
            // Note: In hitstop, scaled time particles will freeze, which is correct!
            Vector3 spawnPos = (Vector3)(hitPoint + hitVFXOffset);
            
            // Calculate rotation based on hit direction
            float angle = Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            GameObject vfxInstance = Instantiate(vfxPrefab, spawnPos, rotation);
            
            // Optional: destroy after duration if the prefab doesn't handle its own death.
            // A simple Destroy(vfxInstance, 2f) might suffice if unscaled, but since we want it 
            // to follow hitstop, we should ensure the prefab destroys itself via ParticleSystem stop action.
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
}
