using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Project.UI
{
    public class TeamUI : MonoBehaviour
    {
        [Header("Canvas Settings")]
        [SerializeField] private int openSortingOrder = 5000;
        [SerializeField] private string uiActionMapName = "UI";
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private PlayerStateMachine playerStateMachine;

        private CanvasGroup canvasGroup;
        private Canvas rootCanvas;
        private IOrchestratedPanel activePanel;
        private bool inputLockHeld = false;
        private bool previousOverrideSorting = false;
        private int previousSortingOrder = 0;
        private string previousActionMapName;

        public bool IsTeamBuilderOpen => activePanel != null;

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
        }

        private void Update()
        {
            if (activePanel == null) return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!EventSystem.current.IsPointerOverGameObject())
                    RequestCloseActivePanel();
            }
        }

        public void RegisterActivePanel(IOrchestratedPanel panel)
        {
            if (activePanel == panel) return;

            if (activePanel != null)
            {
                activePanel.OnPanelClosed();
            }

            activePanel = panel;
            OpenCanvasShell();
        }

        public void UnregisterActivePanel(IOrchestratedPanel panel)
        {
            if (activePanel == panel)
            {
                activePanel = null;

                RestoreCanvasSorting();
                RestorePreviousActionMap();
                if (inputLockHeld)
                {
                    InputLockManager.Unlock();
                    inputLockHeld = false;
                }
            }
        }

        public void RequestCloseActivePanel()
        {
            if (activePanel == null) return;

            if (activePanel.TryRequestClose(CloseActivePanel))
            {
                return;
            }

            CloseActivePanel();
        }

        private void CloseActivePanel()
        {
            if (activePanel != null)
            {
                activePanel.OnPanelClosed();
            }
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
