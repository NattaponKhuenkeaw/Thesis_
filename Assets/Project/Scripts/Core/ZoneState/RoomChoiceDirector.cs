using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Rolls BlazBlue-style next-room offers (default 3).
    /// Shop path appears every N clears; skipping it consumes the opportunity until the next N clears.
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
        [SerializeField] private List<RoomKind> defaultFillKinds = new List<RoomKind>
        {
            RoomKind.Combat,
            RoomKind.Elite,
            RoomKind.Reward
        };

        [Header("Runtime")]
        [SerializeField, Min(0)] private int clearsSinceShopOpportunity;

        private readonly List<RoomData> candidateBuffer = new List<RoomData>(16);
        private readonly List<RoomChoiceOffer> offerBuffer = new List<RoomChoiceOffer>(4);
        private readonly HashSet<string> usedRoomIds = new HashSet<string>();

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
            offerBuffer.Clear();
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffersAfterClear(string excludeRoomId = null)
        {
            clearsSinceShopOpportunity = Mathf.Max(0, clearsSinceShopOpportunity + 1);

            bool includeShop = clearsSinceShopOpportunity >= shopEveryClears;
            if (includeShop)
            {
                clearsSinceShopOpportunity = 0;
            }

            if (string.IsNullOrWhiteSpace(excludeRoomId))
            {
                excludeRoomId = ResolveCurrentRoomId();
            }

            return GenerateOffers(excludeRoomId, includeShop);
        }

        public IReadOnlyList<RoomChoiceOffer> GenerateOffers(string excludeRoomId, bool includeShop)
        {
            if (mapData == null)
            {
                mapData = Resources.Load<MapSO>(DefaultMapResourcePath);
            }

            offerBuffer.Clear();
            usedRoomIds.Clear();

            if (mapData == null)
            {
                Debug.LogError("[RoomChoiceDirector] MapSO is missing; cannot generate room choices.", this);
                return offerBuffer;
            }

            if (includeShop)
            {
                TryAddOfferOfKind(RoomKind.Shop, excludeRoomId);
            }

            int guard = 0;
            while (offerBuffer.Count < offerCount && guard++ < 32)
            {
                RoomKind fillKind = PickFillKind(offerBuffer.Count);
                if (!TryAddOfferOfKind(fillKind, excludeRoomId))
                {
                    if (!TryAddAnyOffer(excludeRoomId))
                    {
                        break;
                    }
                }
            }

            ShuffleOffers();
            Debug.Log($"[RoomChoiceDirector] Generated {offerBuffer.Count} offers (shopIncluded={includeShop}).", this);
            return offerBuffer;
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

            offerBuffer.Add(new RoomChoiceOffer(picked));
            usedRoomIds.Add(picked.RoomId);
            return true;
        }

        private bool TryAddAnyOffer(string excludeRoomId)
        {
            mapData.CollectOfferCandidates(null, excludeRoomId, candidateBuffer);
            RemoveAlreadyUsed(candidateBuffer);
            RoomData picked = PickWeighted(candidateBuffer);
            if (picked == null)
            {
                return false;
            }

            offerBuffer.Add(new RoomChoiceOffer(picked));
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
