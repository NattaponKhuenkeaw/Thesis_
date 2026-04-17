using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CombatCore;

namespace Project.Combat
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public class GenericVFXPlayer : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private AnimatorOverrideController overrideController;
        private List<VFXTransformKey> activeKeys;
        private Vector3 spawnScale = Vector3.one;
        private Vector3 spawnOrigin; // Absolute world or local origin

        private static readonly int PlayStateHash = Animator.StringToHash("Play");

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            
            if (animator.runtimeAnimatorController != null)
            {
                overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
                animator.runtimeAnimatorController = overrideController;

                bool found = false;
                foreach (var clip in overrideController.animationClips)
                {
                    if (clip.name == "VFX_Default")
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Debug.LogError($"[GenericVFXPlayer] {gameObject.name}: Base Animator Controller doesn't have a clip named 'VFX_Default'.");
                }
            }
        }

        public void Play(VFXDefinition def, bool flipX, List<VFXTransformKey> keys = null, float lifespan = -1f)
        {
            if (def == null || def.clip == null)
            {
                OnAnimationComplete();
                return;
            }

            activeKeys = keys;
            spawnScale = (def.localScale == Vector3.zero) ? Vector3.one : def.localScale;
            
            // Store origin. If parented, this is the relative spawn point. 
            // If world-space, this is the world position.
            spawnOrigin = transform.localPosition;

            // Apply direction via Rotation Y (180 deg) as requested
            // This mirrors both visuals and any internal X-offsets in the Keyframes!
            transform.localRotation = Quaternion.Euler(0, (flipX && def.flipWithCharacter) ? 180f : 0f, 0f);
            
            transform.localScale = spawnScale;

            if (def.tint.a <= 0f)
            {
                spriteRenderer.color = Color.white;
            }
            else
            {
                spriteRenderer.color = def.tint;
            }

            animator.updateMode = def.obeyHitstop ? AnimatorUpdateMode.Normal : AnimatorUpdateMode.UnscaledTime;

            if (overrideController != null)
            {
                overrideController["VFX_Default"] = def.clip;
                animator.Play(PlayStateHash, 0, 0f);
                
                // Determine duration: Use passed lifespan (from timeline) if provided, otherwise Clip length
                float duration = (lifespan > 0) ? lifespan : def.clip.length;

                StopAllCoroutines();
                StartCoroutine(PlaybackRoutine(duration));
            }
            else
            {
                Debug.LogError($"[GenericVFXPlayer] {gameObject.name}: No AnimatorOverrideController found.");
                OnAnimationComplete();
            }
        }

        private IEnumerator PlaybackRoutine(float duration)
        {
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                if (activeKeys != null && activeKeys.Count > 0)
                {
                    UpdateTrajectory(elapsed);
                }

                if (animator.updateMode == AnimatorUpdateMode.UnscaledTime)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                }
                else
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }
            }
            
            OnAnimationComplete();
        }

        private void UpdateTrajectory(float time)
        {
            float currentFrame = time * FrameDataConstants.LOGIC_FPS;
            
            VFXTransformKey prev = null;
            VFXTransformKey next = null;

            foreach (var key in activeKeys)
            {
                if (key.frame <= currentFrame)
                {
                    if (prev == null || key.frame > prev.frame) prev = key;
                }
                else
                {
                    if (next == null || key.frame < next.frame) next = key;
                }
            }

            if (prev != null && next != null)
            {
                float t = (currentFrame - prev.frame) / (float)(next.frame - prev.frame);
                ApplyKeyInterpolation(prev, next, t);
            }
            else if (prev != null)
            {
                ApplyKey(prev);
            }
        }

        private void ApplyKey(VFXTransformKey key)
        {
            // Position is an OFFSET relative to spawn origin
            transform.localPosition = spawnOrigin + key.position;
            
            // Apply flip-aware rotation
            Quaternion flipRot = Quaternion.Euler(0, transform.localRotation.eulerAngles.y, 0f);
            transform.localRotation = flipRot * Quaternion.Euler(key.rotation);
            
            transform.localScale = Vector3.Scale(spawnScale, key.scale);
        }

        private void ApplyKeyInterpolation(VFXTransformKey a, VFXTransformKey b, float t)
        {
            // Position is an OFFSET relative to spawn origin
            transform.localPosition = spawnOrigin + Vector3.Lerp(a.position, b.position, t);
            
            // Combine flipping with local rotation key
            Quaternion flipRot = Quaternion.Euler(0, transform.localRotation.eulerAngles.y, 0f);
            Quaternion keyRot = Quaternion.Euler(Vector3.Lerp(a.rotation, b.rotation, t));
            transform.localRotation = flipRot * keyRot;
            
            transform.localScale = Vector3.Scale(spawnScale, Vector3.Lerp(a.scale, b.scale, t));
        }

        public void OnAnimationComplete()
        {
            StopAllCoroutines();
            
            // Clear keys
            activeKeys = null;

            // Reset parenting before returning to pool
            if (VFXManager.Instance != null)
            {
                transform.SetParent(VFXManager.Instance.transform);
                VFXManager.Instance.ReturnToPool(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
