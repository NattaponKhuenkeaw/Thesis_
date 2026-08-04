using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Lives in a RunShell scene. Owns the mount point for room layout prefabs.
    /// Player / persistent managers must NOT live under <see cref="layoutRoot"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomLayoutHost : MonoBehaviour
    {
        public static RoomLayoutHost Instance { get; private set; }

        private static readonly string[] DefaultBakedObjectNames =
        {
            "EnvironmentDependency",
            "RoomWaveManager"
        };

        [Header("Mount")]
        [SerializeField] private Transform layoutRoot;

        [Header("Bootstrap (Optional)")]
        [SerializeField] private RoomData bootstrapRoom;

        [Header("Baked Scene Content")]
        [Tooltip("Scene-authored room pieces to hide while a layout prefab is mounted (combat EnvironmentDependency, RoomWaveManager, etc.).")]
        [SerializeField] private GameObject[] bakedSceneContent;

        private GameObject activeLayoutInstance;
        private RoomData activeRoom;
        private bool bakedContentHidden;

        public RoomData ActiveRoom => activeRoom;
        public GameObject ActiveLayoutInstance => activeLayoutInstance;
        public bool HasActiveLayout => activeLayoutInstance != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[RoomLayoutHost] Duplicate host '{name}' disabled. Active host is '{Instance.name}'.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (layoutRoot == null)
            {
                layoutRoot = transform;
            }

            CaptureDefaultBakedContentIfEmpty();

            if (bootstrapRoom != null && bootstrapRoom.UsesLayoutSwap && activeLayoutInstance == null)
            {
                MountRoom(bootstrapRoom);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Finds an existing host or creates one in the active scene under a LayoutRoot.
        /// </summary>
        public static RoomLayoutHost EnsureInActiveScene()
        {
            if (Instance != null && Instance.isActiveAndEnabled)
            {
                Instance.CaptureDefaultBakedContentIfEmpty();
                return Instance;
            }

            RoomLayoutHost existing = Object.FindFirstObjectByType<RoomLayoutHost>();
            if (existing != null)
            {
                existing.enabled = true;
                existing.CaptureDefaultBakedContentIfEmpty();
                if (Instance == null)
                {
                    Instance = existing;
                }

                return existing;
            }

            GameObject root = new GameObject("RoomLayoutHost");
            RoomLayoutHost host = root.AddComponent<RoomLayoutHost>();
            host.layoutRoot = root.transform;
            host.CaptureDefaultBakedContentIfEmpty();
            Debug.Log("[RoomLayoutHost] Auto-created RoomLayoutHost in active scene.");
            return host;
        }

        public bool MountRoom(RoomData room)
        {
            if (room == null)
            {
                Debug.LogError("[RoomLayoutHost] MountRoom called with null RoomData.", this);
                return false;
            }

            if (!room.UsesLayoutSwap)
            {
                Debug.LogError($"[RoomLayoutHost] Room '{room.RoomId}' has no layoutPrefab. Use scene load instead.", this);
                return false;
            }

            if (layoutRoot == null)
            {
                layoutRoot = transform;
            }

            CaptureDefaultBakedContentIfEmpty();
            ClearActiveLayout(restoreBakedContent: false);
            SetBakedSceneContentActive(false);

            activeLayoutInstance = Instantiate(room.LayoutPrefab, layoutRoot);
            activeLayoutInstance.name = $"{room.LayoutPrefab.name}_Runtime";
            activeRoom = room;

            Debug.Log($"[RoomLayoutHost] Mounted room '{room.RoomId}' from prefab '{room.LayoutPrefab.name}'.", this);
            return true;
        }

        public void ClearActiveLayout()
        {
            ClearActiveLayout(restoreBakedContent: true);
        }

        private void ClearActiveLayout(bool restoreBakedContent)
        {
            if (activeLayoutInstance != null)
            {
                Destroy(activeLayoutInstance);
                activeLayoutInstance = null;
            }

            if (layoutRoot != null)
            {
                for (int i = layoutRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(layoutRoot.GetChild(i).gameObject);
                }
            }

            activeRoom = null;

            if (restoreBakedContent)
            {
                SetBakedSceneContentActive(true);
            }
        }

        public void SetBakedSceneContentActive(bool active)
        {
            CaptureDefaultBakedContentIfEmpty();

            if (bakedSceneContent == null)
            {
                return;
            }

            for (int i = 0; i < bakedSceneContent.Length; i++)
            {
                if (bakedSceneContent[i] != null)
                {
                    bakedSceneContent[i].SetActive(active);
                }
            }

            bakedContentHidden = !active;
        }

        public EntrancePoint FindEntrance(string entranceId)
        {
            if (activeLayoutInstance == null)
            {
                return null;
            }

            EntrancePoint[] entrances = activeLayoutInstance.GetComponentsInChildren<EntrancePoint>(true);
            EntrancePoint fallback = null;

            for (int i = 0; i < entrances.Length; i++)
            {
                EntrancePoint entrance = entrances[i];
                if (entrance == null)
                {
                    continue;
                }

                if (string.Equals(entrance.EntranceId, entranceId, System.StringComparison.Ordinal))
                {
                    return entrance;
                }

                if (fallback == null)
                {
                    fallback = entrance;
                }
            }

            return fallback;
        }

        public SceneBoundaryController FindBoundaryController()
        {
            if (activeLayoutInstance == null)
            {
                return null;
            }

            return activeLayoutInstance.GetComponentInChildren<SceneBoundaryController>(true);
        }

        private void CaptureDefaultBakedContentIfEmpty()
        {
            if (bakedSceneContent != null && bakedSceneContent.Length > 0)
            {
                return;
            }

            var found = new System.Collections.Generic.List<GameObject>(DefaultBakedObjectNames.Length);
            for (int i = 0; i < DefaultBakedObjectNames.Length; i++)
            {
                string objectName = DefaultBakedObjectNames[i];
                GameObject go = GameObject.Find(objectName);
                if (go != null)
                {
                    found.Add(go);
                }
            }

            // Also hide root-level RoomWaveManagers that may be renamed.
            RoomWaveManager[] waveManagers = Object.FindObjectsByType<RoomWaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < waveManagers.Length; i++)
            {
                RoomWaveManager manager = waveManagers[i];
                if (manager == null)
                {
                    continue;
                }

                // Skip wave managers that live under a mounted layout.
                if (activeLayoutInstance != null && manager.transform.IsChildOf(activeLayoutInstance.transform))
                {
                    continue;
                }

                if (layoutRoot != null && manager.transform.IsChildOf(layoutRoot))
                {
                    continue;
                }

                if (!found.Contains(manager.gameObject))
                {
                    found.Add(manager.gameObject);
                }
            }

            bakedSceneContent = found.ToArray();
            if (bakedSceneContent.Length > 0)
            {
                Debug.Log($"[RoomLayoutHost] Captured {bakedSceneContent.Length} baked scene content object(s) to hide during layout mounts.", this);
            }
        }
    }
}
