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

        [Header("Mount")]
        [SerializeField] private Transform layoutRoot;

        [Header("Bootstrap (Optional)")]
        [SerializeField] private RoomData bootstrapRoom;

        private GameObject activeLayoutInstance;
        private RoomData activeRoom;

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

            ClearActiveLayout();

            activeLayoutInstance = Instantiate(room.LayoutPrefab, layoutRoot);
            activeLayoutInstance.name = $"{room.LayoutPrefab.name}_Runtime";
            activeRoom = room;

            Debug.Log($"[RoomLayoutHost] Mounted room '{room.RoomId}' from prefab '{room.LayoutPrefab.name}'.", this);
            return true;
        }

        public void ClearActiveLayout()
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
    }
}
