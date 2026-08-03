using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Global room catalog. Lookup by roomId (preferred) or sceneName (legacy).
    /// </summary>
    [CreateAssetMenu(fileName = "MapSO", menuName = "Project/Core/MapSO")]
    public class MapSO : ScriptableObject
    {
        [Header("Room Database")]
        [SerializeField] private List<RoomData> rooms = new List<RoomData>();

        public List<RoomData> Rooms => rooms;

        public RoomData ResolveRoom(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || rooms == null)
            {
                return null;
            }

            RoomData byId = GetRoomById(key);
            return byId != null ? byId : GetRoomData(key);
        }

        public RoomData GetRoomById(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId) || rooms == null)
            {
                return null;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                RoomData room = rooms[i];
                if (room != null && string.Equals(room.RoomId, roomId, System.StringComparison.Ordinal))
                {
                    return room;
                }
            }

            return null;
        }

        public RoomData GetRoomData(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || rooms == null)
            {
                return null;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                RoomData room = rooms[i];
                if (room != null && string.Equals(room.SceneName, sceneName, System.StringComparison.Ordinal))
                {
                    return room;
                }
            }

            return null;
        }

        public void CollectOfferCandidates(RoomKind? kind, string excludeRoomId, List<RoomData> results)
        {
            results?.Clear();
            if (rooms == null || results == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                RoomData room = rooms[i];
                if (room == null || !room.IncludeInRandomOffers || room.OfferWeight <= 0f)
                {
                    continue;
                }

                if (kind.HasValue && room.RoomKind != kind.Value)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(excludeRoomId)
                    && string.Equals(room.RoomId, excludeRoomId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                results.Add(room);
            }
        }
    }
}
