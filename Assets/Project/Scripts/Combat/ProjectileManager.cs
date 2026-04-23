using UnityEngine;

namespace Project.Combat
{
    public class ProjectileManager : MonoBehaviour
    {
        public static ProjectileManager Instance { get; private set; }

        [SerializeField] private ProjectileVFXManager runtimeManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (runtimeManager == null)
            {
                runtimeManager = GetComponent<ProjectileVFXManager>();
            }

            if (runtimeManager == null)
            {
                runtimeManager = ProjectileVFXManager.Instance;
            }
        }

        public ProjectileRuntime SpawnProjectile(ProjectileSpawnContext context)
        {
            if (runtimeManager == null)
            {
                Debug.LogError("ProjectileManager: ProjectileVFXManager is not assigned or present in the scene.");
                return null;
            }

            ProjectileType resolvedType = context.projectileType != ProjectileType.None
                ? context.projectileType
                : context.projectileData != null ? context.projectileData.projectileType : ProjectileType.None;

            return runtimeManager.SpawnProjectile(resolvedType, context);
        }

        public void UnregisterProjectile(ProjectileRuntime projectile)
        {
        }
    }
}
