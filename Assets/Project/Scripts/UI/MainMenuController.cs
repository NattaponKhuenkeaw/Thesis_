using Project.Core;
using Project.Progression;
using Project.Save;
using Project.ShopSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// Title screen: Play opens the save panel, plus Settings / Quit.
    /// UI lives on the scene Canvas so it can be edited in the editor.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        private const string MetaResourcePath = "InventorySO/PlayerMetaProgression";
        private const string RunInventoryResourcePath = "InventorySO/PlayerRunInventory";

        [Header("Flow")]
        [SerializeField] private string lobbySceneName = "Test_Lobby";

        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject confirmPanel;

        [Header("Main Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button trainingButton;
        [SerializeField] private SaveSelectPanel saveSelectPanel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Menu Animation")]
        [SerializeField] private MainMenuPresentation presentation;

        [Header("Settings")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button settingsBackButton;

        [Header("New Game Confirm")]
        [SerializeField] private Button confirmCancelButton;
        [SerializeField] private Button confirmOkButton;

        [Header("Optional Menu BGM")]
        [SerializeField] private AudioClip menuBgm;
        [SerializeField, Range(0f, 1f)] private float menuBgmSourceVolume = 1f;

        private bool isLoading;
        private bool ownsSavePanel;

        private void Awake()
        {
            GameSettings.Apply();
            EnsureEventSystem();
            BindUi();
            if (mainPanel != null)
            {
                mainPanel.SetActive(true);
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }

            if (confirmPanel != null)
            {
                confirmPanel.SetActive(false);
            }

            EnsureSavePanel();
            BindSavePanel();
            saveSelectPanel?.HideImmediate();
            PlayMenuBgm();
        }

        private void Update()
        {
            if (isLoading)
            {
                return;
            }

            if (!KeyboardEscapePressed())
            {
                return;
            }

            if (saveSelectPanel != null && saveSelectPanel.IsOpen)
            {
                saveSelectPanel.Cancel();
                return;
            }

            if (IsPanelVisible(settingsPanel))
            {
                CloseSettings();
            }
        }

        private void OnDestroy()
        {
            UnbindSavePanel();
            if (ownsSavePanel && saveSelectPanel != null)
            {
                if (Application.isPlaying) Destroy(saveSelectPanel.gameObject);
                else DestroyImmediate(saveSelectPanel.gameObject);
            }
        }

        public void OnPlayClicked()
        {
            if (isLoading || saveSelectPanel == null)
            {
                return;
            }

            presentation?.CompleteIntro();
            saveSelectPanel.Open();
        }

        public void OnTrainingClicked()
        {
            if (isLoading) return;
            if (!Application.CanStreamedLevelBeLoaded(Project.Training.TrainingSession.SceneName))
            {
                Debug.LogError("[MainMenu] Training scene is not available.", this);
                return;
            }
            isLoading = true;
            presentation?.CompleteIntro();
            SceneManager.LoadScene(Project.Training.TrainingSession.SceneName);
        }

        public void OnSettingsClicked()
        {
            if (isLoading)
            {
                return;
            }

            presentation?.CompleteIntro();
            ShowSettings(true);
        }

        public void OnQuitClicked()
        {
            if (isLoading)
            {
                return;
            }

            presentation?.CompleteIntro();
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
            StartLobby(clearMeta: true);
        }

        private void BindUi()
        {
            BindButton(playButton, OnPlayClicked);
            BindButton(trainingButton, OnTrainingClicked);
            BindButton(settingsButton, OnSettingsClicked);
            BindButton(quitButton, OnQuitClicked);
            BindButton(settingsBackButton, CloseSettings);
            BindButton(confirmCancelButton, OnConfirmCancelClicked);
            BindButton(confirmOkButton, OnConfirmOkClicked);

            if (masterSlider != null)
            {
                masterSlider.onValueChanged.RemoveAllListeners();
                masterSlider.onValueChanged.AddListener(value =>
                {
                    GameSettings.MasterVolume = value;
                    ApplyBgmVolume();
                });
            }

            if (bgmSlider != null)
            {
                bgmSlider.onValueChanged.RemoveAllListeners();
                bgmSlider.onValueChanged.AddListener(value =>
                {
                    GameSettings.BgmVolume = value;
                    ApplyBgmVolume();
                });
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

        private void StartLobby(bool clearMeta)
        {
            if (isLoading)
            {
                return;
            }

            isLoading = true;
            ShowConfirm(false);

            PlayerMetaProgression meta = Resources.Load<PlayerMetaProgression>(MetaResourcePath);
            PlayerRunInventory runInventory = Resources.Load<PlayerRunInventory>(RunInventoryResourcePath);

            if (clearMeta)
            {
                meta?.ClearAllProgression();
                runInventory?.ClearBossLobbyCheckpoint();
                runInventory?.ResetRunStateForMenuStart();
            }
            else
            {
                meta?.ReloadPersistentState();
                if (runInventory == null || !runInventory.TryRestoreBossLobbyCheckpoint())
                {
                    runInventory?.ResetRunStateForMenuStart();
                }
            }
            runInventory?.PublishActiveSaveHeader();
            GameSettings.Save();
            SceneManager.LoadScene(lobbySceneName);
        }

        private void BeginSaveSlot(int slotIndex, bool createNew)
        {
            if (isLoading)
            {
                return;
            }

            SaveSlotStore.EnsureMigrated();
            SaveSlotStore.SetActive(slotIndex);
            if (createNew)
            {
                SaveSlotStore.Delete(slotIndex);
            }

            StartLobby(clearMeta: createNew);
        }

        private void OnCreateSave(int slotIndex)
        {
            BeginSaveSlot(slotIndex, createNew: true);
        }

        private void OnPlaySave(int slotIndex)
        {
            BeginSaveSlot(slotIndex, createNew: false);
        }

        private void OnDeleteSave(int slotIndex)
        {
            if (isLoading)
            {
                return;
            }

            SaveSlotStore.Delete(slotIndex);
            saveSelectPanel?.Refresh();
        }

        private void BindSavePanel()
        {
            if (saveSelectPanel == null)
            {
                return;
            }

            saveSelectPanel.CreateRequested += OnCreateSave;
            saveSelectPanel.PlayRequested += OnPlaySave;
            saveSelectPanel.DeleteRequested += OnDeleteSave;
        }

        private void UnbindSavePanel()
        {
            if (saveSelectPanel == null)
            {
                return;
            }

            saveSelectPanel.CreateRequested -= OnCreateSave;
            saveSelectPanel.PlayRequested -= OnPlaySave;
            saveSelectPanel.DeleteRequested -= OnDeleteSave;
        }

        private void EnsureSavePanel()
        {
            if (saveSelectPanel != null && saveSelectPanel.gameObject.scene.IsValid())
            {
                return;
            }

            SaveSelectPanel prefab = saveSelectPanel != null
                ? saveSelectPanel
                : Resources.Load<SaveSelectPanel>("UI/SaveSelectPanel");
            if (prefab == null)
            {
                Debug.LogError("[MainMenu] Missing authored SaveSelectPanel prefab.", this);
                return;
            }

            saveSelectPanel = Instantiate(prefab);
            ownsSavePanel = true;
        }

        private void ShowSettings(bool show)
        {
            if (show && saveSelectPanel != null)
            {
                saveSelectPanel.Hide();
            }

            SetPanelVisible(settingsPanel, show);

            if (mainPanel != null)
            {
                if (mainPanel.TryGetComponent<CanvasGroup>(out var group))
                {
                    mainPanel.SetActive(true);
                    group.interactable = !show;
                    group.blocksRaycasts = !show;
                }
                else mainPanel.SetActive(!show);
            }

            if (show)
            {
                SyncSettingsControls();
            }
            else
            {
                GameSettings.Save();
            }
            if (EventSystem.current != null)
            {
                var focus = show ? settingsBackButton : playButton;
                if (focus != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
            }
        }

        private void ShowConfirm(bool show)
        {
            SetPanelVisible(confirmPanel, show);
        }

        private static bool IsPanelVisible(GameObject panel)
        {
            if (panel == null) return false;
            var transition = panel.GetComponent<UIVisibilityTransition>();
            return transition != null ? transition.IsVisible : panel.activeSelf;
        }

        private static void SetPanelVisible(GameObject panel, bool show)
        {
            if (panel == null) return;
            var transition = panel.GetComponent<UIVisibilityTransition>();
            if (transition != null) transition.SetVisible(show);
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

        private void PlayMenuBgm()
        {
            if (menuBgm == null)
            {
                return;
            }

            GameAudioManager.EnsureInstance().PlayMusic(menuBgm, menuBgmSourceVolume);
        }

        private void ApplyBgmVolume()
        {
            GameAudioManager.Instance?.RefreshMusicVolume();
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

        private static bool KeyboardEscapePressed()
        {
            return UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
        }
    }
}
