using System.Collections.Generic;
using UnityEngine;
using CombatCore;

namespace Project.Combat
{
    public class ProjectileManager : MonoBehaviour
    {
        public static ProjectileManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int initialPoolSize = 10;
        
        private List<ProjectileRuntime> activeProjectiles = new List<ProjectileRuntime>();
        // Future: Add pooling if needed, for now focus on centralization.

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public ProjectileRuntime SpawnProjectile(ProjectileSpawnContext context)
        {
            GameObject projectileObject;
            ProjectileData data = context.projectileData;

            if (data != null && data.projectilePrefab != null)
            {
                projectileObject = Instantiate(
                    data.projectilePrefab,
                    context.origin,
                    Quaternion.identity,
                    context.projectileParent);
            }
            else
            {
                projectileObject = new GameObject(data != null ? data.name : "Projectile");
                projectileObject.transform.position = context.origin;
                if (context.projectileParent != null)
                {
                    projectileObject.transform.SetParent(context.projectileParent);
                }
            }

            ProjectileRuntime runtime = projectileObject.GetComponent<ProjectileRuntime>();
            if (runtime == null)
            {
                runtime = projectileObject.AddComponent<ProjectileRuntime>();
            }

            runtime.Initialize(this, context);
            activeProjectiles.Add(runtime);
            
            return runtime;
        }

        public void UnregisterProjectile(ProjectileRuntime projectile)
        {
            if (activeProjectiles.Contains(projectile))
            {
                activeProjectiles.Remove(projectile);
            }
        }
    }
}
