using UnityEngine;

namespace Project.Combat
{
    [RequireComponent(typeof(ProjectileRuntime))]
    public class GenericProjectilePlayer : MonoBehaviour
    {
        private static readonly int SpawnStateHash = Animator.StringToHash("Spawn");
        private static readonly int LoopStateHash = Animator.StringToHash("Loop");
        private static readonly int PlayStateHash = Animator.StringToHash("Play");

        private ProjectileVFXManager owningManager;
        private Animator cachedAnimator;
        private AnimatorOverrideController overrideController;
        private ProjectileRuntime runtime;

        public ProjectileRuntime Runtime => runtime;

        private void Awake()
        {
            runtime = GetComponent<ProjectileRuntime>();
            cachedAnimator = GetComponentInChildren<Animator>();

            if (cachedAnimator != null && cachedAnimator.runtimeAnimatorController != null)
            {
                overrideController = new AnimatorOverrideController(cachedAnimator.runtimeAnimatorController);
                cachedAnimator.runtimeAnimatorController = overrideController;
            }
        }

        public void BindManager(ProjectileVFXManager manager)
        {
            owningManager = manager;
        }

        public void Play(ProjectileVFXManager manager, ProjectileType projectileType, ProjectileData data, ProjectileSpawnContext context)
        {
            owningManager = manager;

            if (overrideController != null && data != null)
            {
                AnimationClip spawn = data.spawnClip != null ? data.spawnClip : data.loopClip;
                AnimationClip loop = data.loopClip;

                var overrides = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
                overrideController.GetOverrides(overrides);

                for (int i = 0; i < overrides.Count; i++)
                {
                    if (overrides[i].Key != null)
                    {
                        string clipName = overrides[i].Key.name.ToLowerInvariant();
                        if (clipName.Contains("spawn") || clipName.Contains("start"))
                        {
                            overrides[i] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, spawn);
                        }
                        else
                        {
                            overrides[i] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, loop);
                        }
                    }
                }

                overrideController.ApplyOverrides(overrides);
            }

            gameObject.SetActive(true);
            runtime.Initialize(this, manager, projectileType, data, context);
        }

        public void ReleaseToPool()
        {
            if (owningManager == null)
            {
                gameObject.SetActive(false);
                return;
            }

            owningManager.Release(this);
        }

        public void PrepareForPool()
        {
            runtime.ResetRuntime();

            if (cachedAnimator != null)
            {
                cachedAnimator.Rebind();
                cachedAnimator.Update(0f);
            }

            transform.SetParent(owningManager != null ? owningManager.transform : null);
            gameObject.SetActive(false);
        }

        public bool TryPlaySpawnAnimation()
        {
            if (cachedAnimator == null || cachedAnimator.runtimeAnimatorController == null)
            {
                return false;
            }

            if (cachedAnimator.HasState(0, SpawnStateHash))
            {
                cachedAnimator.Play(SpawnStateHash, 0, 0f);
                return true;
            }

            return false;
        }

        public void PlayLoopAnimation()
        {
            if (cachedAnimator == null || cachedAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            if (cachedAnimator.HasState(0, LoopStateHash))
            {
                cachedAnimator.Play(LoopStateHash, 0, 0f);
            }
            else if (cachedAnimator.HasState(0, PlayStateHash))
            {
                cachedAnimator.Play(PlayStateHash, 0, 0f);
            }
        }

        public bool IsSpawnAnimationComplete()
        {
            if (cachedAnimator == null || cachedAnimator.runtimeAnimatorController == null)
            {
                return true;
            }

            if (!cachedAnimator.HasState(0, SpawnStateHash))
            {
                return true;
            }

            AnimatorStateInfo state = cachedAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != SpawnStateHash)
            {
                return true;
            }

            return state.normalizedTime >= 1f;
        }
    }
}
