using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Project.Core.ZoneState;

namespace Project.UI
{
    /// <summary>
    /// Proximity room-choice panel driven by an authored prefab (no runtime UI build).
    /// Assign <see cref="panelRoot"/> + <see cref="cards"/> in the Inspector, or use
    /// Project → UI → Create Room Choice UI Prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIBlockPlayerClick))]
    public sealed class RoomChoiceUI : MonoBehaviour
    {
        private const string ResourcesPrefabPath = "UI/RoomChoiceUI";

        public static RoomChoiceUI Instance { get; private set; }

        [Header("Authored UI")]
        [Tooltip("Root object toggled on Show / Hide. Usually the Canvas or Panel.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Fixed card slots (typically 3). Unused slots are disabled when fewer offers exist.")]
        [SerializeField] private RoomChoiceCardView[] cards;

        private readonly List<RoomChoiceCardView> activeCards = new List<RoomChoiceCardView>(4);
        private bool choiceCommitted;

        public bool IsShowing => panelRoot != null && panelRoot.activeSelf;
        public bool HasCommittedChoice => choiceCommitted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (GetComponent<UIBlockPlayerClick>() == null)
            {
                gameObject.AddComponent<UIBlockPlayerClick>();
            }

            AutoWireIfNeeded();
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static RoomChoiceUI EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            RoomChoiceUI existing = FindFirstObjectByType<RoomChoiceUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            // Must live under Resources/. Prefab/UI alone will not load at runtime.
            GameObject prefab = Resources.Load<GameObject>(ResourcesPrefabPath);
            if (prefab == null)
            {
                GameObject[] listed = Resources.LoadAll<GameObject>("UI");
                for (int i = 0; i < listed.Length; i++)
                {
                    if (listed[i] != null
                        && listed[i].name.IndexOf("RoomChoice", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        prefab = listed[i];
                        break;
                    }
                }
            }

            if (prefab != null)
            {
                GameObject instance = Object.Instantiate(prefab);
                instance.name = "RoomChoiceUI";
                RoomChoiceUI ui = instance.GetComponent<RoomChoiceUI>()
                    ?? instance.GetComponentInChildren<RoomChoiceUI>(true);
                if (ui != null)
                {
                    return ui;
                }

                Debug.LogError(
                    $"[RoomChoiceUI] Prefab '{prefab.name}' loaded but has no RoomChoiceUI component.");
                Object.Destroy(instance);
            }

            Debug.LogError(
                "[RoomChoiceUI] No authored UI found under Resources.\n"
                + "Expected: Assets/Project/Resources/UI/RoomChoiceUI.prefab\n"
                + "(Having it only in Prefab/UI is not enough.)");
            return null;
        }

        public void ClearCommittedChoice()
        {
            choiceCommitted = false;
        }

        public void HideFromProximityExit()
        {
            if (choiceCommitted)
            {
                return;
            }

            Hide();
        }

        public void Show(IReadOnlyList<RoomChoiceOffer> offers)
        {
            if (choiceCommitted)
            {
                return;
            }

            AutoWireIfNeeded();
            if (panelRoot == null || cards == null || cards.Length == 0)
            {
                Debug.LogError("[RoomChoiceUI] Authored panelRoot/cards are not assigned.", this);
                return;
            }

            if (offers == null || offers.Count == 0)
            {
                Debug.LogWarning("[RoomChoiceUI] No offers to show.", this);
                return;
            }

            EnsureEventSystem();
            ClearCardClicks();
            activeCards.Clear();

            int slot = 0;
            for (int i = 0; i < offers.Count && slot < cards.Length; i++)
            {
                if (!offers[i].IsValid || cards[slot] == null)
                {
                    continue;
                }

                RoomChoiceOffer offer = offers[i];
                RoomChoiceCardView card = cards[slot];
                card.Bind(offer, () => HandleChoiceClicked(offer, card));
                activeCards.Add(card);
                slot++;
            }

            for (int i = slot; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].ClearClick();
                    cards[i].gameObject.SetActive(false);
                }
            }

            if (activeCards.Count == 0)
            {
                Debug.LogWarning("[RoomChoiceUI] Offers were present but none could bind to cards.", this);
                Hide();
                return;
            }

            // Level/Rest sequences disable other canvases; ensure ours is drawable again.
            EnsureCanvasEnabled();
            panelRoot.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            Debug.Log($"[RoomChoiceUI] Showing {activeCards.Count} authored room choices.", this);
        }

        public void Hide()
        {
            ClearCardClicks();
            activeCards.Clear();
            HideImmediate();
        }

        private void HideImmediate()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void EnsureCanvasEnabled()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null && panelRoot != null)
            {
                canvas = panelRoot.GetComponentInParent<Canvas>();
            }

            if (canvas != null && !canvas.enabled)
            {
                canvas.enabled = true;
            }
        }

        private void HandleChoiceClicked(RoomChoiceOffer offer, RoomChoiceCardView source)
        {
            if (choiceCommitted || !offer.IsValid)
            {
                return;
            }

            choiceCommitted = true;

            for (int i = 0; i < activeCards.Count; i++)
            {
                activeCards[i]?.SetInteractable(false);
            }

            source?.SetInteractable(false);

            string roomId = offer.RoomId;
            string entranceId = offer.Room.DefaultEntranceId;
            ActiveRouteOffer.Set(offer);

            Debug.Log($"[RoomChoiceUI] Selected room '{roomId}'.", this);
            Hide();

            if (SceneTransitionManager.Instance == null)
            {
                Debug.LogError("[RoomChoiceUI] SceneTransitionManager.Instance is missing.", this);
                ActiveRouteOffer.Clear();
                choiceCommitted = false;
                ResetExitTriggers();
                return;
            }

            if (!SceneTransitionManager.Instance.TryTransitionToRoom(roomId, entranceId))
            {
                Debug.LogWarning($"[RoomChoiceUI] Transition to '{roomId}' was rejected; re-arming exit triggers.", this);
                ActiveRouteOffer.Clear();
                choiceCommitted = false;
                ResetExitTriggers();
            }
        }

        private void AutoWireIfNeeded()
        {
            if (panelRoot == null)
            {
                Transform panel = transform.Find("Panel");
                if (panel == null)
                {
                    panel = transform.Find("Canvas/Panel");
                }

                if (panel != null)
                {
                    panelRoot = panel.gameObject;
                }
                else if (transform.childCount > 0)
                {
                    panelRoot = gameObject;
                }
            }

            if (cards == null || cards.Length == 0)
            {
                cards = GetComponentsInChildren<RoomChoiceCardView>(true);
            }
        }

        private void ClearCardClicks()
        {
            if (cards == null)
            {
                return;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                cards[i]?.ClearClick();
            }
        }

        private static void ResetExitTriggers()
        {
            SceneTransitionManager.ResetAllExitTriggers();
            Instance?.ClearCommittedChoice();
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }
}
