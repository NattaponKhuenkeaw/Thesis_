using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// ScriptableObject data asset containing parameters for a single room.
    /// Maps a scene name to its associated ZoneMode and default spawn configurations.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomData", menuName = "Project/Core/RoomData")]
    public class RoomData : ScriptableObject
    {
        [Header("Room Setup")]
        [Tooltip("The exact name of the scene in Unity Build Settings.")]
        [SerializeField] private string sceneName;

        [Tooltip("The ZoneMode (SafeZone or CombatZone) associated with this room.")]
        [SerializeField] private ZoneMode zoneMode = ZoneMode.CombatZone;

        [Tooltip("The default entrance point ID for spawning when entering this room.")]
        [SerializeField] private string defaultEntranceId = "Default";

        [Tooltip("If true, the camera will switch to a static camera with no following target in this room.")]
        [SerializeField] private bool useStaticCamera = false;

        public string SceneName => sceneName;
        public ZoneMode ZoneMode => zoneMode;
        public string DefaultEntranceId => defaultEntranceId;
        public bool UseStaticCamera => useStaticCamera;
    }
}
