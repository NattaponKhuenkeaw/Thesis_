using UnityEngine;

public class VFXSpawner : MonoBehaviour
{
    private PlayerMovement movement;

    private void Awake()
    {
        movement = GetComponentInParent<PlayerMovement>();
    }

    public Project.Combat.GenericVFXPlayer SpawnVFX(VFXEntry entry)
    {
        if (entry == null) return null;

        // Apply facing direction
        float facingDirection = movement != null ? movement.FacingDirection : 1f;
        bool isFlipped = facingDirection < 0;

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

        // 1. Try Library-driven VFX first (Optimized path)
        if ((entry.vfxType != Project.Combat.VFXType.None || entry.vfxClip != null) && Project.Combat.VFXManager.Instance != null)
        {
            var def = entry.vfxType != Project.Combat.VFXType.None 
                ? Project.Combat.VFXManager.Instance?.InstanceLibrary?.GetDefinition(entry.vfxType)
                : null;
            Vector3 spawnPos = localOffset;
            bool flipXForVFX = isFlipped;

            // Logic Fix: For Local Space (parented), the parent's transformation (which is already rotated 180 by Player)
            // handles both the mirroring of the offset and the visual orientation.
            // We only manually flip and spawn in world space if NOT local space.
            if (def == null || !def.isLocalSpace)
            {
                spawnPos.x *= facingDirection;
                spawnPos = transform.position + spawnPos;
            }
            else
            {
                // Parent handles rotation, so we don't double-flip the child
                flipXForVFX = false;
            }

            // Calculate lifespan in seconds
            float lifespan = entry.duration / CombatCore.FrameDataConstants.LOGIC_FPS;
            AnimationClip clipToPlay = entry.vfxClip;

            return Project.Combat.VFXManager.Instance.SpawnResult(entry.vfxType, spawnPos, flipXForVFX, localRotation.z, entry.transformKeys, transform, lifespan, clipToPlay);
        }

        // 2. Fallback to Prefab instantiation (Legacy path)
        if (entry.vfxPrefab != null)
        {
            // ... legacy code stays same except it returns null since we don't track legacy instantiated objects yet ...
            // (Standardization: library VFX is preferred for pooling and tracking)
            localOffset.x *= facingDirection;
            Vector3 spawnPosition = transform.position + localOffset;

            Quaternion rotation = Quaternion.Euler(localRotation);
            if (isFlipped)
            {
                Vector3 euler = rotation.eulerAngles;
                euler.y += 180f;
                rotation = Quaternion.Euler(euler);
            }

            GameObject vfxInstance = Instantiate(entry.vfxPrefab, spawnPosition, rotation);
            vfxInstance.transform.localScale = localScale;

            var ps = vfxInstance.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
                main.useUnscaledTime = false;
            }
            else
            {
                Destroy(vfxInstance, 5f);
            }
        }

        return null;
    }
}
