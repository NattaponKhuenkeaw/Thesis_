using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Project.Combat
{
    public class ProjectileVFXManager : MonoBehaviour
    {
        public static ProjectileVFXManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private ProjectileLibrary projectileLibrary;
        [SerializeField] private GenericProjectilePlayer defaultProjectilePrefab;
        public IProjectileLibrary Library => projectileLibrary;
        internal int ClearVersion { get; private set; }

        [Header("Pooling")]
        [SerializeField] private int initialPoolSize = 6;
        [SerializeField] private int maxPoolSize = 32;

        private readonly Dictionary<int, ObjectPool<GenericProjectilePlayer>> poolsByDefinition = new Dictionary<int, ObjectPool<GenericProjectilePlayer>>();
        private readonly Dictionary<GenericProjectilePlayer, ObjectPool<GenericProjectilePlayer>> ownership = new Dictionary<GenericProjectilePlayer, ObjectPool<GenericProjectilePlayer>>();

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
            ProjectileFeedbackEvents.FeedbackRequested += OnProjectileFeedbackRequested;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            ProjectileFeedbackEvents.FeedbackRequested -= OnProjectileFeedbackRequested;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            ClearAllActiveProjectiles();
        }

        public void ClearAllActiveProjectiles()
        {
            ClearVersion++;
            if (ownership.Count > 0)
            {
                var activePlayers = new List<GenericProjectilePlayer>(ownership.Keys);
                for (int i = 0; i < activePlayers.Count; i++)
                {
                    GenericProjectilePlayer player = activePlayers[i];
                    if (player != null)
                    {
                        Release(player);
                    }
                }

                ownership.Clear();
            }

            // In case any runtime instances are active in the scene or under this manager
            ProjectileRuntime[] activeRuntimes = FindObjectsByType<ProjectileRuntime>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < activeRuntimes.Length; i++)
            {
                ProjectileRuntime runtime = activeRuntimes[i];
                if (runtime == null)
                {
                    continue;
                }

                GenericProjectilePlayer player = runtime.GetComponent<GenericProjectilePlayer>();
                if (player != null && ownership.ContainsKey(player))
                {
                    Release(player);
                }
                else
                {
                    runtime.ResetRuntime();
                    runtime.gameObject.SetActive(false);
                }
            }
        }

        public GenericProjectilePlayer SpawnProjectilePlayer(ProjectileType type, ProjectileSpawnContext context)
        {
            ProjectileData data = ResolveProjectileData(type, context);
            if (data == null)
            {
                Debug.LogWarning($"ProjectileVFXManager: No ProjectileData resolved for type {type}.");
                return null;
            }

            int poolKey = data.GetInstanceID();
            ObjectPool<GenericProjectilePlayer> pool = GetOrCreatePool(poolKey, data);
            GenericProjectilePlayer player = pool.Get();
            ownership[player] = pool;

            if (context.projectileParent != null)
            {
                player.transform.SetParent(context.projectileParent, true);
            }
            else
            {
                player.transform.SetParent(transform, true);
            }

            ProjectileType resolvedType = type != ProjectileType.None ? type : data.projectileType;
            ProjectileSpawnContext resolvedContext = context;
            resolvedContext.projectileData = data;
            resolvedContext.projectileType = resolvedType;

            // Set position before play/activation
            player.transform.position = data.ResolveSpawnPosition(resolvedContext);

            player.Play(this, resolvedType, data, resolvedContext);
            return player;
        }

        public ProjectileRuntime SpawnProjectile(ProjectileType type, ProjectileSpawnContext context)
        {
            GenericProjectilePlayer player = SpawnProjectilePlayer(type, context);
            return player != null ? player.Runtime : null;
        }

        public void Release(GenericProjectilePlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (!ownership.TryGetValue(player, out ObjectPool<GenericProjectilePlayer> pool))
            {
                player.gameObject.SetActive(false);
                return;
            }

            ownership.Remove(player);
            pool.Release(player);
        }

        private ProjectileData ResolveProjectileData(ProjectileType type, ProjectileSpawnContext context)
        {
            if (context.projectileData != null)
            {
                return context.projectileData;
            }

            if (type != ProjectileType.None && projectileLibrary != null && projectileLibrary.TryGetDefinition(type, out ProjectileData data))
            {
                return data;
            }

            if (context.projectileType != ProjectileType.None
                && projectileLibrary != null
                && projectileLibrary.TryGetDefinition(context.projectileType, out ProjectileData contextData))
            {
                return contextData;
            }

            return null;
        }

        private ObjectPool<GenericProjectilePlayer> GetOrCreatePool(int poolKey, ProjectileData data)
        {
            if (poolsByDefinition.TryGetValue(poolKey, out ObjectPool<GenericProjectilePlayer> existing))
            {
                return existing;
            }

            ObjectPool<GenericProjectilePlayer> createdPool = null;
            createdPool = new ObjectPool<GenericProjectilePlayer>(
                () => CreatePlayerInstance(data),
                player => player.gameObject.SetActive(true),
                player => player.PrepareForPool(),
                player =>
                {
                    if (player != null)
                    {
                        Destroy(player.gameObject);
                    }
                },
                true,
                initialPoolSize,
                maxPoolSize);

            poolsByDefinition[poolKey] = createdPool;

            for (int i = 0; i < initialPoolSize; i++)
            {
                GenericProjectilePlayer player = createdPool.Get();
                createdPool.Release(player);
            }

            return createdPool;
        }

        private GenericProjectilePlayer CreatePlayerInstance(ProjectileData data)
        {
            GenericProjectilePlayer player = null;

            if (data != null && data.projectilePrefab != null)
            {
                GameObject instance = Instantiate(data.projectilePrefab, transform);
                player = instance.GetComponent<GenericProjectilePlayer>();
                if (player == null)
                {
                    player = instance.AddComponent<GenericProjectilePlayer>();
                }
            }
            else if (defaultProjectilePrefab != null)
            {
                player = Instantiate(defaultProjectilePrefab, transform);
            }
            else
            {
                GameObject fallback = new GameObject(data != null ? data.name : "Projectile");
                fallback.transform.SetParent(transform);
                player = fallback.AddComponent<GenericProjectilePlayer>();
            }

            if (player.GetComponent<ProjectileRuntime>() == null)
            {
                player.gameObject.AddComponent<ProjectileRuntime>();
            }

            player.BindManager(this);
            player.gameObject.SetActive(false);
            return player;
        }

        private void OnProjectileFeedbackRequested(ProjectileFeedbackEvent feedbackEvent)
        {
            if ((feedbackEvent.Definition.vfxType != VFXType.None || feedbackEvent.Definition.vfxClip != null) && VFXManager.Instance != null)
            {
                if (feedbackEvent.Definition.vfxClip != null)
                {
                    VFXManager.Instance.Spawn(feedbackEvent.Definition.vfxClip, feedbackEvent.Position, feedbackEvent.FlipX);
                }
                else
                {
                    VFXManager.Instance.Spawn(feedbackEvent.Definition.vfxType, feedbackEvent.Position, feedbackEvent.FlipX);
                }
            }

            if (feedbackEvent.Definition.audio != null && GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.PlayHitSFX(feedbackEvent.Definition.audio);
            }
        }
    }
}
