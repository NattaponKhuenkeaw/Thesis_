using Project.Core;
using Project.Progression;
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
    /// Title screen: Continue / New Game / Settings / Quit.
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
        [SerializeField] private Button continueButton;
        [SerializeField] private TextMeshProUGUI continueLabel;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

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

        private AudioSource bgmSource;
        private bool isLoading;

        private void Awake()
        {
            GameSettings.Apply();
            EnsureEventSystem();
            EnsureBgmSource();
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

            RefreshContinueState();
            PlayMenuBgm();
        }

        private void Update()
        {
            if (isLoading)
            {
                return;
            }

            if (KeyboardEscapePressed() && settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
            }
        }

        private void OnDestroy()
        {
            StopMenuBgm();
        }

        public void OnContinueClicked()
        {
            if (isLoading || !PlayerMetaProgression.HasPersistentSave())
            {
                return;
            }

            StartLobby(clearMeta: false);
        }

        public void OnNewGameClicked()
        {
            if (isLoading)
            {
                return;
            }

            if (PlayerMetaProgression.HasPersistentSave())
            {
                ShowConfirm(true);
                return;
            }

            StartLobby(clearMeta: true);
        }

        public void OnSettingsClicked()
        {
            if (isLoading)
            {
                return;
            }

            ShowSettings(true);
        }

        public void OnQuitClicked()
        {
            if (isLoading)
            {
                return;
            }

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
            BindButton(continueButton, OnContinueClicked);
            BindButton(newGameButton, OnNewGameClicked);
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
            StopMenuBgm();

            PlayerMetaProgression meta = Resources.Load<PlayerMetaProgression>(MetaResourcePath);
            PlayerRunInventory runInventory = Resources.Load<PlayerRunInventory>(RunInventoryResourcePath);

            if (clearMeta)
            {
                meta?.ClearAllProgression();
            }
            else
            {
                meta?.LoadPersistentStateIfNeeded();
            }

            runInventory?.ResetRunStateForMenuStart();
            GameSettings.Save();
            SceneManager.LoadScene(lobbySceneName);
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

        private void RefreshContinueState()
        {
            bool hasSave = PlayerMetaProgression.HasPersistentSave();
            if (continueButton != null)
            {
                continueButton.interactable = hasSave;
            }

            if (continueLabel != null)
            {
                continueLabel.color = hasSave ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        private void EnsureBgmSource()
        {
            bgmSource = gameObject.GetComponent<AudioSource>();
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
            }

            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
        }

        private void PlayMenuBgm()
        {
            if (bgmSource == null || menuBgm == null)
            {
                return;
            }

            bgmSource.clip = menuBgm;
            ApplyBgmVolume();
            bgmSource.Play();
        }

        private void StopMenuBgm()
        {
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.Stop();
            }
        }

        private void ApplyBgmVolume()
        {
            if (bgmSource != null)
            {
                bgmSource.volume = menuBgmSourceVolume * GameSettings.BgmVolume;
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

        private static bool KeyboardEscapePressed()
        {
            return UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
        }
    }
}
