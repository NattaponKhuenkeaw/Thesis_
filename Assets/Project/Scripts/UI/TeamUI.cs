using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;
using Project.TeamBuilder.UI;

namespace Project.UI
{
    public enum TeamUIOpenMode
    {
        Auto,
        SafeZone,
        CombatZone
    }

    [RequireComponent(typeof(CanvasGroup))]
    public class TeamUI : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float animationDuration = 0.4f;
        [SerializeField] private int openSortingOrder = 5000;

        [Header("Team Builder Integration")]
        [SerializeField] private TeamBuilderUI teamBuilderUI;
        [SerializeField] private TeamUIOpenMode defaultOpenMode = TeamUIOpenMode.Auto;
        [SerializeField] private string uiActionMapName = "UI";
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private PlayerStateMachine playerStateMachine;

        [Header("Input")]
        [SerializeField] private bool listenForKeyboardToggle = true;
        [SerializeField] private Key toggleKey = Key.Tab;

        private CanvasGroup canvasGroup;
        private Canvas rootCanvas;
        private Coroutine activeAnimation;
        private bool isOpen = false;
        private bool inputLockHeld = false;
        private bool previousOverrideSorting = false;
        private int previousSortingOrder = 0;
        private string previousActionMapName;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rootCanvas = GetComponent<Canvas>();
            playerInput = playerInput ?? FindFirstObjectByType<PlayerInput>();
            playerStateMachine = playerStateMachine ?? FindFirstObjectByType<PlayerStateMachine>();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void Update()
        {
            if (listenForKeyboardToggle
                && Keyboard.current != null
                && toggleKey != Key.None
                && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                Toggle();
                return;
            }

            if (!isOpen) return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!EventSystem.current.IsPointerOverGameObject())
                    RequestClose();
            }
        }

        public void Open()
        {
            OpenInMode(ResolveOpenMode());
        }

        public void OpenInMode(TeamBuilderMode mode)
        {
            if (isOpen) return;

            gameObject.SetActive(true);
            isOpen = true;
            BringCanvasToFront();

            if (!inputLockHeld)
            {
                InputLockManager.Lock();
                inputLockHeld = true;
                SwitchToUIActionMap();
            }

            playerStateMachine?.InputBuffer?.ClearAll();

            if (activeAnimation != null) StopCoroutine(activeAnimation);
            activeAnimation = StartCoroutine(AnimatePanel(true));

            if (teamBuilderUI != null)
            {
                teamBuilderUI.SetMode(mode);
                teamBuilderUI.OnPanelOpened();
            }
        }

        public void Close()
        {
            if (!isOpen) return;

            if (teamBuilderUI != null)
                teamBuilderUI.OnPanelClosed();

            isOpen = false;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (activeAnimation != null) StopCoroutine(activeAnimation);
            activeAnimation = StartCoroutine(AnimatePanel(false));
        }

        public void Toggle()
        {
            if (isOpen) RequestClose();
            else Open();
        }

        public void ToggleInMode(TeamBuilderMode mode)
        {
            if (isOpen) RequestClose();
            else OpenInMode(mode);
        }

        public void RequestClose()
        {
            if (!isOpen)
            {
                return;
            }

            if (teamBuilderUI != null && teamBuilderUI.TryRequestSafeZoneClose(Close))
            {
                return;
            }

            Close();
        }

        private IEnumerator AnimatePanel(bool opening)
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            float targetAlpha = opening ? 1f : 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);

                yield return null;
            }

            canvasGroup.alpha = targetAlpha;

            if (opening)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                RestoreCanvasSorting();
                RestorePreviousActionMap();
                if (inputLockHeld)
                {
                    InputLockManager.Unlock();
                    inputLockHeld = false;
                }
            }

            activeAnimation = null;
        }

        private TeamBuilderMode ResolveOpenMode()
        {
            return defaultOpenMode switch
            {
                TeamUIOpenMode.SafeZone => TeamBuilderMode.SafeZone,
                TeamUIOpenMode.CombatZone => TeamBuilderMode.CombatZone,
                _ => ResolveAutoMode()
            };
        }

        private TeamBuilderMode ResolveAutoMode()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(sceneName))
            {
                string normalizedSceneName = sceneName.ToLowerInvariant();
                if (normalizedSceneName.Contains("shop")
                    || normalizedSceneName.Contains("lobby")
                    || normalizedSceneName.Contains("hub"))
                {
                    return TeamBuilderMode.SafeZone;
                }
            }

            return TeamBuilderMode.CombatZone;
        }

        private void BringCanvasToFront()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponent<Canvas>();
            }

            if (rootCanvas == null)
            {
                return;
            }

            previousOverrideSorting = rootCanvas.overrideSorting;
            previousSortingOrder = rootCanvas.sortingOrder;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = openSortingOrder;
        }

        private void RestoreCanvasSorting()
        {
            if (rootCanvas == null)
            {
                return;
            }

            rootCanvas.overrideSorting = previousOverrideSorting;
            rootCanvas.sortingOrder = previousSortingOrder;
        }

        private void SwitchToUIActionMap()
        {
            if (playerInput == null || playerInput.actions == null)
            {
                previousActionMapName = null;
                return;
            }

            previousActionMapName = playerInput.currentActionMap != null
                ? playerInput.currentActionMap.name
                : null;

            if (!string.IsNullOrWhiteSpace(uiActionMapName)
                && playerInput.actions.FindActionMap(uiActionMapName, throwIfNotFound: false) != null)
            {
                playerInput.SwitchCurrentActionMap(uiActionMapName);
            }
        }

        private void RestorePreviousActionMap()
        {
            if (playerInput == null
                || playerInput.actions == null
                || string.IsNullOrWhiteSpace(previousActionMapName))
            {
                previousActionMapName = null;
                return;
            }

            if (playerInput.actions.FindActionMap(previousActionMapName, throwIfNotFound: false) != null)
            {
                playerInput.SwitchCurrentActionMap(previousActionMapName);
            }

            previousActionMapName = null;
        }
    }
}
