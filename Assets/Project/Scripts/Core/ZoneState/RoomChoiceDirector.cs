using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// One step in a biome act. Safe nodes inject at most one Shop/Level/Rest into the choice.
    /// </summary>
    public enum BiomeActNode
    {
        Combat = 0,
        Shop = 1,
        Level = 2,
        Rest = 3,
        Boss = 4
    }

    /// <summary>
    /// Rolls next-room offers from a biome act template (not independent Shop/Level/Rest counters).
    /// Rules: Safe ≤ 1 per choice, Rest/Level one-shot per biome (cleared on biome advance),
    /// Boss when the act reaches the Boss node (template is authoritative).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoiceDirector : MonoBehaviour
    {
        private const string DefaultMapResourcePath = "MapSO/Test_MapSO";
        private const int RequiredChoiceCount = 3;

        public static RoomChoiceDirector Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private MapSO mapData;

        [Header("Offer Rules")]
        [Tooltip("Normal route choices always show this many cards. Boss remains a single offer.")]
        [SerializeField, Min(3)] private int offerCount = 3;

        [Tooltip("Biome route. Combat nodes deepen the run; Shop/Level/Rest inject one safe; Boss forces the boss room.")]
        [SerializeField] private List<BiomeActNode> biomeTemplate = new List<BiomeActNode>
        {
            BiomeActNode.Combat,
            BiomeActNode.Combat,
            BiomeActNode.Shop,
            BiomeActNode.Combat,
            BiomeActNode.Level,
            BiomeActNode.Combat,
            BiomeActNode.Rest,
            BiomeActNode.Boss
        };

        [Header("Runtime (debug)")]
        [SerializeField, Min(0)] private int actIndex;
        [SerializeField] private bool pendingPostBossRest;
        [SerializeField] private bool biomeHadShop;
        [SerializeField] private bool biomeHadLevel;
        [SerializeField] private bool biomeHadRest;
        [SerializeField] private RoomKind lastSafeKindConsumed = (RoomKind)(-1);

        private readonly List<RoomData> candidateBuffer = new List<RoomData>(16);
        private readonly List<RoomChoiceOffer> offerBuffer = new List<RoomChoiceOffer>(4);
        private readonly HashSet<string> usedRoomIds = new HashSet<string>();
        private readonly HashSet<string> visitedRoomIds = new HashSet<string>();

        private bool allowRevisits;
        private bool allowShopInAnyFallback;
        private int pendingActOfferIndex = -1;
        private string preparedExitRoomId;
        private string lastClearedRoomId;

        public int OfferCount => offerCount;
        public int ActIndex => actIndex;
        public IReadOnlyList<BiomeActNode> BiomeTemplate => biomeTemplate;
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

            EnsureOfferCount();
            EnsureTemplate();
            SyncBossDepthToTemplate();
        }

        private void OnValidate()
        {
            EnsureOfferCount();
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
            actIndex = 0;
            pendingPostBossRest = false;
            biomeHadShop = false;
            biomeHadLevel = false;
            biomeHadRest = false;
            lastSafeKindConsumed = (RoomKind)(-1);
            pendingActOfferIndex = -1;
            preparedExitRoomId = null;
            lastClearedRoomId = null;
            offerBuffer.Clear();
            visitedRoomIds.Clear();
            EnsureOfferCount();
            EnsureTemplate();
            SyncBossDepthToTemplate();
            RunProgressionManager.EnsureInstance()?.ResetRun();
        }

        /// <summary>
        /// Call when leaving a room (transition start). Marks one-shot safes visited
        /// without burning them on SafeZone enter / leave-offer prep.
        /// Lobby/meta rooms authored as Shop but with IncludeInRandomOffers=false do not burn act Shop.
        /// </summary>
        public void NotifyLeavingRoom(string roomId)
        {
            RoomData room = ResolveRoom(roomId);
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
            {
                return;
            }

            if (!CountsAsBiomeActSafe(room))
            {
                return;
            }

            if (room.RoomKind == RoomKind.Level || room.RoomKind == RoomKind.Rest)
            {
                visitedRoomIds.Add(room.RoomId);
            }

            lastSafeKindConsumed = room.RoomKind;
            MarkBiomeSafeConsumed(room.RoomKind);
        }

        /// <summary>
        /// Consume the offered template slot only after its destination was reached.
        /// Reopening cards, aborted transitions and duplicate arrival notifications do not advance it.
        /// </summary>
        public void NotifyRoomEntered(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)) return;
            bool wasOffered = false;
            for (int i = 0; i < offerBuffer.Count; i++)
            {
                if (offerBuffer[i].IsValid && offerBuffer[i].RoomId == roomId)
                {
                    wasOffered = true;
                    break;
                }
            }
            if (!wasOffered) return;

            if (pendingActOfferIndex == actIndex) AdvanceAct();
            pendingActOfferIndex = -1;
            preparedExitRoomId = null;
            lastClearedRoomId = null;
            offerBuffer.Clear();
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffersAfterClear(string excludeRoomId = null)
        {
            if (string.IsNullOrWhiteSpace(excludeRoomId))
            {
                excludeRoomId = ResolveCurrentRoomId();
            }

            if (HasPreparedExitOffers(excludeRoomId)) return offerBuffer;

            RoomData clearedRoom = ResolveRoom(excludeRoomId);
            if (clearedRoom != null && !string.IsNullOrWhiteSpace(clearedRoom.RoomId))
            {
                // Combat/Elite uniqueness only — do not treat Lobby/meta clears as act-safe burns.
                if (!IsSafeKind(clearedRoom.RoomKind) || CountsAsBiomeActSafe(clearedRoom))
                {
                    visitedRoomIds.Add(clearedRoom.RoomId);
                }

                excludeRoomId = clearedRoom.RoomId;
                if (CountsAsBiomeActSafe(clearedRoom))
                {
                    lastSafeKindConsumed = clearedRoom.RoomKind;
                    MarkBiomeSafeConsumed(clearedRoom.RoomKind);
                }
            }
            else if (!string.IsNullOrWhiteSpace(excludeRoomId))
            {
                visitedRoomIds.Add(excludeRoomId);
            }

            RunProgressionManager progression = RunProgressionManager.EnsureInstance();
            bool newClear = clearedRoom != null && lastClearedRoomId != clearedRoom.RoomId;
            if (newClear)
            {
                progression?.NotifyRoomCleared(clearedRoom);
                lastClearedRoomId = clearedRoom.RoomId;
            }

            EnsureTemplate();

            if (newClear && clearedRoom.RoomKind == RoomKind.Boss)
            {
                BeginNextBiome();
                pendingPostBossRest = true;
            }

            if (pendingPostBossRest)
            {
                pendingPostBossRest = false;
                IReadOnlyList<RoomChoiceOffer> postBoss = BuildOffersForNode(
                    BiomeActNode.Rest,
                    excludeRoomId,
                    consumeActOnEntry: false);
                if (OfferBufferContainsKind(RoomKind.Rest))
                {
                    return RememberExitOffers(postBoss, excludeRoomId);
                }

                // No Rest room authored — fall through to normal act offers.
            }

            if (TryForceBossOffer(progression))
            {
                return RememberExitOffers(offerBuffer, excludeRoomId);
            }

            BiomeActNode node = ResolveNodeForOffers();
            if (node == BiomeActNode.Boss)
            {
                if (TryForceBossOffer(progression, force: true))
                {
                    return RememberExitOffers(offerBuffer, excludeRoomId);
                }

                Debug.LogError(
                    "[RoomChoiceDirector] Act is on Boss node but boss room could not be resolved. Check Boss_01 in MapSO.",
                    this);
            }

            return RememberExitOffers(
                BuildOffersForNode(node, excludeRoomId, consumeActOnEntry: true), excludeRoomId);
        }

        /// <summary>
        /// Leave-path for Lobby/Shop/Level/Rest. Like combat exits, cards refer to the
        /// next unconsumed slot; entering the chosen room consumes it exactly once.
        /// </summary>
        public IReadOnlyList<RoomChoiceOffer> GenerateLeaveOffersFromSafeRoom(string excludeRoomId = null)
        {
            if (string.IsNullOrWhiteSpace(excludeRoomId))
            {
                excludeRoomId = ResolveCurrentRoomId();
            }

            if (HasPreparedExitOffers(excludeRoomId)) return offerBuffer;

            RoomData currentRoom = ResolveRoom(excludeRoomId);
            if (currentRoom != null && !string.IsNullOrWhiteSpace(currentRoom.RoomId))
            {
                excludeRoomId = currentRoom.RoomId;
            }

            EnsureTemplate();
            RunProgressionManager progression = RunProgressionManager.EnsureInstance();

            if (TryForceBossOffer(progression))
            {
                return RememberExitOffers(offerBuffer, excludeRoomId);
            }

            BiomeActNode node = ResolveNodeForOffers();
            if (node == BiomeActNode.Boss)
            {
                if (TryForceBossOffer(progression, force: true))
                {
                    return RememberExitOffers(offerBuffer, excludeRoomId);
                }
            }

            // Leaving a safe should not inject another safe of the same kind.
            BiomeActNode leaveNode = node;
            if (currentRoom != null && IsSafeKind(currentRoom.RoomKind) && NodeMatchesSafe(leaveNode, currentRoom.RoomKind))
            {
                leaveNode = BiomeActNode.Combat;
            }

            IReadOnlyList<RoomChoiceOffer> offers = BuildOffersForNode(
                leaveNode,
                excludeRoomId,
                consumeActOnEntry: true);
            if (offers != null && offers.Count > 0)
            {
                return RememberExitOffers(offers, excludeRoomId);
            }

            offers = GenerateOffersAllowingRevisits(excludeRoomId, includeShop: false, includeLevel: false, includeRest: false);
            pendingActOfferIndex = offers.Count > 0 ? actIndex : -1;
            return RememberExitOffers(offers, excludeRoomId);
        }

        private bool HasPreparedExitOffers(string roomId)
        {
            return !string.IsNullOrWhiteSpace(roomId)
                && preparedExitRoomId == roomId && offerBuffer.Count > 0;
        }

        private IReadOnlyList<RoomChoiceOffer> RememberExitOffers(IReadOnlyList<RoomChoiceOffer> offers, string roomId)
        {
            preparedExitRoomId = offers.Count > 0 ? roomId : null;
            return offers;
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffers(string excludeRoomId, bool includeShop)
        {
            return GenerateOffers(excludeRoomId, includeShop, includeLevel: false, includeRest: false);
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffers(string excludeRoomId, bool includeShop, bool includeLevel)
        {
            return GenerateOffers(excludeRoomId, includeShop, includeLevel, includeRest: false);
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffers(
            string excludeRoomId,
            bool includeShop,
            bool includeLevel,
            bool includeRest)
        {
            // Clamp to a single safe kind (act rule: Safe ≤ 1).
            if (includeRest)
            {
                includeShop = false;
                includeLevel = false;
            }
            else if (includeLevel)
            {
                includeShop = false;
            }

            return BuildOffersRaw(excludeRoomId, includeShop, includeLevel, includeRest);
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffersAllowingRevisits(string excludeRoomId, bool includeShop)
        {
            return GenerateOffersAllowingRevisits(excludeRoomId, includeShop, includeLevel: false, includeRest: false);
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffersAllowingRevisits(
            string excludeRoomId,
            bool includeShop,
            bool includeLevel)
        {
            return GenerateOffersAllowingRevisits(excludeRoomId, includeShop, includeLevel, includeRest: false);
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffersAllowingRevisits(
            string excludeRoomId,
            bool includeShop,
            bool includeLevel,
            bool includeRest)
        {
            if (EnsureMapData() == null)
            {
                Debug.LogError("[RoomChoiceDirector] MapSO is missing; cannot generate room choices.", this);
                offerBuffer.Clear();
                return offerBuffer;
            }

            HashSet<string> savedVisited = new HashSet<string>(visitedRoomIds);
            visitedRoomIds.Clear();
            foreach (string id in savedVisited)
            {
                RoomData visited = ResolveRoom(id);
                if (visited != null
                    && (visited.RoomKind == RoomKind.Level || visited.RoomKind == RoomKind.Rest))
                {
                    visitedRoomIds.Add(id);
                }
            }

            allowRevisits = true;
            try
            {
                IReadOnlyList<RoomChoiceOffer> offers = GenerateOffers(
                    excludeRoomId,
                    includeShop,
                    includeLevel,
                    includeRest);
                if (offers.Count == 0)
                {
                    Debug.LogWarning(
                        "[RoomChoiceDirector] Revisit-allowed generation still produced 0 offers.",
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

        /// <summary>
        /// Combat nodes before the first Boss node — used to align <see cref="RunProgressionManager.roomsBeforeBoss"/>.
        /// </summary>
        public int CountCombatNodesBeforeBoss()
        {
            EnsureTemplate();
            int count = 0;
            for (int i = 0; i < biomeTemplate.Count; i++)
            {
                if (biomeTemplate[i] == BiomeActNode.Boss)
                {
                    break;
                }

                if (biomeTemplate[i] == BiomeActNode.Combat)
                {
                    count++;
                }
            }

            return Mathf.Max(1, count);
        }

        private IReadOnlyList<RoomChoiceOffer> BuildOffersForNode(
            BiomeActNode node,
            string excludeRoomId,
            bool consumeActOnEntry)
        {
            ResolveNodeFlags(node, out bool includeShop, out bool includeLevel, out bool includeRest);
            IReadOnlyList<RoomChoiceOffer> offers = BuildOffersRaw(
                excludeRoomId,
                includeShop,
                includeLevel,
                includeRest);

            // Do NOT mark biomeHad* when a safe is merely offered — only NotifyLeavingRoom
            // / actually visiting burns the opportunity. Offering Shop then picking Combat
            // used to permanently suppress later safe gates.

            if (includeShop && !OfferBufferContainsKind(RoomKind.Shop))
            {
                Debug.LogWarning(
                    "[RoomChoiceDirector] Act Shop node could not add a Shop room. Check MapSO candidates.",
                    this);
            }

            if (includeLevel && !OfferBufferContainsKind(RoomKind.Level))
            {
                Debug.LogWarning(
                    "[RoomChoiceDirector] Act Level node could not add a Level room. Check MapSO / visited.",
                    this);
            }

            if (includeRest && !OfferBufferContainsKind(RoomKind.Rest))
            {
                Debug.LogWarning(
                    "[RoomChoiceDirector] Act Rest node could not add a Rest room. Check MapSO / visited.",
                    this);
            }

            pendingActOfferIndex = consumeActOnEntry && offers.Count > 0 ? actIndex : -1;

            return offers;
        }

        private void ResolveNodeFlags(
            BiomeActNode node,
            out bool includeShop,
            out bool includeLevel,
            out bool includeRest)
        {
            includeShop = false;
            includeLevel = false;
            includeRest = false;

            switch (node)
            {
                case BiomeActNode.Shop:
                    includeShop = !biomeHadShop && lastSafeKindConsumed != RoomKind.Shop;
                    break;
                case BiomeActNode.Level:
                    includeLevel = !biomeHadLevel && lastSafeKindConsumed != RoomKind.Level;
                    break;
                case BiomeActNode.Rest:
                    includeRest = !biomeHadRest && lastSafeKindConsumed != RoomKind.Rest;
                    break;
                case BiomeActNode.Combat:
                case BiomeActNode.Boss:
                default:
                    break;
            }
        }

        private IReadOnlyList<RoomChoiceOffer> BuildOffersRaw(
            string excludeRoomId,
            bool includeShop,
            bool includeLevel,
            bool includeRest)
        {
            pendingActOfferIndex = -1;
            preparedExitRoomId = null;
            if (EnsureMapData() == null)
            {
                Debug.LogError("[RoomChoiceDirector] MapSO is missing; cannot generate room choices.", this);
                offerBuffer.Clear();
                return offerBuffer;
            }

            offerBuffer.Clear();
            usedRoomIds.Clear();
            allowShopInAnyFallback = includeShop;

            // Safe ≤ 1: add at most one safe kind, Rest > Level > Shop priority if multiple requested.
            if (includeRest)
            {
                TryAddOfferOfKind(RoomKind.Rest, excludeRoomId);
            }
            else if (includeLevel)
            {
                TryAddOfferOfKind(RoomKind.Level, excludeRoomId);
            }
            else if (includeShop)
            {
                TryAddOfferOfKind(RoomKind.Shop, excludeRoomId);
            }

            int guard = 0;
            while (offerBuffer.Count < offerCount && guard++ < 32)
            {
                if (!TryAddOfferOfKind(RoomKind.Combat, excludeRoomId)
                    && !TryAddOfferOfKind(RoomKind.Elite, excludeRoomId)
                    && !TryAddAnyOffer(excludeRoomId))
                {
                    break;
                }
            }

            // Unique layouts ran out (or MapSO only has 1–2 combat rooms). Pad with
            // freshly rolled combat content so the player still sees 3 paths.
            guard = 0;
            while (offerBuffer.Count < offerCount && guard++ < 16)
            {
                if (!TryAddDuplicateCombatOffer(excludeRoomId))
                {
                    break;
                }
            }

            ShuffleOffers();
            Debug.Log(
                $"[RoomChoiceDirector] Generated {offerBuffer.Count} offers "
                + $"(act={actIndex}/{biomeTemplate.Count}, shop={includeShop}, level={includeLevel}, rest={includeRest}, "
                + $"visited={visitedRoomIds.Count}, revisits={allowRevisits}).",
                this);
            return offerBuffer;
        }

        private BiomeActNode ResolveNodeForOffers()
        {
            EnsureTemplate();
            if (biomeTemplate.Count == 0)
            {
                return BiomeActNode.Combat;
            }

            int index = Mathf.Clamp(actIndex, 0, biomeTemplate.Count - 1);
            return biomeTemplate[index];
        }

        private void AdvanceAct()
        {
            EnsureTemplate();
            if (biomeTemplate.Count == 0)
            {
                actIndex = 0;
                return;
            }

            actIndex = Mathf.Min(actIndex + 1, biomeTemplate.Count - 1);
        }

        private void BeginNextBiome()
        {
            actIndex = 0;
            biomeHadShop = false;
            biomeHadLevel = false;
            biomeHadRest = false;
            lastSafeKindConsumed = (RoomKind)(-1);
            ClearOneShotSafeVisitsForNewBiome();
        }

        /// <summary>
        /// Rest/Level are one-shot per biome, not per whole run — otherwise post-boss Rest
        /// and later biomes can never re-offer the only Rest_01 / Level_01 entries.
        /// </summary>
        private void ClearOneShotSafeVisitsForNewBiome()
        {
            if (visitedRoomIds.Count == 0)
            {
                return;
            }

            List<string> remove = new List<string>(visitedRoomIds.Count);
            foreach (string id in visitedRoomIds)
            {
                RoomData room = ResolveRoom(id);
                if (room != null
                    && (room.RoomKind == RoomKind.Level || room.RoomKind == RoomKind.Rest))
                {
                    remove.Add(id);
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                visitedRoomIds.Remove(remove[i]);
            }
        }

        private void MarkBiomeSafeConsumed(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Shop:
                    biomeHadShop = true;
                    break;
                case RoomKind.Level:
                    biomeHadLevel = true;
                    break;
                case RoomKind.Rest:
                    biomeHadRest = true;
                    break;
            }
        }

        private bool TryForceBossOffer(RunProgressionManager progression, bool force = false)
        {
            EnsureTemplate();
            // Template is the authority. Depth must NOT snap to Boss early or Shop/Level nodes are skipped.
            bool bossNode = ResolveNodeForOffers() == BiomeActNode.Boss;
            if (!force && !bossNode)
            {
                return false;
            }

            if (progression == null
                || !progression.TryResolveForcedBossRoom(
                    mapData != null ? mapData : EnsureMapData(),
                    out RoomData bossRoom,
                    requireDue: false)
                || bossRoom == null)
            {
                return false;
            }

            offerBuffer.Clear();
            usedRoomIds.Clear();
            offerBuffer.Add(RouteOfferGenerator.BuildOffer(bossRoom));
            usedRoomIds.Add(bossRoom.RoomId);
            SnapActToBossNode();
            pendingActOfferIndex = actIndex;
            Debug.Log(
                $"[RoomChoiceDirector] Boss offer '{bossRoom.RoomId}' "
                + $"(act={actIndex}, depth={progression.currentRoomDepth}/{progression.RoomsBeforeBoss}).",
                this);
            return true;
        }

        private void SnapActToBossNode()
        {
            EnsureTemplate();
            for (int i = 0; i < biomeTemplate.Count; i++)
            {
                if (biomeTemplate[i] == BiomeActNode.Boss)
                {
                    actIndex = i;
                    return;
                }
            }

            actIndex = Mathf.Max(0, biomeTemplate.Count - 1);
        }

        private void SyncBossDepthToTemplate()
        {
            RunProgressionManager progression = RunProgressionManager.EnsureInstance();
            if (progression == null)
            {
                return;
            }

            // Keep depth as a soft ceiling behind the full act (Shop/Level/Rest nodes do not
            // increment depth). Boss offers are driven by the Boss template node.
            progression.roomsBeforeBoss = Mathf.Max(
                CountCombatNodesBeforeBoss() + 2,
                progression.RoomsBeforeBoss);
        }

        private void EnsureTemplate()
        {
            if (biomeTemplate == null)
            {
                biomeTemplate = new List<BiomeActNode>();
            }

            if (biomeTemplate.Count == 0)
            {
                biomeTemplate.Add(BiomeActNode.Combat);
                biomeTemplate.Add(BiomeActNode.Combat);
                biomeTemplate.Add(BiomeActNode.Shop);
                biomeTemplate.Add(BiomeActNode.Combat);
                biomeTemplate.Add(BiomeActNode.Level);
                biomeTemplate.Add(BiomeActNode.Combat);
                biomeTemplate.Add(BiomeActNode.Rest);
                biomeTemplate.Add(BiomeActNode.Boss);
            }

            bool hasBoss = false;
            for (int i = 0; i < biomeTemplate.Count; i++)
            {
                if (biomeTemplate[i] == BiomeActNode.Boss)
                {
                    hasBoss = true;
                    break;
                }
            }

            if (!hasBoss)
            {
                biomeTemplate.Add(BiomeActNode.Boss);
            }
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
            mapData.CollectOfferCandidates(kind, excludeRoomId, candidateBuffer, ActiveBiomeIndex);
            RemoveAlreadyUsed(candidateBuffer);
            RoomData picked = PickWeighted(candidateBuffer);
            if (picked == null)
            {
                return false;
            }

            offerBuffer.Add(BuildRolledOffer(picked));
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
            mapData.CollectOfferCandidates(null, excludeRoomId, candidateBuffer, ActiveBiomeIndex);
            RemoveAlreadyUsed(candidateBuffer);

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
                    || room.RoomKind == RoomKind.Rest
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

            offerBuffer.Add(BuildRolledOffer(picked));
            usedRoomIds.Add(picked.RoomId);
            return true;
        }

        private bool TryAddDuplicateCombatOffer(string excludeRoomId)
        {
            mapData.CollectOfferCandidates(RoomKind.Combat, excludeRoomId, candidateBuffer, ActiveBiomeIndex);
            if (candidateBuffer.Count == 0)
            {
                mapData.CollectOfferCandidates(RoomKind.Elite, excludeRoomId, candidateBuffer, ActiveBiomeIndex);
            }

            for (int i = candidateBuffer.Count - 1; i >= 0; i--)
            {
                if (candidateBuffer[i] == null)
                {
                    candidateBuffer.RemoveAt(i);
                }
            }

            RoomData picked = PickWeighted(candidateBuffer);
            if (picked == null)
            {
                return false;
            }

            offerBuffer.Add(BuildRolledOffer(picked));
            return true;
        }

        private static int ActiveBiomeIndex => RunProgressionManager.Instance != null
            ? RunProgressionManager.Instance.CurrentBiomeIndex : 0;

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

                if (room.RoomKind == RoomKind.Combat
                    || room.RoomKind == RoomKind.Elite
                    || room.RoomKind == RoomKind.Shop)
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

        private void EnsureOfferCount()
        {
            if (offerCount < RequiredChoiceCount)
            {
                offerCount = RequiredChoiceCount;
            }
        }

        /// <summary>
        /// Combat cards reroll a few times so the three paths are less likely to share
        /// the same difficulty. Layout stays a corridor; the roll is the random part.
        /// </summary>
        private RoomChoiceOffer BuildRolledOffer(RoomData room)
        {
            RoomChoiceOffer offer = RouteOfferGenerator.BuildOffer(room);
            if (!offer.ShowsDifficulty)
            {
                return offer;
            }

            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (!OfferBufferContainsDifficulty(offer.Difficulty))
                {
                    return offer;
                }

                offer = RouteOfferGenerator.BuildOffer(room);
            }

            return offer;
        }

        private bool OfferBufferContainsDifficulty(RouteDifficulty difficulty)
        {
            for (int i = 0; i < offerBuffer.Count; i++)
            {
                RoomChoiceOffer offer = offerBuffer[i];
                if (offer.ShowsDifficulty && offer.Difficulty == difficulty)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSafeKind(RoomKind kind)
        {
            return kind == RoomKind.Shop || kind == RoomKind.Level || kind == RoomKind.Rest || kind == RoomKind.Reward;
        }

        /// <summary>
        /// Act-track safes only. Lobby is authored as Shop but IncludeInRandomOffers=false —
        /// leaving it must not burn the biome Shop gate.
        /// </summary>
        private static bool CountsAsBiomeActSafe(RoomData room)
        {
            if (room == null || !room.IncludeInRandomOffers)
            {
                return false;
            }

            return room.RoomKind == RoomKind.Shop
                || room.RoomKind == RoomKind.Level
                || room.RoomKind == RoomKind.Rest;
        }

        private static bool NodeMatchesSafe(BiomeActNode node, RoomKind kind)
        {
            return (node == BiomeActNode.Shop && kind == RoomKind.Shop)
                || (node == BiomeActNode.Level && kind == RoomKind.Level)
                || (node == BiomeActNode.Rest && kind == RoomKind.Rest);
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
