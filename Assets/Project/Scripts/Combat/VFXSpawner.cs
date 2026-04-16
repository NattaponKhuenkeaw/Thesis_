using UnityEngine;

public class VFXSpawner : MonoBehaviour
{
    private PlayerMovement movement;

    private void Awake()
    {
        movement = GetComponentInParent<PlayerMovement>();
    }

    public void SpawnVFX(VFXEntry entry)
    {
        if (entry == null || entry.vfxPrefab == null) return;

        // Apply facing direction
        float facingDirection = movement != null ? movement.FacingDirection : 1f;

        // Find initial spawn position from the first transform key if available
        Vector3 localOffset = Vector3.zero;
        Vector3 localRotation = Vector3.zero;
        Vector3 localScale = Vector3.one;

        if (entry.transformKeys != null && entry.transformKeys.Count > 0)
        {
            var firstKey = entry.transformKeys[0];
            localOffset = firstKey.position;
            localRotation = firstKey.rotation;
            localScale = firstKey.scale;
        }

        // Flip X offset based on facing
        localOffset.x *= facingDirection;

        // Calculate world spawn
        Vector3 spawnPosition = transform.position + localOffset;

        // Calculate rotation
        // For 2D fighting games, typically we just flip the Y axis 180 degrees.
        Quaternion rotation = Quaternion.Euler(localRotation);
        if (facingDirection < 0)
        {
            // Simple flip
            Vector3 euler = rotation.eulerAngles;
            euler.y += 180f;
            rotation = Quaternion.Euler(euler);
        }

        GameObject vfxInstance = Instantiate(entry.vfxPrefab, spawnPosition, rotation);
        
        // Scale
        vfxInstance.transform.localScale = localScale;

        // Ensure it cleans itself up
        var ps = vfxInstance.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            // Action VFX obeys scaled time by default, so it freezes during hitstop.
            main.useUnscaledTime = false;
        }
        else
        {
            // Fallback destruction
            Destroy(vfxInstance, 5f);
        }
    }
}
