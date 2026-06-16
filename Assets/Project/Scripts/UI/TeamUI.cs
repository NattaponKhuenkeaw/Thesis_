using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;
using Project.TeamBuilder.UI;

namespace Project.UI
{
    public class TeamUI : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float animationDuration = 0.4f;
        [SerializeField] private int openSortingOrder = 5000;

        [Header("Team Builder Integration")]
        [SerializeField] private TeamBuilderUI teamBuilderUI;
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

        public bool IsTeamBuilderOpen => isOpen;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rootCanvas = GetComponent<Canvas>();
            playerInput = playerInput ?? FindFirstObjectByType<PlayerInput>();
            playerStateMachine = playerStateMachine ?? FindFirstObjectByType<PlayerStateMachine>();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (teamBuilderUI != null)
            {
                teamBuilderUI.gameObject.SetActive(false);
            }
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
            if (isOpen) return;

            OpenCanvasShell();
            isOpen = true;

            if (teamBuilderUI != null)
            {
                teamBuilderUI.gameObject.SetActive(true);
            }

            if (teamBuilderUI != null)
            {
                teamBuilderUI.OnPanelOpened();
            }
        }

        public void Close()
        {
            if (!isOpen) return;

            if (teamBuilderUI != null)
                teamBuilderUI.OnPanelClosed();

            isOpen = false;

            if (activeAnimation != null) StopCoroutine(activeAnimation);
            activeAnimation = StartCoroutine(AnimatePanel(false));
        }

        public void OpenCanvasShell()
        {
            gameObject.SetActive(true);
            BringCanvasToFront();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (!inputLockHeld)
            {
                InputLockManager.Lock();
                inputLockHeld = true;
                SwitchToUIActionMap();
            }

            playerStateMachine?.InputBuffer?.ClearAll();
        }

        public void ReleaseCanvasShell()
        {
            if (isOpen)
            {
                return;
            }

            RestoreCanvasSorting();
            RestorePreviousActionMap();
            if (inputLockHeld)
            {
                InputLockManager.Unlock();
                inputLockHeld = false;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public void Toggle()
        {
            if (isOpen) RequestClose();
            else Open();
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

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (opening)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
            }
            else
            {
                if (teamBuilderUI != null)
                {
                    teamBuilderUI.gameObject.SetActive(false);
                }

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
