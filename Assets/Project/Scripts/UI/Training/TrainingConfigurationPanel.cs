using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.TeamBuilder.UI.Panels;
using Project.Training;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.UI.Training
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class TrainingConfigurationPanel : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] private TrainingSession session;
        [Header("Panel")]
        [SerializeField] private GameObject content;
        [SerializeField] private CharacterPotentialPanel memoryWindowTemplate;
        [SerializeField] private Button openButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button resetDamageButton;
        [SerializeField] private Button statsButton;
        [SerializeField] private TrainingToolsDrawer toolsDrawer;
        [SerializeField] private TMP_Text errorText;
        [Header("Character Selection")]
        [SerializeField] private Button previousCharacterButton;
        [SerializeField] private Button nextCharacterButton;
        [SerializeField] private TMP_Text characterLabel;
        [Header("Configuration")]
        [SerializeField] private Slider teamLevel;
        [SerializeField] private Slider skill;
        [SerializeField] private Slider ultimate;
        [SerializeField] private Slider passive;
        [SerializeField] private TMP_Text teamLevelValue;
        [SerializeField] private TMP_Text skillValue;
        [SerializeField] private TMP_Text ultimateValue;
        [SerializeField] private TMP_Text passiveValue;

        private TrainingConfiguration _draft;
        private readonly List<CharacterItem> _roster = new();
        private int _index;
        private bool _ownsLock;
        private Action _releasePauseHold;
        private TMP_Text _statsLabel;
        private TeamUI _teamUI;
        private CharacterPotentialPanel _memoryWindow;
        private GameObject _legacyPanel;
        private Button _legacyButton;
        private Button _resetMemoryButton;
        private bool _showLegacy;
        public bool IsOpen { get; private set; }
        public bool IsLoadoutOpen => _teamUI != null && _teamUI.IsTeamBuilderOpen;
        public bool AreToolsVisible => toolsDrawer != null && toolsDrawer.IsPresented;
        public static TrainingConfigurationPanel Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            openButton.onClick.AddListener(Open);
            applyButton.onClick.AddListener(Apply);
            cancelButton.onClick.AddListener(Close);
            resetDamageButton.onClick.AddListener(ResetDamage);
            if (statsButton != null)
            {
                statsButton.onClick.AddListener(ToggleStats);
                _statsLabel = statsButton.GetComponentInChildren<TMP_Text>(true);
            }
            previousCharacterButton.onClick.AddListener(() => SelectCharacter(-1));
            nextCharacterButton.onClick.AddListener(() => SelectCharacter(1));
            teamLevel.onValueChanged.AddListener(_ => ReadGlobalLevel());
            skill.onValueChanged.AddListener(_ => RefreshRankLabels());
            ultimate.onValueChanged.AddListener(_ => RefreshRankLabels());
            passive.onValueChanged.AddListener(_ => RefreshRankLabels());
            content.SetActive(false);
        }

        private void Start()
        {
            _teamUI = FindFirstObjectByType<TeamUI>();
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(toolsDrawer != null ? toolsDrawer.ConfigButton.gameObject : openButton.gameObject);
        }

        private void Update()
        {
            if (IsOpen && ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)))
            {
                if (_memoryWindow != null && _memoryWindow.DismissPracticePreview()) return;
                if (_showLegacy && _memoryWindow != null) ToggleLegacy();
                else Close();
            }
        }

        private void EnsureMemoryWindow()
        {
            if (_memoryWindow != null) return;
            var template = memoryWindowTemplate != null ? memoryWindowTemplate
                : FindFirstObjectByType<CharacterPotentialPanel>(FindObjectsInactive.Include);
            if (template == null) return;

            _legacyPanel = skill.transform.parent.gameObject;
            _memoryWindow = Instantiate(template, content.transform);
            _memoryWindow.gameObject.SetActive(false);
            _memoryWindow.name = "Training Memory Window";
            _memoryWindow.transform.SetAsFirstSibling();
            Place(_memoryWindow.transform, content.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var footer = new GameObject("Training Configuration Controls", typeof(RectTransform), typeof(Image));
            footer.GetComponent<Image>().color = new Color(.055f, .065f, .09f, .98f);
            Place(footer.transform, content.transform, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 100));
            _legacyButton = _memoryWindow.CreateMemoryControl(footer.transform, "Previous upgrades");
            _legacyButton.onClick.AddListener(ToggleLegacy);
            _resetMemoryButton = _memoryWindow.CreateMemoryControl(footer.transform, "Reset All Memory");
            _resetMemoryButton.onClick.AddListener(ResetMemory);
            foreach (Button button in new[] { previousCharacterButton, nextCharacterButton, applyButton, cancelButton })
                _memoryWindow.StyleMemoryControl(button);
            PlaceFooter(teamLevelValue.transform, footer.transform, .02f, .18f);
            teamLevelValue.enableAutoSizing = true;
            teamLevelValue.fontSizeMin = 14f;
            teamLevelValue.fontSizeMax = 24f;
            PlaceFooter(teamLevel.transform, footer.transform, .19f, .40f);
            PlaceFooter(_legacyButton.transform, footer.transform, .42f, .55f);
            PlaceFooter(_resetMemoryButton.transform, footer.transform, .56f, .70f);
            PlaceFooter(applyButton.transform, footer.transform, .71f, .83f);
            PlaceFooter(cancelButton.transform, footer.transform, .84f, .98f);
            Place(errorText.transform, footer.transform, new Vector2(.02f, .56f), new Vector2(.98f, 1f), Vector2.zero, Vector2.zero);
            Place(previousCharacterButton.transform, content.transform, new Vector2(0f, .5f), new Vector2(0f, .5f),
                new Vector2(14f, -36f), new Vector2(86f, 36f));
            Place(nextCharacterButton.transform, content.transform, new Vector2(1f, .5f), new Vector2(1f, .5f),
                new Vector2(-86f, -36f), new Vector2(-14f, 36f));

            _memoryWindow.ConfigurePractice(new Selectable[] { previousCharacterButton, nextCharacterButton },
                new[] { new Selectable[] { teamLevel }, new Selectable[] { _legacyButton, _resetMemoryButton, applyButton, cancelButton } });
        }

        private static void PlaceFooter(Transform control, Transform footer, float left, float right) =>
            Place(control, footer, new Vector2(left, .08f), new Vector2(right, .55f), Vector2.zero, Vector2.zero);

        private static void Place(Transform control, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (control.parent != parent) control.SetParent(parent, false);
            var rect = (RectTransform)control;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void ToggleLegacy()
        {
            CaptureRanks();
            _showLegacy = !_showLegacy;
            ShowCharacter();
            EventSystem.current?.SetSelectedGameObject(_legacyButton.gameObject);
        }

        private void ResetMemory()
        {
            if (!IsOpen || _draft == null || _roster.Count == 0 || _showLegacy) return;
            CaptureRanks();
            var character = _draft.characters[_index];
            character.memory = new CharacterMemoryProgress(character.characterId);
            ShowCharacter();
            errorText.text = $"{characterLabel.text}'s Memory cleared in this draft.";
            EventSystem.current?.SetSelectedGameObject(_resetMemoryButton.gameObject);
        }

        public void Open()
        {
            if (IsOpen || session == null || !session.IsActive) return;
            _draft = session.CaptureConfiguration();
            _roster.Clear();
            for (int index = 0; index < _draft.characters.Count; index++)
            {
                string id = _draft.characters[index].characterId;
                CharacterItem item = session.Inventory.ownedItems.Find(candidate => candidate is CharacterItem owned &&
                    owned.characterObjectData != null && owned.characterObjectData.characterId == id) as CharacterItem;
                if (item != null) _roster.Add(item);
                else _draft.characters.RemoveAt(index--);
            }
            _index = 0;
            _showLegacy = false;
            IsOpen = true;
            InputLockManager.Lock();
            _ownsLock = true;
            _releasePauseHold = GameFreezeManager.AcquirePauseHold();
            EnsureModalCanvasAvailable();
            EnsureMemoryWindow();
            content.SetActive(true);
            errorText.text = "";
            teamLevel.minValue = 1;
            teamLevel.maxValue = session.Inventory.GlobalMaxLevel;
            teamLevel.SetValueWithoutNotify(_draft.teamLevel);
            ShowCharacter();
            ReadGlobalLevel();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(teamLevel.gameObject);
        }

        private void SelectCharacter(int direction)
        {
            if (!IsOpen || _roster.Count < 2) return;
            CaptureRanks();
            var selected = EventSystem.current?.currentSelectedGameObject;
            _index = (_index + direction + _roster.Count) % _roster.Count;
            ShowCharacter();
            if (selected == previousCharacterButton.gameObject || selected == nextCharacterButton.gameObject)
                EventSystem.current?.SetSelectedGameObject(selected);
        }

        private void ShowCharacter()
        {
            previousCharacterButton.interactable = nextCharacterButton.interactable = _roster.Count > 1;
            if (_roster.Count == 0)
            {
                characterLabel.text = "No deployed character";
                skill.interactable = ultimate.interactable = passive.interactable = false;
                if (_memoryWindow != null)
                {
                    _legacyPanel.SetActive(false);
                    _legacyButton.gameObject.SetActive(false);
                    _resetMemoryButton.gameObject.SetActive(false);
                    _memoryWindow.Hide();
                    errorText.text = "Deploy a character to edit Memory.";
                    CharacterMemoryPanel.BindNavigation(new[] { new Selectable[] { teamLevel }, new Selectable[] { applyButton, cancelButton } });
                }
                return;
            }
            CharacterItem item = _roster[_index];
            var character = item.characterObjectData;
            characterLabel.text = CombatCore.TrainingDebugManager.GetCharacterDisplayName(character);
            var values = _draft.characters[_index];
            SetTrack(skill, character, PotentialTrack.Skill, values.skill);
            SetTrack(ultimate, character, PotentialTrack.Ultimate, values.ultimate);
            SetTrack(passive, character, PotentialTrack.Passive, values.passive);
            RefreshRankLabels();
            if (_memoryWindow == null) return;

            bool legacyAvailable = !MainMemoryKit.AppliesTo(character);
            _showLegacy &= legacyAvailable;
            _legacyButton.gameObject.SetActive(legacyAvailable);
            _resetMemoryButton.gameObject.SetActive(!_showLegacy);
            if (legacyAvailable)
                _legacyButton.GetComponentInChildren<TMP_Text>().text = _showLegacy ? "Memory" : "Previous upgrades";
            _legacyPanel.SetActive(_showLegacy);
            if (_showLegacy)
            {
                _memoryWindow.Hide();
                var rows = new List<Selectable[]>();
                if (previousCharacterButton.interactable) rows.Add(new Selectable[] { previousCharacterButton, nextCharacterButton });
                foreach (Slider rank in new[] { skill, ultimate, passive })
                    if (rank.interactable) rows.Add(new Selectable[] { rank });
                rows.Add(new Selectable[] { teamLevel });
                rows.Add(new Selectable[] { _legacyButton, applyButton, cancelButton });
                CharacterMemoryPanel.BindNavigation(rows, _legacyButton);
                return;
            }

            values.memory ??= new CharacterMemoryProgress(character.characterId);
            _memoryWindow.ShowPracticeMemory(item, values.memory);
        }

        private static void SetTrack(Slider slider, CharacterObject character, PotentialTrack track, int value)
        {
            slider.minValue = 0;
            slider.maxValue = MainMemoryKit.AppliesTo(character) ? 0 : TrainingSession.GetMaxPotentialLevel(character, track);
            slider.interactable = slider.maxValue > 0;
            slider.SetValueWithoutNotify(value);
        }

        private void ReadGlobalLevel()
        {
            if (!IsOpen || _draft == null) return;
            _draft.teamLevel = Mathf.RoundToInt(teamLevel.value);
            teamLevelValue.text = $"Global Level {_draft.teamLevel} / {session.Inventory.GlobalMaxLevel}";
        }

        private void CaptureRanks()
        {
            if (_draft == null || _roster.Count == 0 || MainMemoryKit.AppliesTo(_roster[_index].characterObjectData)) return;
            var values = _draft.characters[_index];
            values.skill = Mathf.RoundToInt(skill.value);
            values.ultimate = Mathf.RoundToInt(ultimate.value);
            values.passive = Mathf.RoundToInt(passive.value);
        }

        private void RefreshRankLabels()
        {
            skillValue.text = $"{Mathf.RoundToInt(skill.value)} / {Mathf.RoundToInt(skill.maxValue)}";
            ultimateValue.text = $"{Mathf.RoundToInt(ultimate.value)} / {Mathf.RoundToInt(ultimate.maxValue)}";
            passiveValue.text = $"{Mathf.RoundToInt(passive.value)} / {Mathf.RoundToInt(passive.maxValue)}";
        }

        public void Apply()
        {
            if (!IsOpen || session == null) return;
            CaptureRanks();
            if (!session.TryApply(_draft, out string error)) { errorText.text = error; return; }
            Close();
        }

        private void ResetDamage() { if (session != null) session.ResetDamageSample(); }

        private void EnsureModalCanvasAvailable()
        {
            if (content == null) return;

            // The modal is authored on a separate top-sorting root Canvas. Repair the
            // root here as well so a stale inactive/zero-scale scene root cannot hide it.
            Transform ancestor = content.transform;
            Canvas modalCanvas = null;
            while (ancestor != null && modalCanvas == null)
            {
                modalCanvas = ancestor.GetComponent<Canvas>();
                ancestor = ancestor.parent;
            }

            if (modalCanvas == null) return;
            modalCanvas.sortingOrder = 6100;
            RectTransform canvasRect = modalCanvas.transform as RectTransform;
            if (canvasRect != null) canvasRect.localScale = Vector3.one;
            if (!modalCanvas.gameObject.activeSelf) modalCanvas.gameObject.SetActive(true);
        }

        public void ToggleStats()
        {
            if (session == null) return;
            session.ToggleStats();
            if (_statsLabel != null) _statsLabel.text = CombatCore.TrainingDebugManager.ShowStatsHud ? "Hide Stats" : "Stats";
        }

        public void Close()
        {
            IsOpen = false;
            if (_memoryWindow != null) _memoryWindow.Hide();
            if (content != null) content.SetActive(false);
            _draft = null;
            if (_ownsLock)
            {
                _ownsLock = false;
                _releasePauseHold?.Invoke();
                _releasePauseHold = null;
                InputLockManager.Unlock();
            }
            if (EventSystem.current != null && gameObject.activeInHierarchy && openButton != null)
                EventSystem.current.SetSelectedGameObject(toolsDrawer != null ? toolsDrawer.ConfigButton.gameObject : openButton.gameObject);
        }

        private void OnDisable() => Close();
        private void OnDestroy() { Close(); if (Instance == this) Instance = null; }
    }
}
