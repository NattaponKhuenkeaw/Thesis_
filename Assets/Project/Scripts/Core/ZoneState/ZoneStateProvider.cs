using System;
using UnityEngine;

namespace Project.Core.ZoneState
{
    public enum ClearingState
    {
        Resolving,
        Combat,
        RewardPending,
        Accessible
    }

    [CreateAssetMenu(fileName = "ZoneStateProvider", menuName = "Project/Core/ZoneStateProvider")]
    public class ZoneStateProvider : ScriptableObject
    {
        [SerializeField] private ZoneMode fallbackMode = ZoneMode.CombatZone;

        private RoomData currentRoom;
        private ClearingState clearingState;
        private bool clearingStateResolved;
        private int visit;

        public event Action<ZoneMode> OnZoneModeChanged;
        public event Action<RoomData> OnRoomChanged;
        public event Action<ZoneMode> OnAccessModeChanged;

        public int Visit => visit;
        public ClearingState Clearing => clearingStateResolved
            ? clearingState
            : (CurrentMode == ZoneMode.SafeZone ? ClearingState.Accessible : ClearingState.Combat);
        public ZoneMode AccessMode => Clearing == ClearingState.Accessible ? ZoneMode.SafeZone : ZoneMode.CombatZone;

        public static bool HasEncounterIntent(RoomData room)
        {
            if (room == null) return false;
            if (room.Encounter != null || room.RoomKind == RoomKind.Boss ||
                RouteOfferGenerator.NeedsGeneratedCombat(room.RoomKind)) return true;

            RoomLayoutHost host = RoomLayoutHost.Instance;
            return host != null && host.ActiveRoom == room && host.ActiveLayoutInstance != null &&
                host.ActiveLayoutInstance.GetComponentInChildren<CombatStageController>(true) != null;
        }

        public void BeginVisit()
        {
            visit++;
            SetClearing(ClearingState.Resolving);
        }

        public bool SetClearing(int expectedVisit, ClearingState state)
        {
            if (expectedVisit != visit) return false;
            SetClearing(state);
            return true;
        }

        private void SetClearing(ClearingState state)
        {
            if (clearingStateResolved && clearingState == state) return;
            clearingState = state;
            clearingStateResolved = true;
            OnAccessModeChanged?.Invoke(AccessMode);
        }

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
            clearingStateResolved = false;
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
                OnAccessModeChanged?.Invoke(AccessMode);
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
            OnAccessModeChanged?.Invoke(AccessMode);
        }

        public void SetFallbackMode(ZoneMode mode)
        {
            fallbackMode = mode;

            if (CurrentRoom == null)
            {
                OnZoneModeChanged?.Invoke(fallbackMode);
                OnAccessModeChanged?.Invoke(AccessMode);
            }
        }

        public void ForceBroadcast()
        {
            OnRoomChanged?.Invoke(CurrentRoom);
            OnZoneModeChanged?.Invoke(CurrentMode);
            OnAccessModeChanged?.Invoke(AccessMode);
        }
    }
}
