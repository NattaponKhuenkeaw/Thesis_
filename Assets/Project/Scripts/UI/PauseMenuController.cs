using Project.Core;
using Project.Core.RunEnd;
using Project.Core.ZoneState;
using Project.Progression;
using Project.ShopSystem;
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
        private bool ownsPauseHold;

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
            if (Instance == this)
            {
                Instance = null;
            }

            if (!isLeaving)
            {
                ForceGameplayResume();
            }
        }

        private void Update()
        {
            if (isLeaving || !PausePressedThisFrame())
            {
                return;
            }

            if (!isPaused)
            {
                TryPause();
                return;
            }

            if (confirmPanel != null && confirmPanel.activeSelf)
            {
                ShowConfirm(false);
                return;
            }

            if (settingsPanel != null && settingsPanel.activeSelf)
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
            if (pauseRoot != null)
            {
                pauseRoot.SetActive(true);
            }

            if (mainPanel != null)
            {
                mainPanel.SetActive(true);
            }

            ShowSettings(false);
            ShowConfirm(false);
            BeginPauseHold();
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
            HideAll();
            GameSettings.Save();
            AudioListener.pause = false;
            EndPauseHold();

            if (IsTeamBuilderOpen())
            {
                Time.timeScale = 0f;
            }

            if (ownsInputLock)
            {
                InputLockManager.Unlock();
                ownsInputLock = false;
            }
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

            PlayerMetaProgression meta = Resources.Load<PlayerMetaProgression>(MetaResourcePath);
            PlayerRunInventory runInventory = Resources.Load<PlayerRunInventory>(RunInventoryResourcePath);
            meta?.SavePersistentState();
            runInventory?.ResetRunStateForMenuStart();
            GameSettings.Save();
            TeardownRunSession();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private bool CanPause()
        {
            if (isPaused || isLeaving)
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
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(show);
            }

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
        }

        private void ShowConfirm(bool show)
        {
            if (confirmPanel != null)
            {
                confirmPanel.SetActive(show);
            }
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

        private void HideAll()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }

            if (confirmPanel != null)
            {
                confirmPanel.SetActive(false);
            }

            if (mainPanel != null)
            {
                mainPanel.SetActive(false);
            }

            if (pauseRoot != null)
            {
                pauseRoot.SetActive(false);
            }
        }

        private void BeginPauseHold()
        {
            if (ownsPauseHold)
            {
                return;
            }

            if (GameFreezeManager.Instance != null)
            {
                GameFreezeManager.Instance.BeginPauseHold();
            }
            else
            {
                Time.timeScale = 0f;
            }

            ownsPauseHold = true;
        }

        private void EndPauseHold()
        {
            if (!ownsPauseHold)
            {
                return;
            }

            ownsPauseHold = false;
            if (GameFreezeManager.Instance != null)
            {
                GameFreezeManager.Instance.EndPauseHold();
            }
            else
            {
                Time.timeScale = 1f;
            }
        }

        private void ForceGameplayResume()
        {
            AudioListener.pause = false;
            EndPauseHold();
            if (GameFreezeManager.Instance != null)
            {
                GameFreezeManager.Instance.RestoreTime();
            }
            else
            {
                Time.timeScale = 1f;
            }

            if (ownsInputLock)
            {
                InputLockManager.Unlock();
                ownsInputLock = false;
            }
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

        private static bool IsTeamBuilderOpen()
        {
            TeamUI teamUi = FindFirstObjectByType<TeamUI>();
            return teamUi != null && teamUi.IsTeamBuilderOpen;
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

        private static bool PausePressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.startButton.wasPressedThisFrame;
        }
    }
}
