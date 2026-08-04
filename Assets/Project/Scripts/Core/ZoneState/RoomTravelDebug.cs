using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Runtime debug traveler for any RoomData in MapSO.
    /// Drop on any active object, Play, then press the toggle key (default F10).
    /// </summary>
    public sealed class RoomTravelDebug : MonoBehaviour
    {
        private const string DefaultMapResourcePath = "MapSO/Test_MapSO";

        [Header("Data")]
        [SerializeField] private MapSO mapData;
        [SerializeField] private string entranceId = "Default";

        [Header("UI")]
        [SerializeField] private Key toggleKey = Key.F10;
        [SerializeField] private bool showOnStart;
        [SerializeField] private bool visible = true;

        [Header("Selection")]
        [SerializeField] private int selectedIndex;

        private readonly List<RoomData> rooms = new List<RoomData>(16);
        private Vector2 scroll;
        private bool filterEnabled;
        private RoomKind filterKindValue = RoomKind.Combat;
        private Rect windowRect = new Rect(24f, 24f, 420f, 460f);

        private void Awake()
        {
            if (mapData == null)
            {
                mapData = Resources.Load<MapSO>(DefaultMapResourcePath);
            }

            RefreshRoomList();
            visible = showOnStart;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Room Travel Debug");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label($"Toggle: {toggleKey} | Map: {(mapData != null ? mapData.name : "MISSING")}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
            {
                RefreshRoomList();
            }

            filterEnabled = GUILayout.Toggle(filterEnabled, "Filter Kind", GUILayout.Width(100f));
            if (filterEnabled)
            {
                if (GUILayout.Button("<", GUILayout.Width(28f)))
                {
                    filterKindValue = PreviousKind(filterKindValue);
                    RefreshRoomList();
                }

                GUILayout.Label(filterKindValue.ToString(), GUILayout.Width(80f));

                if (GUILayout.Button(">", GUILayout.Width(28f)))
                {
                    filterKindValue = NextKind(filterKindValue);
                    RefreshRoomList();
                }
            }
            GUILayout.EndHorizontal();

            entranceId = GUILayout.TextField(string.IsNullOrEmpty(entranceId) ? "Default" : entranceId);

            if (rooms.Count == 0)
            {
                GUILayout.Label("No rooms found. Assign MapSO or add RoomData to Test_MapSO.");
                GUI.DragWindow();
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, rooms.Count - 1);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(280f));
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomData room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                string label = $"{room.DisplayName}  [{room.RoomKind}]  id={room.RoomId}";
                if (room.UsesLayoutSwap)
                {
                    label += "  (layout)";
                }

                bool selected = GUILayout.Toggle(selectedIndex == i, label, "Button");
                if (selected)
                {
                    selectedIndex = i;
                }
            }
            GUILayout.EndScrollView();

            RoomData current = rooms[selectedIndex];
            GUI.enabled = current != null && Application.isPlaying;
            if (GUILayout.Button($"Go To: {(current != null ? current.RoomId : "-")}", GUILayout.Height(36f)))
            {
                GoToSelected();
            }
            GUI.enabled = true;

            if (SceneTransitionManager.Instance == null)
            {
                GUILayout.Label("Waiting for SceneTransitionManager.Instance...");
            }
            else if (current != null && current.UsesLayoutSwap && RoomLayoutHost.Instance == null)
            {
                GUILayout.Label("Warning: layout room needs RoomLayoutHost in the active shell.");
            }

            GUI.DragWindow();
        }

        [ContextMenu("Refresh Room List")]
        public void RefreshRoomList()
        {
            rooms.Clear();

            if (mapData == null)
            {
                mapData = Resources.Load<MapSO>(DefaultMapResourcePath);
            }

            if (mapData == null || mapData.Rooms == null)
            {
                return;
            }

            for (int i = 0; i < mapData.Rooms.Count; i++)
            {
                RoomData room = mapData.Rooms[i];
                if (room == null)
                {
                    continue;
                }

                if (filterEnabled && room.RoomKind != filterKindValue)
                {
                    continue;
                }

                rooms.Add(room);
            }

            if (rooms.Count > 0)
            {
                selectedIndex = Mathf.Clamp(selectedIndex, 0, rooms.Count - 1);
            }
            else
            {
                selectedIndex = 0;
            }
        }

        [ContextMenu("Go To Selected Room")]
        public void GoToSelected()
        {
            if (rooms.Count == 0)
            {
                RefreshRoomList();
            }

            if (rooms.Count == 0 || selectedIndex < 0 || selectedIndex >= rooms.Count)
            {
                Debug.LogWarning("[RoomTravelDebug] No room selected.", this);
                return;
            }

            GoToRoom(rooms[selectedIndex]);
        }

        public void GoToRoomId(string roomId)
        {
            if (mapData == null)
            {
                mapData = Resources.Load<MapSO>(DefaultMapResourcePath);
            }

            RoomData room = mapData != null ? mapData.ResolveRoom(roomId) : null;
            GoToRoom(room);
        }

        public void GoToRoom(RoomData room)
        {
            if (room == null)
            {
                Debug.LogError("[RoomTravelDebug] RoomData is null.", this);
                return;
            }

            if (SceneTransitionManager.Instance == null)
            {
                Debug.LogError("[RoomTravelDebug] SceneTransitionManager.Instance is missing.", this);
                return;
            }

            string entrance = string.IsNullOrWhiteSpace(entranceId) ? room.DefaultEntranceId : entranceId;
            Debug.Log($"[RoomTravelDebug] Travel -> '{room.RoomId}' entrance='{entrance}' layout={room.UsesLayoutSwap}", this);
            SceneTransitionManager.Instance.TransitionToRoom(room.RoomId, entrance);
        }

        private static RoomKind NextKind(RoomKind kind)
        {
            int next = ((int)kind + 1) % 5;
            return (RoomKind)next;
        }

        private static RoomKind PreviousKind(RoomKind kind)
        {
            int prev = ((int)kind - 1 + 5) % 5;
            return (RoomKind)prev;
        }
    }
}
