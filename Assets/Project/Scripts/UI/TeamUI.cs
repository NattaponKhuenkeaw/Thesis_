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
        [SerializeField] private string gameplayActionMapName = "Player";
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private PlayerStateMachine playerStateMachine;

        private CanvasGroup canvasGroup;
        private Canvas rootCanvas;
        private IOrchestratedPanel activePanel;
        private bool inputLockHeld = false;
        private bool previousOverrideSorting = false;
        private int previousSortingOrder = 0;
        private string previousActionMapName;
        private int canvasShellOpenedFrame = -1;

        public bool IsTeamBuilderOpen => activePanel != null;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rootCanvas = GetComponent<Canvas>();
            playerInput = playerInput ?? FindFirstObjectByType<PlayerInput>();
            playerStateMachine = playerStateMachine ?? FindFirstObjectByType<PlayerStateMachine>();
            RepairCanvasRootTransform();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                // Start non-blocking: only block raycasts while a panel is actually open
                // (OpenCanvasShell sets this to true, UnregisterActivePanel clears it)
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnDestroy()
        {
            // Safety net: if this TeamUI is destroyed while holding the input lock / action map,
            // restore the action map so the new scene's PlayerInput starts clean.
            if (inputLockHeld)
            {
                InputLockManager.Unlock();
                inputLockHeld = false;
            }
            RestorePreviousActionMap();
        }

        private void Update()
        {
            if (activePanel == null) return;
            if (Time.frameCount == canvasShellOpenedFrame) return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
                    RequestCloseActivePanel();
            }
        }

        public void RegisterActivePanel(IOrchestratedPanel panel)
        {
            if (activePanel == panel)
            {
                OpenCanvasShell();
                return;
            }

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

                // Restore raycast-blocking to false so the canvas no longer intercepts gameplay input.
                if (canvasGroup != null)
                {
                    canvasGroup.blocksRaycasts = false;
                }

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
            canvasShellOpenedFrame = Time.frameCount;
            RepairCanvasRootTransform();
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

        private void RepairCanvasRootTransform()
        {
            if (transform is RectTransform rectTransform
                && rectTransform.localScale == Vector3.zero)
            {
                rectTransform.localScale = Vector3.one;
            }
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
                : gameplayActionMapName;

            if (!string.IsNullOrWhiteSpace(uiActionMapName)
                && playerInput.actions.FindActionMap(uiActionMapName, throwIfNotFound: false) != null)
            {
                playerInput.SwitchCurrentActionMap(uiActionMapName);
            }
        }

        private void RestorePreviousActionMap()
        {
            if (playerInput == null
                || playerInput.actions == null)
            {
                previousActionMapName = null;
                return;
            }

            string targetActionMapName = !string.IsNullOrWhiteSpace(previousActionMapName)
                ? previousActionMapName
                : gameplayActionMapName;

            if (!string.IsNullOrWhiteSpace(targetActionMapName)
                && playerInput.actions.FindActionMap(targetActionMapName, throwIfNotFound: false) != null)
            {
                playerInput.SwitchCurrentActionMap(targetActionMapName);
            }

            previousActionMapName = null;
        }
    }
}
