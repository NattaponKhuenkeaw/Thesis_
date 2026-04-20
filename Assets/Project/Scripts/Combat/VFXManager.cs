using System.Collections.Generic;
using UnityEngine;
using Project.Combat;

namespace Project.Combat
{
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Refs")]
        [SerializeField] private VFXLibrary library;
        public VFXLibrary InstanceLibrary => library;
        [SerializeField] private GenericVFXPlayer vfxPrefab;
        public GenericVFXPlayer VFXPrefab => vfxPrefab;
        
        [Header("Settings")]
        [SerializeField] private int initialPoolSize = 10;
        
        private Queue<GenericVFXPlayer> pool = new Queue<GenericVFXPlayer>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            InitializePool();
        }

        private void InitializePool()
        {
            if (vfxPrefab == null) return;

            for (int i = 0; i < initialPoolSize; i++)
            {
                pool.Enqueue(CreateNewInstance());
            }
        }

        private GenericVFXPlayer CreateNewInstance()
        {
            GenericVFXPlayer instance = Instantiate(vfxPrefab, transform);
            instance.gameObject.SetActive(false);
            return instance;
        }

        /// <summary>
        /// Main entry point for spawning library-driven VFX.
        /// </summary>
        public void Spawn(VFXType type, Vector3 position, bool flipX = false, float rotationZ = 0f, List<VFXTransformKey> keys = null, Transform parent = null, float lifespan = -1f)
        {
            SpawnResult(type, position, flipX, rotationZ, keys, parent, lifespan);
        }

        public GenericVFXPlayer SpawnResult(VFXType type, Vector3 position, bool flipX = false, float rotationZ = 0f, List<VFXTransformKey> keys = null, Transform parent = null, float lifespan = -1f)
        {
            if (type == VFXType.None) return null;
            
            if (library == null)
            {
                Debug.LogWarning("VFXManager: Library is not assigned! Cannot spawn VFX.");
                return null;
            }

            VFXDefinition def = library.GetDefinition(type);
            if (def == null)
            {
                Debug.LogWarning($"VFXManager: No definition found in library for type {type}");
                return null;
            }

            GenericVFXPlayer player = GetFromPool();
            
            // Handle Local Space (Parenting)
            if (def.isLocalSpace && parent != null)
            {
                player.transform.SetParent(parent);
                player.transform.localPosition = position;
            }
            else
            {
                player.transform.SetParent(transform); // Stay under manager
                player.transform.position = position;
            }

            // Initial rotation (Z only, Y flip is handled in GenericVFXPlayer.Play)
            player.transform.localRotation = Quaternion.Euler(0, 0, rotationZ);
            
            player.gameObject.SetActive(true);
            player.Play(def, flipX, keys, lifespan);
            return player;
        }

        private GenericVFXPlayer GetFromPool()
        {
            while (pool.Count > 0)
            {
                GenericVFXPlayer player = pool.Dequeue();
                if (player != null) return player;
            }
            return CreateNewInstance();
        }

        public void ReturnToPool(GenericVFXPlayer player)
        {
            if (player == null) return;
            
            player.gameObject.SetActive(false);
            
            // Safety: Ensure we don't return an object that's already in the queue or destroyed
            if (!pool.Contains(player))
            {
                pool.Enqueue(player);
            }
        }
    }
}
