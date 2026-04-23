using UnityEngine;

namespace Project.Combat
{
    [RequireComponent(typeof(ProjectileRuntime))]
    public class GenericProjectilePlayer : MonoBehaviour
    {
        private static readonly int SpawnStateHash = Animator.StringToHash("Spawn");
        private static readonly int LoopStateHash = Animator.StringToHash("Loop");

        private ProjectileVFXManager owningManager;
        private Animator cachedAnimator;
        private AnimatorOverrideController overrideController;
        private ProjectileRuntime runtime;

        public ProjectileRuntime Runtime => runtime;

        private void Awake()
        {
            runtime = GetComponent<ProjectileRuntime>();
            cachedAnimator = GetComponent<Animator>();

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
                // Fallback: If spawnClip is null, use loopClip for both
                AnimationClip spawn = data.spawnClip != null ? data.spawnClip : data.loopClip;
                AnimationClip loop = data.loopClip;

                overrideController["Projectile_Spawn"] = spawn;
                overrideController["Projectile_Loop"] = loop;
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

            // We check for the generic state name in the animator controller
            if (!cachedAnimator.HasState(0, SpawnStateHash))
            {
                return false;
            }

            cachedAnimator.Play(SpawnStateHash, 0, 0f);
            return true;
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
