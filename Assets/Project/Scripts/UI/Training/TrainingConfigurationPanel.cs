using System;
using Project.Progression;
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
        private CharacterObject[] _roster = Array.Empty<CharacterObject>();
        private int _index;
        private bool _ownsLock;
        private GameFreezeManager _freeze;
        private float _previousTimeScale;
        private TMP_Text _statsLabel;
        private TeamUI _teamUI;
        public bool IsOpen { get; private set; }
        public bool IsLoadoutOpen => _teamUI != null && _teamUI.IsTeamBuilderOpen;
        public bool AreToolsVisible => toolsDrawer != null && toolsDrawer.IsPresented;
        public static TrainingConfigurationPanel Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            var toolsCanvas = GetComponent<Canvas>();
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
            teamLevel.onValueChanged.AddListener(_ => ReadValues());
            skill.onValueChanged.AddListener(_ => ReadValues());
            ultimate.onValueChanged.AddListener(_ => ReadValues());
            passive.onValueChanged.AddListener(_ => ReadValues());
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
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))) Close();
        }

        public void Open()
        {
            if (IsOpen || session == null || !session.IsActive) return;
            _draft = session.CaptureConfiguration();
            var list = new System.Collections.Generic.List<CharacterObject>();
            foreach (var entry in _draft.characters)
            {
                CharacterObject character = Array.Find(session.Inventory.GetTeamSnapshot(), c => c != null && c.characterId == entry.characterId);
                if (character != null) list.Add(character);
            }
            _roster = list.ToArray();
            _index = 0;
            IsOpen = true;
            InputLockManager.Lock();
            _ownsLock = true;
            _freeze = GameFreezeManager.Instance;
            _previousTimeScale = Time.timeScale;
            if (_freeze != null) _freeze.BeginPauseHold();
            else Time.timeScale = 0f;
            EnsureModalCanvasAvailable();
            content.SetActive(true);
            errorText.text = "";
            teamLevel.minValue = 1;
            teamLevel.maxValue = session.Inventory.GlobalMaxLevel;
            teamLevel.SetValueWithoutNotify(_draft.teamLevel);
            ShowCharacter();
            ReadValues();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(teamLevel.gameObject);
        }

        private void SelectCharacter(int direction)
        {
            if (_roster.Length == 0) return;
            _index = (_index + direction + _roster.Length) % _roster.Length;
            ShowCharacter();
            ReadValues();
        }

        private void ShowCharacter()
        {
            bool any = _roster.Length > 0;
            previousCharacterButton.interactable = nextCharacterButton.interactable = _roster.Length > 1;
            if (!any)
            {
                characterLabel.text = "No deployed character";
                skill.interactable = ultimate.interactable = passive.interactable = false;
                return;
            }
            var character = _roster[_index];
            characterLabel.text = CombatCore.TrainingDebugManager.GetCharacterDisplayName(character);
            var values = _draft.characters[_index];
            SetTrack(skill, character, PotentialTrack.Skill, values.skill);
            SetTrack(ultimate, character, PotentialTrack.Ultimate, values.ultimate);
            SetTrack(passive, character, PotentialTrack.Passive, values.passive);
        }

        private static void SetTrack(Slider slider, CharacterObject character, PotentialTrack track, int value)
        {
            slider.minValue = 0;
            slider.maxValue = TrainingSession.GetMaxPotentialLevel(character, track);
            slider.interactable = slider.maxValue > 0;
            slider.SetValueWithoutNotify(value);
        }

        private void ReadValues()
        {
            if (!IsOpen || _draft == null) return;
            _draft.teamLevel = Mathf.RoundToInt(teamLevel.value);
            teamLevelValue.text = $"{_draft.teamLevel} / {session.Inventory.GlobalMaxLevel}";
            if (_roster.Length > 0)
            {
                var values = _draft.characters[_index];
                values.skill = Mathf.RoundToInt(skill.value);
                values.ultimate = Mathf.RoundToInt(ultimate.value);
                values.passive = Mathf.RoundToInt(passive.value);
            }
            skillValue.text = $"{Mathf.RoundToInt(skill.value)} / {Mathf.RoundToInt(skill.maxValue)}";
            ultimateValue.text = $"{Mathf.RoundToInt(ultimate.value)} / {Mathf.RoundToInt(ultimate.maxValue)}";
            passiveValue.text = $"{Mathf.RoundToInt(passive.value)} / {Mathf.RoundToInt(passive.maxValue)}";
        }

        public void Apply()
        {
            if (!IsOpen || session == null) return;
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
            if (content != null) content.SetActive(false);
            IsOpen = false;
            _draft = null;
            if (_ownsLock)
            {
                _ownsLock = false;
                if (_freeze != null) _freeze.EndPauseHold();
                else Time.timeScale = _previousTimeScale;
                InputLockManager.Unlock();
            }
            if (EventSystem.current != null && gameObject.activeInHierarchy && openButton != null)
                EventSystem.current.SetSelectedGameObject(toolsDrawer != null ? toolsDrawer.ConfigButton.gameObject : openButton.gameObject);
        }

        private void OnDisable() => Close();
        private void OnDestroy() { Close(); if (Instance == this) Instance = null; }
    }
}
