using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// ScriptableObject representing the global list of rooms.
    /// Provides central lookup matching scene names to their room data.
    /// </summary>
    [CreateAssetMenu(fileName = "MapSO", menuName = "Project/Core/MapSO")]
    public class MapSO : ScriptableObject
    {
        [Header("Room Database")]
        [SerializeField] private List<RoomData> rooms = new List<RoomData>();

        public List<RoomData> Rooms => rooms;

        /// <summary>
        /// Retrieves the configuration data for a room by its scene name.
        /// </summary>
        public RoomData GetRoomData(string sceneName)
        {
            if (rooms == null) return null;

            foreach (RoomData room in rooms)
            {
                if (room != null && room.SceneName == sceneName)
                {
                    return room;
                }
            }
            return null;
        }
    }
}
