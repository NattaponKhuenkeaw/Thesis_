using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Rolls BlazBlue-style next-room offers (default 3).
    /// Shop / Level appear on cadence. Combat fills the rest.
    /// Elite/Reward only fill when those RoomData entries exist — never fallback into Shop/Level.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoiceDirector : MonoBehaviour
    {
        private const string DefaultMapResourcePath = "MapSO/Test_MapSO";

        public static RoomChoiceDirector Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private MapSO mapData;

        [Header("Offer Rules")]
        [SerializeField, Min(1)] private int offerCount = 3;
        [SerializeField, Min(1)] private int shopEveryClears = 3;
        [SerializeField, Min(1)] private int levelEveryClears = 4;
        [SerializeField] private List<RoomKind> defaultFillKinds = new List<RoomKind>
        {
            RoomKind.Combat,
            RoomKind.Combat,
            RoomKind.Combat
        };

        [Header("Runtime")]
        [SerializeField, Min(0)] private int clearsSinceShopOpportunity;
        [SerializeField, Min(0)] private int clearsSinceLevelOpportunity;

        private readonly List<RoomData> candidateBuffer = new List<RoomData>(16);
        private readonly List<RoomChoiceOffer> offerBuffer = new List<RoomChoiceOffer>(4);
        private readonly HashSet<string> usedRoomIds = new HashSet<string>();
        private readonly HashSet<string> visitedRoomIds = new HashSet<string>();

        private bool allowRevisits;
        private bool allowShopInAnyFallback;

        public int OfferCount => offerCount;
        public IReadOnlyList<RoomChoiceOffer> LastOffers => offerBuffer;

        private void Awake()
        {
            Project.Core.RootPersistentManager rootManager = GetComponentInParent<Project.Core.RootPersistentManager>();
            if (rootManager != null && rootManager.IsDuplicate)
            {
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (rootManager == null && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (mapData == null)
            {
                mapData = Resources.Load<MapSO>(DefaultMapResourcePath);
            }

            EnsureCombatFillKinds();
        }

        private void EnsureCombatFillKinds()
        {
            if (defaultFillKinds == null)
            {
                defaultFillKinds = new List<RoomKind>();
            }

            // Legacy assets may still list Elite/Reward/Level here. Those kinds are offered
            // only when real RoomData exists; Level/Shop use cadence instead of fill fallback.
            if (defaultFillKinds.Count == 0)
            {
                defaultFillKinds.Add(RoomKind.Combat);
                defaultFillKinds.Add(RoomKind.Combat);
                defaultFillKinds.Add(RoomKind.Combat);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ResetRouteState()
        {
            clearsSinceShopOpportunity = 0;
            clearsSinceLevelOpportunity = 0;
            offerBuffer.Clear();
            visitedRoomIds.Clear();
            RunProgressionManager.EnsureInstance()?.ResetRun();
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffersAfterClear(string excludeRoomId = null)
        {
            if (string.IsNullOrWhiteSpace(excludeRoomId))
            {
                excludeRoomId = ResolveCurrentRoomId();
            }

            RoomData clearedRoom = ResolveRoom(excludeRoomId);
            if (clearedRoom != null && !string.IsNullOrWhiteSpace(clearedRoom.RoomId))
            {
                visitedRoomIds.Add(clearedRoom.RoomId);
                excludeRoomId = clearedRoom.RoomId;
            }
            else if (!string.IsNullOrWhiteSpace(excludeRoomId))
            {
                visitedRoomIds.Add(excludeRoomId);
            }

            RunProgressionManager progression = RunProgressionManager.EnsureInstance();
            progression?.NotifyRoomCleared(clearedRoom);

            if (TryForceBossOffer(progression))
            {
                return offerBuffer;
            }

            clearsSinceShopOpportunity = Mathf.Max(0, clearsSinceShopOpportunity + 1);
            clearsSinceLevelOpportunity = Mathf.Max(0, clearsSinceLevelOpportunity + 1);

            bool includeShop = clearsSinceShopOpportunity >= shopEveryClears;
            bool includeLevel = clearsSinceLevelOpportunity >= levelEveryClears;

            IReadOnlyList<RoomChoiceOffer> offers = GenerateOffers(excludeRoomId, includeShop, includeLevel);

            // Only consume cadence when the offer was actually placed.
            if (includeShop && OfferBufferContainsKind(RoomKind.Shop))
            {
                clearsSinceShopOpportunity = 0;
            }

            if (includeLevel && OfferBufferContainsKind(RoomKind.Level))
            {
                clearsSinceLevelOpportunity = 0;
            }

            return offers;
        }

        /// <summary>
        /// Leave-path for Shop/Level/other SafeZones. Does not advance shop/level cadence.
        /// Still respects boss due and never softlocks when combat pool is exhausted.
        /// </summary>
        public IReadOnlyList<RoomChoiceOffer> GenerateLeaveOffersFromSafeRoom(string excludeRoomId = null)
        {
            if (string.IsNullOrWhiteSpace(excludeRoomId))
            {
                excludeRoomId = ResolveCurrentRoomId();
            }

            RoomData currentRoom = ResolveRoom(excludeRoomId);
            if (currentRoom != null && !string.IsNullOrWhiteSpace(currentRoom.RoomId))
            {
                // Level (and other one-shot safes) should not reappear forever via revisit fallback.
                if (currentRoom.RoomKind != RoomKind.Shop)
                {
                    visitedRoomIds.Add(currentRoom.RoomId);
                }

                excludeRoomId = currentRoom.RoomId;
            }

            RunProgressionManager progression = RunProgressionManager.EnsureInstance();
            if (TryForceBossOffer(progression))
            {
                return offerBuffer;
            }

            IReadOnlyList<RoomChoiceOffer> offers = GenerateOffers(excludeRoomId, includeShop: false, includeLevel: false);
            if (offers != null && offers.Count > 0)
            {
                return offers;
            }

            return GenerateOffersAllowingRevisits(excludeRoomId, includeShop: false, includeLevel: false);
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffers(string excludeRoomId, bool includeShop)
        {
            return GenerateOffers(excludeRoomId, includeShop, includeLevel: false);
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffers(string excludeRoomId, bool includeShop, bool includeLevel)
        {
            if (EnsureMapData() == null)
            {
                Debug.LogError("[RoomChoiceDirector] MapSO is missing; cannot generate room choices.", this);
                offerBuffer.Clear();
                return offerBuffer;
            }

            offerBuffer.Clear();
            usedRoomIds.Clear();
            allowShopInAnyFallback = includeShop;

            if (includeShop)
            {
                TryAddOfferOfKind(RoomKind.Shop, excludeRoomId);
            }

            if (includeLevel)
            {
                TryAddOfferOfKind(RoomKind.Level, excludeRoomId);
            }

            int guard = 0;
            while (offerBuffer.Count < offerCount && guard++ < 32)
            {
                RoomKind fillKind = PickFillKind(offerBuffer.Count);
                if (IsCadenceOnlyKind(fillKind))
                {
                    // Cadence kinds must not be used as normal fill; fall through to combat.
                    fillKind = RoomKind.Combat;
                }

                if (!TryAddOfferOfKind(fillKind, excludeRoomId))
                {
                    if (!TryAddOfferOfKind(RoomKind.Combat, excludeRoomId)
                        && !TryAddAnyOffer(excludeRoomId))
                    {
                        break;
                    }
                }
            }

            ShuffleOffers();
            Debug.Log(
                $"[RoomChoiceDirector] Generated {offerBuffer.Count} offers (shop={includeShop}, level={includeLevel}, visited={visitedRoomIds.Count}, revisits={allowRevisits}).",
                this);
            return offerBuffer;
        }

        /// <summary>
        /// Leave-path fallback when unique-room filtering emptied the pool (e.g. late shop exit).
        /// Still excludes the current room id and never offers Boss via normal fill.
        /// </summary>
        public IReadOnlyList<RoomChoiceOffer> GenerateOffersAllowingRevisits(string excludeRoomId, bool includeShop)
        {
            return GenerateOffersAllowingRevisits(excludeRoomId, includeShop, includeLevel: false);
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffersAllowingRevisits(
            string excludeRoomId,
            bool includeShop,
            bool includeLevel)
        {
            if (EnsureMapData() == null)
            {
                Debug.LogError("[RoomChoiceDirector] MapSO is missing; cannot generate room choices.", this);
                offerBuffer.Clear();
                return offerBuffer;
            }

            HashSet<string> savedVisited = new HashSet<string>(visitedRoomIds);
            visitedRoomIds.Clear();
            // Keep one-shot safes (Level) blocked even during combat revisit softlock recovery.
            foreach (string id in savedVisited)
            {
                RoomData visited = ResolveRoom(id);
                if (visited != null && visited.RoomKind == RoomKind.Level)
                {
                    visitedRoomIds.Add(id);
                }
            }

            allowRevisits = true;
            try
            {
                IReadOnlyList<RoomChoiceOffer> offers = GenerateOffers(excludeRoomId, includeShop, includeLevel);
                if (offers.Count == 0)
                {
                    Debug.LogWarning(
                        "[RoomChoiceDirector] Revisit-allowed generation still produced 0 offers.",
                        this);
                }
                else
                {
                    Debug.Log(
                        $"[RoomChoiceDirector] Softlock fallback generated {offers.Count} leave offers (combat revisits allowed).",
                        this);
                }

                return offers;
            }
            finally
            {
                allowRevisits = false;
                visitedRoomIds.Clear();
                foreach (string id in savedVisited)
                {
                    visitedRoomIds.Add(id);
                }
            }
        }

        private bool TryForceBossOffer(RunProgressionManager progression)
        {
            if (progression == null
                || !progression.TryResolveForcedBossRoom(mapData != null ? mapData : EnsureMapData(), out RoomData bossRoom)
                || bossRoom == null)
            {
                return false;
            }

            offerBuffer.Clear();
            usedRoomIds.Clear();
            offerBuffer.Add(RouteOfferGenerator.BuildOffer(bossRoom));
            usedRoomIds.Add(bossRoom.RoomId);
            Debug.Log(
                $"[RoomChoiceDirector] Boss due (depth={progression.currentRoomDepth}/{progression.RoomsBeforeBoss}). Forced offer '{bossRoom.RoomId}'.",
                this);
            return true;
        }

        private static bool IsCadenceOnlyKind(RoomKind kind)
        {
            return kind == RoomKind.Shop || kind == RoomKind.Level || kind == RoomKind.Boss;
        }

        private MapSO EnsureMapData()
        {
            if (mapData == null)
            {
                mapData = Resources.Load<MapSO>(DefaultMapResourcePath);
            }

            return mapData;
        }

        private RoomData ResolveRoom(string roomId)
        {
            if (EnsureMapData() == null || string.IsNullOrWhiteSpace(roomId))
            {
                return null;
            }

            return mapData.ResolveRoom(roomId);
        }

        private bool TryAddOfferOfKind(RoomKind kind, string excludeRoomId)
        {
            mapData.CollectOfferCandidates(kind, excludeRoomId, candidateBuffer);
            RemoveAlreadyUsed(candidateBuffer);
            RoomData picked = PickWeighted(candidateBuffer);
            if (picked == null)
            {
                return false;
            }

            offerBuffer.Add(RouteOfferGenerator.BuildOffer(picked));
            usedRoomIds.Add(picked.RoomId);
            return true;
        }

        private bool OfferBufferContainsKind(RoomKind kind)
        {
            for (int i = 0; i < offerBuffer.Count; i++)
            {
                RoomChoiceOffer offer = offerBuffer[i];
                if (offer.IsValid && offer.Room != null && offer.Room.RoomKind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryAddAnyOffer(string excludeRoomId)
        {
            mapData.CollectOfferCandidates(null, excludeRoomId, candidateBuffer);
            RemoveAlreadyUsed(candidateBuffer);

            // Never invent Shop/Level/Boss through "any" fallback — that caused Level↔Shop loops
            // when Elite/Reward kinds were empty.
            for (int i = candidateBuffer.Count - 1; i >= 0; i--)
            {
                RoomData room = candidateBuffer[i];
                if (room == null)
                {
                    candidateBuffer.RemoveAt(i);
                    continue;
                }

                if (room.RoomKind == RoomKind.Boss
                    || room.RoomKind == RoomKind.Level
                    || (room.RoomKind == RoomKind.Shop && !allowShopInAnyFallback))
                {
                    candidateBuffer.RemoveAt(i);
                }
            }

            RoomData picked = PickWeighted(candidateBuffer);
            if (picked == null)
            {
                return false;
            }

            offerBuffer.Add(RouteOfferGenerator.BuildOffer(picked));
            usedRoomIds.Add(picked.RoomId);
            return true;
        }

        private void RemoveAlreadyUsed(List<RoomData> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                RoomData room = list[i];
                if (room == null || usedRoomIds.Contains(room.RoomId))
                {
                    list.RemoveAt(i);
                    continue;
                }

                // Combat/Elite/Reward/Level should not repeat the same room id in a run.
                // Shop stays cadence-repeatable. Revisit fallback may reopen Combat only.
                if (room.RoomKind == RoomKind.Shop)
                {
                    continue;
                }

                if (!allowRevisits && visitedRoomIds.Contains(room.RoomId))
                {
                    list.RemoveAt(i);
                    continue;
                }

                if (allowRevisits
                    && visitedRoomIds.Contains(room.RoomId)
                    && room.RoomKind != RoomKind.Combat
                    && room.RoomKind != RoomKind.Elite)
                {
                    list.RemoveAt(i);
                }
            }
        }

        private RoomKind PickFillKind(int slotIndex)
        {
            if (defaultFillKinds == null || defaultFillKinds.Count == 0)
            {
                return RoomKind.Combat;
            }

            return defaultFillKinds[slotIndex % defaultFillKinds.Count];
        }

        private static RoomData PickWeighted(List<RoomData> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                total += Mathf.Max(0f, candidates[i].OfferWeight);
            }

            if (total <= 0f)
            {
                return candidates[Random.Range(0, candidates.Count)];
            }

            float roll = Random.Range(0f, total);
            float cursor = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cursor += Mathf.Max(0f, candidates[i].OfferWeight);
                if (roll <= cursor)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private void ShuffleOffers()
        {
            for (int i = offerBuffer.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                RoomChoiceOffer tmp = offerBuffer[i];
                offerBuffer[i] = offerBuffer[j];
                offerBuffer[j] = tmp;
            }
        }

        private static string ResolveCurrentRoomId()
        {
            if (RoomLayoutHost.Instance != null && RoomLayoutHost.Instance.ActiveRoom != null)
            {
                return RoomLayoutHost.Instance.ActiveRoom.RoomId;
            }

            ZoneStateProvider provider = Resources.Load<ZoneStateProvider>("CombatSO/ZoneStateProvider");
            if (provider != null && provider.CurrentRoom != null)
            {
                return provider.CurrentRoom.RoomId;
            }

            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }
    }
}
