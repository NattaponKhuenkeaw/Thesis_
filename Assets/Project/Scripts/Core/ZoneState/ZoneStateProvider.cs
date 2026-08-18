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
                EnsureRoomMatchesActiveScene();
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

        /// <summary>
        /// ScriptableObject state can linger across scene loads. Refresh when the active scene changed.
        /// </summary>
        private void EnsureRoomMatchesActiveScene()
        {
            string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(activeSceneName))
            {
                return;
            }

            if (currentRoom != null
                && !string.IsNullOrWhiteSpace(currentRoom.SceneName)
                && string.Equals(currentRoom.SceneName, activeSceneName, System.StringComparison.Ordinal))
            {
                return;
            }

            RoomData previous = currentRoom;
            ResolveCurrentRoomFromActiveScene();
            if (currentRoom != previous)
            {
                OnRoomChanged?.Invoke(currentRoom);
                OnZoneModeChanged?.Invoke(currentRoom != null ? currentRoom.ZoneMode : fallbackMode);
            }
        }

        private void ResolveCurrentRoomFromActiveScene()
        {
            if (RoomLayoutHost.Instance != null && RoomLayoutHost.Instance.ActiveRoom != null)
            {
                currentRoom = RoomLayoutHost.Instance.ActiveRoom;
                return;
            }

            string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(activeSceneName))
            {
                return;
            }

            MapSO mapData = Resources.Load<MapSO>("MapSO/Test_MapSO");
            if (mapData != null)
            {
                currentRoom = mapData.ResolveRoom(activeSceneName);
            }
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
