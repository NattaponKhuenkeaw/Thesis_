using System;
using UnityEngine;

namespace Project.Core.ZoneState
{
    [CreateAssetMenu(fileName = "ZoneStateProvider", menuName = "Project/Core/ZoneStateProvider")]
    public class ZoneStateProvider : ScriptableObject
    {
        [SerializeField] private ZoneMode fallbackMode = ZoneMode.CombatZone;

        private RoomData currentRoom;

        public event Action<ZoneMode> OnZoneModeChanged;
        public event Action<RoomData> OnRoomChanged;

        public RoomData CurrentRoom
        {
            get
            {
                if (currentRoom == null)
                {
                    ResolveCurrentRoomFromActiveScene();
                }
                return currentRoom;
            }
        }

        public ZoneMode CurrentMode
        {
            get
            {
                RoomData room = CurrentRoom;
                return room != null ? room.ZoneMode : fallbackMode;
            }
        }

        private void OnEnable()
        {
            ResolveCurrentRoomFromActiveScene();
        }

        private void ResolveCurrentRoomFromActiveScene()
        {
            #if UNITY_EDITOR || true
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != null)
            {
                string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(activeSceneName))
                {
                    MapSO mapData = Resources.Load<MapSO>("MapSO/Test_MapSO");
                    if (mapData != null)
                    {
                        currentRoom = mapData.GetRoomData(activeSceneName);
                    }
                }
            }
            #endif
        }

        public void SetCurrentRoom(RoomData room)
        {
            if (currentRoom == room)
            {
                ForceBroadcast();
                return;
            }

            currentRoom = room;
            OnRoomChanged?.Invoke(currentRoom);
            OnZoneModeChanged?.Invoke(CurrentMode);
        }

        public void SetFallbackMode(ZoneMode mode)
        {
            fallbackMode = mode;

            if (CurrentRoom == null)
            {
                OnZoneModeChanged?.Invoke(fallbackMode);
            }
        }

        public void ForceBroadcast()
        {
            OnRoomChanged?.Invoke(CurrentRoom);
            OnZoneModeChanged?.Invoke(CurrentMode);
        }
    }
}
