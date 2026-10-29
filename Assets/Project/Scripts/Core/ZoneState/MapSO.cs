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

        public void CollectOfferCandidates(RoomKind? kind, string excludeRoomId, List<RoomData> results, int biomeIndex = -1)
        {
            results?.Clear();
            if (rooms == null || results == null)
            {
                return;
            }

            RoomData excludedRoom = ResolveRoom(excludeRoomId);
            int availableBiome = biomeIndex >= 0 ? ResolveAuthoredBiomeIndex(biomeIndex) : -1;

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

                if (biomeIndex >= 0 && room.BiomeIndex >= 0 && room.BiomeIndex != availableBiome)
                    continue;

                if (IsExcludedRoom(room, excludeRoomId, excludedRoom))
                {
                    continue;
                }

                results.Add(room);
            }
        }

        public int ResolveAuthoredBiomeIndex(int progressionBiomeIndex)
        {
            // Lobby artwork and room offers share the same cycle; difficulty keeps its original index.
            var authoredBiomes = new SortedSet<int>();
            if (rooms != null)
                foreach (RoomData room in rooms)
                    if (room != null && room.IncludeInRandomOffers && room.OfferWeight > 0f
                        && room.BiomeIndex >= 0)
                        authoredBiomes.Add(room.BiomeIndex);

            if (authoredBiomes.Count == 0)
                return 0;

            int themeSlot = Mathf.Max(0, progressionBiomeIndex) % authoredBiomes.Count;
            foreach (int authoredBiome in authoredBiomes)
                if (themeSlot-- == 0)
                    return authoredBiome;

            return 0;
        }

        private static bool IsExcludedRoom(RoomData room, string excludeKey, RoomData excludedRoom)
        {
            if (room == null)
            {
                return true;
            }

            if (excludedRoom != null && ReferenceEquals(room, excludedRoom))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(excludeKey))
            {
                return false;
            }

            // Match RoomId only. Do not exclude by SceneName — hybrid combat rooms share combat_1.
            return string.Equals(room.RoomId, excludeKey, System.StringComparison.Ordinal);
        }
    }
}
