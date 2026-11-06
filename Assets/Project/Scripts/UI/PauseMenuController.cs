using Project.Core;
using Project.Core.RunEnd;
using Project.Core.ZoneState;
using Project.Progression;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// In-run pause overlay: Resume / Settings / return to title.
    /// Lives on an authored prefab so the Canvas can be edited in the editor.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenuController : MonoBehaviour
    {
        private const string MetaResourcePath = "InventorySO/PlayerMetaProgression";
        private const string RunInventoryResourcePath = "InventorySO/PlayerRunInventory";

        public static PauseMenuController Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.isPaused;

        [Header("Flow")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Panels")]
        [SerializeField] private GameObject pauseRoot;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject confirmPanel;

        [Header("Main Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button returnToTitleButton;

        [Header("Settings")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button settingsBackButton;

        [Header("Return Confirm")]
        [SerializeField] private Button confirmCancelButton;
        [SerializeField] private Button confirmOkButton;

        private bool isPaused;
        private bool ownsInputLock;
        private bool isLeaving;
        private System.Action releasePauseHold;
        private GameObject returnFocus;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
            BindUi();
            HideAll();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (!isLeaving && releasePauseHold != null) ReleaseGameplayPause();
        }

        private void Update()
        {
            if (isLeaving || !PausePressedThisFrame())
            {
                return;
            }

            if (!isPaused && EscapePressedThisFrame() && TeamBuilderUI.TryCloseFromEscape())
            {
                return;
            }

            if (!isPaused)
            {
                TryPause();
                return;
            }

            if (IsPanelVisible(confirmPanel))
            {
                ShowConfirm(false);
                return;
            }

            if (IsPanelVisible(settingsPanel))
            {
                CloseSettings();
                return;
            }

            Resume();
        }

        public void OnResumeClicked()
        {
            Resume();
        }

        public void OnSettingsClicked()
        {
            if (!isPaused || isLeaving)
            {
                return;
            }

            ShowSettings(true);
        }

        public void OpenPauseMenu()
        {
            if (isLeaving) return;
            if (!isPaused) TryPause();
            if (!isPaused || isLeaving) return;
            ShowSettings(true);
        }

        public void OnReturnToTitleClicked()
        {
            if (!isPaused || isLeaving)
            {
                return;
            }

            ShowConfirm(true);
        }

        public void CloseSettings()
        {
            ShowSettings(false);
        }

        public void OnConfirmCancelClicked()
        {
            ShowConfirm(false);
        }

        public void OnConfirmOkClicked()
        {
            ReturnToTitle();
        }

        private void TryPause()
        {
            if (!CanPause())
            {
                return;
            }

            isPaused = true;
            returnFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            SetPanelVisible(settingsPanel, false, false);
            SetPanelVisible(confirmPanel, false, false);

            if (mainPanel != null)
            {
                mainPanel.SetActive(true);
                SetMainInteractable(true);
            }

            SetPanelVisible(pauseRoot, true);
            Focus(resumeButton);
            releasePauseHold = GameFreezeManager.AcquirePauseHold();
            AudioListener.pause = true;

            if (!ownsInputLock)
            {
                InputLockManager.Lock();
                ownsInputLock = true;
            }
        }

        private void Resume()
        {
            if (!isPaused || isLeaving)
            {
                return;
            }

            isPaused = false;
            HideAll(true);
            GameSettings.Save();
            ReleaseGameplayPause();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(returnFocus != null && returnFocus.activeInHierarchy ? returnFocus : null);
            returnFocus = null;
        }

        public void ReturnFromTraining()
        {
            if (Project.Training.TrainingSession.IsTrainingContext) ReturnToTitle();
        }

        private void ReturnToTitle()
        {
            if (isLeaving)
            {
                return;
            }

            isLeaving = true;
            isPaused = false;
            HideAll();
            ForceGameplayResume();

            if (Project.Training.TrainingSession.IsTrainingContext)
            {
                Project.Training.TrainingSession.Instance?.EndSession();
                GameSettings.Save();
                TeardownRunSession();
                SceneManager.LoadScene(mainMenuSceneName);
                return;
            }

            CharacterProgressionState meta = Project.Training.CharacterStateContext.ResolveProgression();
            CharacterInventoryState runInventory = Project.Training.CharacterStateContext.ResolveInventory();
            if (IsLobbyCheckpointScene())
            {
                meta?.SavePersistentState();
                runInventory?.SaveBossLobbyCheckpoint();
            }
            else
            {
                runInventory?.RestoreBossLobbyMeta();
                meta?.RevertToCommitted();
            }
            GameSettings.Save();
            TeardownRunSession();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private bool IsLobbyCheckpointScene()
        {
            GameRunManager run = GameRunManager.Instance;
            string lobbyName = run != null ? run.LobbySceneName : "Test_Lobby";
            return string.Equals(SceneManager.GetActiveScene().name, lobbyName, System.StringComparison.Ordinal);
        }

        private bool CanPause()
        {
            if (isPaused || isLeaving || (Project.UI.Training.TrainingConfigurationPanel.Instance != null
                && Project.UI.Training.TrainingConfigurationPanel.Instance.IsOpen))
            {
                return false;
            }

            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || string.Equals(active.name, mainMenuSceneName, System.StringComparison.Ordinal))
            {
                return false;
            }

            if (GameRunManager.Instance != null && GameRunManager.Instance.IsBoundaryLoading)
            {
                return false;
            }

            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
            {
                return false;
            }

            if (RunEndController.Instance != null && RunEndController.Instance.IsHandlingDeath)
            {
                return false;
            }

            return true;
        }

        private void BindUi()
        {
            BindButton(resumeButton, OnResumeClicked);
            BindButton(settingsButton, OnSettingsClicked);
            BindButton(returnToTitleButton, OnReturnToTitleClicked);
            BindButton(settingsBackButton, CloseSettings);
            BindButton(confirmCancelButton, OnConfirmCancelClicked);
            BindButton(confirmOkButton, OnConfirmOkClicked);

            if (masterSlider != null)
            {
                masterSlider.onValueChanged.RemoveAllListeners();
                masterSlider.onValueChanged.AddListener(value => GameSettings.MasterVolume = value);
            }

            if (bgmSlider != null)
            {
                bgmSlider.onValueChanged.RemoveAllListeners();
                bgmSlider.onValueChanged.AddListener(value => GameSettings.BgmVolume = value);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.onValueChanged.AddListener(value => GameSettings.SfxVolume = value);
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.RemoveAllListeners();
                fullscreenToggle.onValueChanged.AddListener(value => GameSettings.Fullscreen = value);
            }
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(handler);
        }

        private void ShowSettings(bool show)
        {
            SetPanelVisible(settingsPanel, show);

            if (mainPanel != null)
            {
                mainPanel.SetActive(!show);
            }

            if (show)
            {
                SyncSettingsControls();
            }
            else
            {
                GameSettings.Save();
            }
            Focus(show ? settingsBackButton : resumeButton);
        }

        private void ShowConfirm(bool show)
        {
            SetMainInteractable(!show);
            SetPanelVisible(confirmPanel, show);
            Focus(show ? confirmCancelButton : returnToTitleButton);
        }

        private void SetMainInteractable(bool enabled)
        {
            if (mainPanel != null && mainPanel.TryGetComponent<CanvasGroup>(out var group))
            {
                group.interactable = enabled;
                group.blocksRaycasts = enabled;
            }
        }

        private static void Focus(Button button)
        {
            if (button != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private static bool IsPanelVisible(GameObject panel)
        {
            if (panel == null) return false;
            var transition = panel.GetComponent<UIVisibilityTransition>();
            return transition != null ? transition.IsVisible : panel.activeSelf;
        }

        private static void SetPanelVisible(GameObject panel, bool show, bool animated = true)
        {
            if (panel == null) return;
            var transition = panel.GetComponent<UIVisibilityTransition>();
            if (transition != null) transition.SetVisible(show, animated);
            else panel.SetActive(show);
        }

        private void SyncSettingsControls()
        {
            if (masterSlider != null)
            {
                masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            }

            if (bgmSlider != null)
            {
                bgmSlider.SetValueWithoutNotify(GameSettings.BgmVolume);
            }

            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            }
        }

        private void HideAll(bool animated = false)
        {
            if (!animated)
            {
                SetPanelVisible(settingsPanel, false, false);
                SetPanelVisible(confirmPanel, false, false);
                if (mainPanel != null) mainPanel.SetActive(false);
            }
            SetPanelVisible(pauseRoot, false, animated);
        }

        private void ReleaseGameplayPause()
        {
            AudioListener.pause = false;
            releasePauseHold?.Invoke();
            releasePauseHold = null;
            if (ownsInputLock)
            {
                InputLockManager.Unlock();
                ownsInputLock = false;
            }
        }

        private void ForceGameplayResume()
        {
            ReleaseGameplayPause();
            if (GameFreezeManager.Instance != null) GameFreezeManager.Instance.RestoreTime();
            else if (!GameFreezeManager.IsGameplayPaused) Time.timeScale = 1f;
        }

        private void TeardownRunSession()
        {
            InputLockManager.ResetLocks();
            ownsInputLock = false;

            RootPersistentManager root = RootPersistentManager.Instance;
            if (root != null)
            {
                Destroy(root.gameObject);
            }

            DestroyIfAlive(GameRunManager.Instance);
            DestroyIfAlive(SceneTransitionManager.Instance);
            DestroyIfAlive(GameFreezeManager.Instance);
            DestroyIfAlive(RunProgressionManager.Instance);
            DestroyIfAlive(RoomChoiceDirector.Instance);
            Destroy(gameObject);
        }

        private static void DestroyIfAlive(Component component)
        {
            if (component != null)
            {
                Destroy(component.gameObject);
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static bool EscapePressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        private static bool PausePressedThisFrame()
        {
            if (EscapePressedThisFrame())
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.startButton.wasPressedThisFrame;
        }
    }
}
