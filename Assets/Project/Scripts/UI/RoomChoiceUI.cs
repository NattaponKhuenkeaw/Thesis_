using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Project.Core.ZoneState;

namespace Project.UI
{
    /// <summary>
    /// Proximity room-choice panel driven by an authored prefab (no runtime UI build).
    /// Assign <see cref="panelRoot"/> + <see cref="cards"/> in the Inspector, or use
    /// Project > UI > Open Room Choice UI Prefab.
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
        [SerializeField] private UIVisibilityTransition visibilityTransition;
        [SerializeField] private RoomRouteProgressUI routeProgress;
        [SerializeField, Min(0f)] private float selectionHoldSeconds = .12f;

        private readonly List<RoomChoiceCardView> activeCards = new List<RoomChoiceCardView>(4);
        private bool choiceCommitted;
        private Coroutine pendingChoice;
        private bool ownsInputLock;

        public bool IsShowing => visibilityTransition != null ? visibilityTransition.IsVisible : panelRoot != null && panelRoot.activeSelf;
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
            ReleaseInputLock();
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
            if (pendingChoice != null) StopCoroutine(pendingChoice);
            pendingChoice = null;
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

            ConfigureCardNavigation();
            RefreshRouteProgress();

            // Level/Rest sequences disable other canvases; ensure ours is drawable again.
            EnsureCanvasEnabled();
            if (visibilityTransition != null) visibilityTransition.SetVisible(true);
            else panelRoot.SetActive(true);
            if (!ownsInputLock)
            {
                InputLockManager.Lock();
                ownsInputLock = true;
            }
            if (EventSystem.current != null && activeCards[0].Button != null)
                EventSystem.current.SetSelectedGameObject(activeCards[0].Button.gameObject);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            Debug.Log($"[RoomChoiceUI] Showing {activeCards.Count} authored room choices.", this);
        }

        private void RefreshRouteProgress()
        {
            if (routeProgress == null) return;
            var director = RoomChoiceDirector.Instance;
            if (director == null || director.LastOffers.Count == 0)
            {
                routeProgress.gameObject.SetActive(false);
                return;
            }
            int level = RunProgressionManager.Instance != null ? RunProgressionManager.Instance.CurrentBiomeIndex + 1 : 1;
            routeProgress.Bind(director.BiomeTemplate, director.OfferedActIndex, level, director.OfferedActIndex < 0);
            routeProgress.SetNextRoom(activeCards[0].BoundOffer);
        }

        private void LateUpdate()
        {
            if (routeProgress == null || !routeProgress.gameObject.activeInHierarchy || !IsShowing || EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            var card = selected != null ? selected.GetComponent<RoomChoiceCardView>() : null;
            if (card != null && activeCards.Contains(card)) routeProgress.SetNextRoom(card.BoundOffer);
        }

        public void Hide()
        {
            if (pendingChoice != null) ClearCommittedChoice();
            ClearCardClicks();
            activeCards.Clear();
            HideImmediate(true);
        }

        private void HideImmediate(bool animated = false)
        {
            ReleaseInputLock();
            if (visibilityTransition != null) visibilityTransition.SetVisible(false, animated);
            else if (panelRoot != null)
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
            source?.SetConfirmed(true);
            pendingChoice = StartCoroutine(CommitChoice(offer));
        }

        private IEnumerator CommitChoice(RoomChoiceOffer offer)
        {
            if (selectionHoldSeconds > 0f) yield return new WaitForSecondsRealtime(selectionHoldSeconds);
            pendingChoice = null;
            if (!choiceCommitted) yield break;

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
                yield break;
            }

            if (!SceneTransitionManager.Instance.TryTransitionToRoom(roomId, entranceId))
            {
                Debug.LogWarning($"[RoomChoiceUI] Transition to '{roomId}' was rejected; re-arming exit triggers.", this);
                ActiveRouteOffer.Clear();
                choiceCommitted = false;
                ResetExitTriggers();
            }
        }

        private void OnDisable()
        {
            ClearCommittedChoice();
            ClearCardClicks();
            activeCards.Clear();
            HideImmediate();
        }

        private void ReleaseInputLock()
        {
            if (!ownsInputLock) return;
            InputLockManager.Unlock();
            ownsInputLock = false;
        }

        private void AutoWireIfNeeded()
        {
            if (routeProgress == null) routeProgress = GetComponentInChildren<RoomRouteProgressUI>(true);
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

        private void ConfigureCardNavigation()
        {
            // Automatic navigation can select unrelated HUD buttons behind this modal.
            for (int i = 0; i < activeCards.Count; i++)
            {
                Button button = activeCards[i].Button;
                if (button == null) continue;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = navigation.selectOnUp = activeCards[Mathf.Max(0, i - 1)].Button;
                navigation.selectOnRight = navigation.selectOnDown = activeCards[Mathf.Min(activeCards.Count - 1, i + 1)].Button;
                button.navigation = navigation;
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
