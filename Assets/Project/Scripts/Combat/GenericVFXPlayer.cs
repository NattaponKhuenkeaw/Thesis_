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
        private bool isFlipped;
        private bool flipTrajectoryX;
        private bool released;
        private Matrix4x4 releasedSpace;
        private Quaternion releasedRotation;
        private Vector3 releasedScale;
        public uint PlaybackVersion { get; private set; }

        private struct RendererSortingInfo
        {
            public Renderer renderer;
            public int originalLayerID;
            public int originalOrder;
        }

        private List<RendererSortingInfo> originalSortingInfos = new List<RendererSortingInfo>();
        private Camera targetCamera;
        private bool fitScreenSize;
        private bool adjustLayer;
        private Vector3 currentKeyScale = Vector3.one;
        private Vector3 currentKeyPositionOffset = Vector3.zero;

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

            CacheOriginalSorting();
        }

        private void CacheOriginalSorting()
        {
            originalSortingInfos.Clear();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                originalSortingInfos.Add(new RendererSortingInfo
                {
                    renderer = r,
                    originalLayerID = r.sortingLayerID,
                    originalOrder = r.sortingOrder
                });
            }
        }

        public void Play(VFXDefinition def, bool flipX, List<VFXTransformKey> keys = null, float lifespan = -1f, bool fitScreenSize = false, bool adjustLayer = false)
        {
            unchecked { PlaybackVersion++; }
            released = false;
            if (def == null || def.clip == null)
            {
                OnAnimationComplete();
                return;
            }

            this.fitScreenSize = fitScreenSize;
            this.adjustLayer = adjustLayer;
            currentKeyScale = Vector3.one;
            currentKeyPositionOffset = Vector3.zero;

            ResetSortingProperties();

            if (adjustLayer)
            {
                ApplySortingProperties();
            }

            activeKeys = keys;
            
            // Apply direction via Rotation Y (180 deg) as requested
            // This mirrors both visuals and any internal X-offsets in the Keyframes!
            isFlipped = flipX && def.flipWithCharacter;
            flipTrajectoryX = flipX && !def.isLocalSpace; // If world space and flipped, we must manually negate X trajectory
            
            transform.localRotation = Quaternion.Euler(0, isFlipped ? 180f : 0f, 0f);
            
            if (fitScreenSize)
            {
                targetCamera = Camera.main;
                UpdateFullScreenScaleAndPosition();
            }
            else
            {
                spawnScale = (def.localScale == Vector3.zero) ? Vector3.one : def.localScale;
                // Store origin. If parented, this is the relative spawn point. 
                // If world-space, this is the world position.
                spawnOrigin = transform.localPosition;
                transform.localScale = spawnScale;
            }

            spriteRenderer.flipX = false;
            spriteRenderer.flipY = false;

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

        private void LateUpdate()
        {
            if (fitScreenSize)
            {
                UpdateFullScreenScaleAndPosition();
            }
        }

        private void UpdateFullScreenScaleAndPosition()
        {
            if (targetCamera == null) return;

            // Follow camera + keyframe position offset
            Vector3 camPos = targetCamera.transform.position;
            Vector3 targetPos = camPos + currentKeyPositionOffset;
            targetPos.z = transform.position.z;
            transform.position = targetPos;
            spawnOrigin = transform.localPosition;

            // Fit scale
            float baseSize = Mathf.Max(targetCamera.orthographicSize * 2.5f, 15f);
            float cameraHeight = baseSize * 2f;
            float cameraWidth = cameraHeight * targetCamera.aspect;

            Vector2 spriteSize = Vector2.one;
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                spriteSize = spriteRenderer.sprite.bounds.size;
            }

            if (spriteSize.x > 0.001f && spriteSize.y > 0.001f)
            {
                spawnScale = new Vector3(
                    cameraWidth / spriteSize.x,
                    cameraHeight / spriteSize.y,
                    1f);
            }
            else
            {
                spawnScale = Vector3.one;
            }
            
            transform.localScale = Vector3.Scale(spawnScale, currentKeyScale);
        }

        private void ApplySortingProperties()
        {
            int targetLayerID = 0;
            int targetOrder = -9999;

            if (UltimateFlashOverlay.Instance != null && UltimateFlashOverlay.Instance.FlashRenderer != null)
            {
                targetLayerID = UltimateFlashOverlay.Instance.FlashRenderer.sortingLayerID;
                targetOrder = UltimateFlashOverlay.Instance.FlashRenderer.sortingOrder;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sortingLayerID = targetLayerID;
                r.sortingOrder = targetOrder;
            }
        }

        private void ResetSortingProperties()
        {
            foreach (var info in originalSortingInfos)
            {
                if (info.renderer != null)
                {
                    info.renderer.sortingLayerID = info.originalLayerID;
                    info.renderer.sortingOrder = info.originalOrder;
                }
            }
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
            Vector3 posOffset = key.position;
            if (flipTrajectoryX) posOffset.x = -posOffset.x;

            if (fitScreenSize)
            {
                currentKeyPositionOffset = posOffset;
                currentKeyScale = key.scale;

                // Apply rotation immediately
                Quaternion flipRot = Quaternion.Euler(0, isFlipped ? 180f : 0f, 0f);
                transform.localRotation = flipRot * Quaternion.Euler(key.rotation);
            }
            else
            {
                // Position is an OFFSET relative to spawn origin
                Quaternion flipRot = Quaternion.Euler(0, isFlipped ? 180f : 0f, 0f);
                ApplyPose(spawnOrigin + posOffset, flipRot * Quaternion.Euler(key.rotation), Vector3.Scale(spawnScale, key.scale));
            }
        }

        private void ApplyKeyInterpolation(VFXTransformKey a, VFXTransformKey b, float t)
        {
            Vector3 posA = a.position;
            Vector3 posB = b.position;
            if (flipTrajectoryX)
            {
                posA.x = -posA.x;
                posB.x = -posB.x;
            }

            if (fitScreenSize)
            {
                currentKeyPositionOffset = Vector3.Lerp(posA, posB, t);
                currentKeyScale = Vector3.Lerp(a.scale, b.scale, t);

                // Combine flipping with local rotation key
                Quaternion flipRot = Quaternion.Euler(0, isFlipped ? 180f : 0f, 0f);
                Quaternion keyRot = Quaternion.Euler(Vector3.Lerp(a.rotation, b.rotation, t));
                transform.localRotation = flipRot * keyRot;
            }
            else
            {
                // Position is an OFFSET relative to spawn origin
                Quaternion flipRot = Quaternion.Euler(0, isFlipped ? 180f : 0f, 0f);
                Quaternion keyRot = Quaternion.Euler(Vector3.Lerp(a.rotation, b.rotation, t));
                ApplyPose(spawnOrigin + Vector3.Lerp(posA, posB, t), flipRot * keyRot,
                    Vector3.Scale(spawnScale, Vector3.Lerp(a.scale, b.scale, t)));
            }
        }

        private void ApplyPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (released)
            {
                transform.SetPositionAndRotation(releasedSpace.MultiplyPoint3x4(position), releasedRotation * rotation);
                transform.localScale = Vector3.Scale(releasedScale, scale);
            }
            else
            {
                transform.localPosition = position; transform.localRotation = rotation; transform.localScale = scale;
            }
        }

        public void ReleaseToLifetime()
        {
            if (!gameObject.activeSelf || released) return;
            Transform parent = transform.parent;
            releasedSpace = parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
            releasedRotation = parent != null ? parent.rotation : Quaternion.identity;
            releasedScale = parent != null ? parent.lossyScale : Vector3.one;
            released = true;
            transform.SetParent(null, true);
            GetComponent<CutImpactVfxSource>()?.ReleaseToLifetime();
        }

        public void Stop()
        {
            OnAnimationComplete();
        }

        public void OnAnimationComplete()
        {
            StopAllCoroutines();
            
            // Clear keys
            activeKeys = null;
            spriteRenderer.flipX = false;
            spriteRenderer.flipY = false;

            ResetSortingProperties();
            fitScreenSize = false;
            adjustLayer = false;

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
