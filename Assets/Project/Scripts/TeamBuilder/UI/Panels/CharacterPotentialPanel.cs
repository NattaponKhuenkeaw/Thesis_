using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.TeamBuilder.UI;
using Project.TeamBuilder.UI.Interfaces;
using Project.UI;

namespace Project.TeamBuilder.UI.Panels
{
    public enum CharacterPotentialNodeState
    {
        Locked,
        InsufficientFragments,
        Upgradeable,
        Upgraded
    }

    public class CharacterPotentialPanel : MonoBehaviour, IInspectSubPanel
    {
        private static readonly PotentialTrack[] TrackOrder =
        {
            PotentialTrack.Skill,
            PotentialTrack.Ultimate,
            PotentialTrack.Passive
        };

        [Header("Header")]
        [SerializeField] private Button exitButton;
        [SerializeField] private TMP_Text characterNameText;
        [Tooltip("Optional. Assign your own Fragment label; no label is created automatically.")]
        [SerializeField] private TMP_Text fragmentOwnedText;
        [SerializeField] private TMP_Text elementRoleText;
        [SerializeField] private Image elementIcon;
        [SerializeField] private CharacterElementRoleUI elementRoleUI;

        [Serializable]
        private struct PotentialArtwork
        {
            public CharacterObject character;
            public Sprite skill;
            public Sprite ultimate;
            public Sprite passive;
        }

        [Header("Potential Artwork")]
        [Tooltip("Optional character-specific Potential artwork. Other characters use their kit ability icons.")]
        [SerializeField] private PotentialArtwork[] potentialArtwork;

        [Header("Content")]
        [SerializeField] private ScrollRect contentScrollRect;
        [SerializeField] private Transform nodeRoot;
        [SerializeField] private CharacterPotentialNodeUI[] nodeSlots;
        [Tooltip("Authored generic node roots for pages I, II and III, ordered by step.")]
        [SerializeField] private RectTransform[] genericMemoryPages;
        [SerializeField] private TMP_FontAsset memoryFont;
        [SerializeField] private Sprite memoryControlSprite;
        [SerializeField] private Sprite memoryLockSprite;
        [SerializeField] private bool createRuntimeFallbackNodes = true;

        [Header("Upgrade Window")]
        [SerializeField] private CharacterPotentialUpgradeWindow upgradeWindow;

        [Header("Data Sources")]
        [SerializeField] private CharacterProgressionState metaProgression;
        [SerializeField] private InspectScreenUI inspectScreenUI;

        [Header("Legacy Fallback")]
        [SerializeField] private TMP_Text potentialText;
        [SerializeField] private Button skillUpgradeButton;
        [SerializeField] private Button ultimateUpgradeButton;
        [SerializeField] private Button passiveUpgradeButton;
        [SerializeField] private bool enablePotentialHoverFallback = true;
        [SerializeField] private float potentialHoverScale = 1.1f;
        [SerializeField] private float potentialHoverDuration = 0.1f;
        [SerializeField] private Color potentialHoverGlowColor = new Color(1f, 0.85f, 0.25f, 0.85f);

        private CharacterItem _currentCharacterItem;
        private CharacterObject _currentCharacter;
        private ArtifactInstance[] _currentArtifacts = Array.Empty<ArtifactInstance>();
        private PotentialNodeDefinition _selectedNode;
        private readonly List<PotentialNodeDefinition> _displayNodes = new List<PotentialNodeDefinition>(12);
        private bool _buttonsBound;
        private bool _upgradeWindowOpen;
        private int _ignoreOutsideClickFrame = -1;
        private CharacterMemoryPanel _memoryPanel;
        private CharacterObject _memoryCharacter;
        private bool _showPreviousUpgrades;
        private RectTransform _potentialContent;
        private float _potentialScrollSensitivity;
        private Navigation _potentialExitNavigation;
        private TMP_Text _panelTitle;
        private string _potentialTitle;
        private Button _switchUpgradeView;
        private Button _memoryPreviewSource;
        private CharacterMemoryProgress _practiceMemory;
        private Selectable[] _practiceHeader;
        private Selectable[][] _practiceFooter;
        private static readonly string[] MemoryPageLabels = { "I", "II", "III" };
        private Button[] _memoryPageButtons;
        private Image[] _memoryPageLocks;
        private TMP_Text _memoryLockNotice;
        private Coroutine _memoryLockNoticeMotion;
        private int _lastUnlockedMemoryPage;
        private int _memoryPage = -1;
        private Vector2 _potentialTitleAnchorMax;
        private readonly List<GameObject> _legacyTrackHeaders = new();

        private void Awake()
        {
            ResolveReferences();
            EnsureRuntimeFallbacks();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureRuntimeFallbacks();
            BindButtons();
            if (_practiceFooter == null) BindFragmentEvents();
            CloseUpgradeWindow();
            Refresh();
            PresentMemoryTrack();
        }

        private void OnDisable()
        {
            UnbindButtons();
            UnbindFragmentEvents();
            CloseUpgradeWindow();
            _showPreviousUpgrades = false;
            _memoryCharacter = null;
            _memoryPage = -1;
            RestorePotentialView();
        }

        private void Update()
        {
            HandleUpgradeWindowOutsideClick();
        }

        public void PopulateData(CharacterItem character, ArtifactInstance[] equippedArtifacts)
        {
            if (_currentCharacterItem != character)
            {
                _showPreviousUpgrades = false;
                _memoryPage = -1;
            }
            _currentCharacterItem = character;
            _currentCharacter = character != null ? character.characterObjectData : null;
            _currentArtifacts = equippedArtifacts ?? Array.Empty<ArtifactInstance>();
            ResolveReferences();
            EnsureRuntimeFallbacks();
            BuildDisplayNodes();

            if (_selectedNode == null || !ContainsNode(_selectedNode))
            {
                _selectedNode = null;
            }

            CloseUpgradeWindow();
            Refresh();
            PresentMemoryTrack();
        }

        internal void ConfigurePractice(Selectable[] header, Selectable[][] footer)
        {
            ResolveReferences();
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            if (contentScrollRect != null && contentScrollRect.transform is RectTransform board)
            {
                board.offsetMin += new Vector2(76f, 80f);
                board.offsetMax -= new Vector2(76f, 0f);
            }
            if (upgradeWindow != null && upgradeWindow.transform is RectTransform preview)
            {
                preview.anchorMin = new Vector2(preview.anchorMin.x, .11f);
                preview.anchorMax = new Vector2(preview.anchorMax.x, .88f);
                preview.sizeDelta = new Vector2(preview.sizeDelta.x, 0f);
                preview.anchoredPosition = new Vector2(-100f, 0f);
            }
            _practiceHeader = header;
            _practiceFooter = footer;
        }

        internal void ShowPracticeMemory(CharacterItem character, CharacterMemoryProgress memory)
        {
            if (!ReferenceEquals(_practiceMemory, memory))
            {
                _memoryCharacter = null;
                _memoryPage = -1;
            }
            _practiceMemory = memory;
            PopulateData(character, null);
            Show();
        }

        internal bool DismissPracticePreview()
        {
            if (_practiceFooter == null || _memoryPreviewSource == null) return false;
            Button source = _memoryPreviewSource;
            CloseUpgradeWindow();
            EventSystem.current?.SetSelectedGameObject(source.gameObject);
            return true;
        }

        private void PresentMemoryTrack((int step, PotentialTrack family)? focus = null, Vector2? scrollPosition = null)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _currentCharacter == null || _showPreviousUpgrades ||
                contentScrollRect == null || contentScrollRect.content == null ||
                (_memoryCharacter == _currentCharacter && _memoryPanel != null && _memoryPanel.gameObject.activeSelf))
                return;

            var progression = GetMetaProgression();
            if (progression == null) return;
            if (_memoryPanel == null)
            {
                if (genericMemoryPages == null || genericMemoryPages.Length != CharacterMemoryProgress.PageCount ||
                    Array.Exists(genericMemoryPages, page => page == null ||
                        page.GetComponentsInChildren<CharacterPotentialNodeUI>(true).Length != CharacterMemoryProgress.GenericStepsPerPage))
                    return;
                _potentialContent = contentScrollRect.content;
                _potentialScrollSensitivity = contentScrollRect.scrollSensitivity;
                _panelTitle = transform.Find("PotentialTitle")?.GetComponent<TMP_Text>();
                _potentialTitle = _panelTitle?.text;
                if (_panelTitle != null) _potentialTitleAnchorMax = _panelTitle.rectTransform.anchorMax;
                foreach (string name in new[] { "SKILLTrackTitle", "ULTIMATETrackTitle", "PASSIVETrackTitle" })
                {
                    Transform trackTitle = transform.Find(name);
                    if (trackTitle != null && trackTitle.gameObject.activeSelf) _legacyTrackHeaders.Add(trackTitle.gameObject);
                }
                _memoryPanel = CharacterMemoryPanel.CreateBoard(contentScrollRect, genericMemoryPages);
                CreateMemoryPageButtons();
                CreateMemoryLockNotice();
                if (_practiceFooter == null) CreateUpgradeViewButton();
            }

            CloseUpgradeWindow();
            HideMemoryLockNotice();
            _potentialContent.gameObject.SetActive(false);
            _memoryPanel.gameObject.SetActive(true);
            _memoryCharacter = _currentCharacter;
            var memory = _practiceMemory ?? progression.CaptureMemory(_currentCharacter.characterId);
            _lastUnlockedMemoryPage = memory.LastUnlockedPage;
            _memoryPage = _memoryPage < 0 ? _lastUnlockedMemoryPage : Mathf.Min(_memoryPage, _lastUnlockedMemoryPage);
            foreach (GameObject trackTitle in _legacyTrackHeaders) trackTitle.SetActive(false);
            if (_panelTitle != null)
            {
                _panelTitle.rectTransform.anchorMax = new Vector2(.36f, _potentialTitleAnchorMax.y);
                _panelTitle.text = ResolveCharacterDisplayName();
            }
            bool canShowPrevious = _switchUpgradeView != null && !CharacterMemoryKit.AppliesTo(_currentCharacter);
            if (_switchUpgradeView != null)
                _switchUpgradeView.gameObject.SetActive(canShowPrevious);
            if (canShowPrevious)
                _switchUpgradeView.GetComponentInChildren<TMP_Text>(true).text = "PREVIOUS UPGRADES";

            var header = new List<Selectable>();
            if (exitButton != null && exitButton.isActiveAndEnabled) header.Add(exitButton);
            for (int page = 0; page < _memoryPageButtons.Length; page++)
            {
                Button button = _memoryPageButtons[page];
                button.gameObject.SetActive(true);
                bool pageLocked = page > _lastUnlockedMemoryPage;
                button.image.color = pageLocked ? new Color(.38f, .38f, .38f) : Color.white;
                _memoryPageLocks[page].gameObject.SetActive(pageLocked);
                button.GetComponentInChildren<TMP_Text>().color = page == _memoryPage ? new Color(1f, .86f, .35f) : Color.white;
                header.Add(button);
            }
            if (canShowPrevious) header.Add(_switchUpgradeView);
            if (_practiceHeader != null) header.AddRange(ActiveControls(_practiceHeader));
            var footer = _practiceFooter == null ? null : Array.ConvertAll(_practiceFooter, ActiveControls);
            CharacterPotentialNodeUI major = nodeSlots.Length > 0 ? nodeSlots[0] : null;
            _memoryPanel.ShowConnectedTrack(_currentCharacterItem, memory, header.ToArray(), major, _memoryPage,
                _potentialContent.rect.width, memoryFont, ResolvePotentialIcon, OpenMemoryPreview, footer, focus, scrollPosition);
        }

        internal Button CreateMemoryControl(Transform parent, string caption) =>
            CharacterMemoryPanel.CreateButton(parent, caption, memoryFont, memoryControlSprite);

        internal void StyleMemoryControl(Button button) =>
            CharacterMemoryPanel.StyleButton(button, memoryFont, memoryControlSprite);

        private void CreateMemoryPageButtons()
        {
            _memoryPageButtons = new Button[CharacterMemoryProgress.PageCount];
            _memoryPageLocks = new Image[CharacterMemoryProgress.PageCount];
            for (int page = 0; page < _memoryPageButtons.Length; page++)
            {
                int target = page;
                Button button = CreateMemoryControl(transform, MemoryPageLabels[page]);
                button.name = $"Memory Page {MemoryPageLabels[page]}";
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
                rect.anchoredPosition = new Vector2((page - 1) * 114f, -42f);
                rect.sizeDelta = new Vector2(104f, 42f);
                _memoryPageLocks[page] = CreateMemoryLockIcon(button.transform, new Vector2(1f, .5f), new Vector2(-17f, 0f), 20f);
                button.onClick.AddListener(() =>
                {
                    if (target > _lastUnlockedMemoryPage)
                    {
                        ShowMemoryLockNotice(target);
                        return;
                    }
                    if (_memoryPage == target) return;
                    _memoryPage = target;
                    _memoryCharacter = null;
                    PresentMemoryTrack();
                    EventSystem.current?.SetSelectedGameObject(button.gameObject);
                });
                _memoryPageButtons[page] = button;
            }
        }

        private void CreateMemoryLockNotice()
        {
            _memoryLockNotice = CharacterMemoryPanel.AddText(transform, string.Empty, 28f, memoryFont);
            _memoryLockNotice.name = "Memory Unlock Notice";
            var rect = _memoryLockNotice.rectTransform;
            rect.anchorMin = new Vector2(.15f, 1f);
            rect.anchorMax = new Vector2(.85f, 1f);
            rect.sizeDelta = new Vector2(0f, 48f);
            _memoryLockNotice.gameObject.SetActive(false);
        }

        private void ShowMemoryLockNotice(int target)
        {
            HideMemoryLockNotice();
            _memoryLockNotice.text = $"Complete Page {MemoryPageLabels[target - 1]} to Unlock";
            _memoryLockNotice.gameObject.SetActive(true);
            _memoryLockNoticeMotion = StartCoroutine(AnimateMemoryLockNotice());
        }

        private IEnumerator AnimateMemoryLockNotice()
        {
            const float appear = .18f;
            const float fadeStart = 1f;
            const float end = 1.32f;
            for (float elapsed = 0f; elapsed < end; elapsed += Time.unscaledDeltaTime)
            {
                float slide = Mathf.SmoothStep(0f, 1f, elapsed / appear);
                _memoryLockNotice.rectTransform.anchoredPosition = new Vector2(0f, -126f + 30f * slide);
                _memoryLockNotice.alpha = elapsed < fadeStart ? Mathf.Clamp01(elapsed / appear)
                    : 1f - Mathf.InverseLerp(fadeStart, end, elapsed);
                yield return null;
            }
            _memoryLockNotice.gameObject.SetActive(false);
            _memoryLockNoticeMotion = null;
        }

        private void HideMemoryLockNotice()
        {
            if (_memoryLockNoticeMotion != null) StopCoroutine(_memoryLockNoticeMotion);
            _memoryLockNoticeMotion = null;
            if (_memoryLockNotice != null) _memoryLockNotice.gameObject.SetActive(false);
        }

        private Image CreateMemoryLockIcon(Transform parent, Vector2 anchor, Vector2 position, float size)
        {
            var host = new GameObject("Lock Icon", typeof(RectTransform), typeof(Image));
            host.transform.SetParent(parent, false);
            var icon = host.GetComponent<Image>();
            icon.sprite = memoryLockSprite;
            icon.enabled = memoryLockSprite != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = anchor;
            icon.rectTransform.anchoredPosition = position;
            icon.rectTransform.sizeDelta = Vector2.one * size;
            return icon;
        }

        private static Selectable[] ActiveControls(Selectable[] controls) =>
            Array.FindAll(controls, control => control != null && control.isActiveAndEnabled && control.IsInteractable());

        private void CreateUpgradeViewButton()
        {
            if (exitButton == null) return;
            _potentialExitNavigation = exitButton.navigation;
            _switchUpgradeView = Instantiate(exitButton, transform);
            _switchUpgradeView.name = "Switch Upgrade View";
            _switchUpgradeView.onClick = new Button.ButtonClickedEvent();
            _switchUpgradeView.onClick.AddListener(SwitchUpgradeView);
            var rect = (RectTransform)_switchUpgradeView.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, rect.anchorMin.y);
            rect.pivot = new Vector2(1f, rect.pivot.y);
            rect.anchoredPosition = new Vector2(-Mathf.Abs(rect.anchoredPosition.x), rect.anchoredPosition.y);
            TMP_Text label = _switchUpgradeView.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                Destroy(_switchUpgradeView.gameObject);
                _switchUpgradeView = null;
                return;
            }
            rect.sizeDelta = new Vector2(label.GetPreferredValues("PREVIOUS UPGRADES").x + 32f, rect.sizeDelta.y);
        }

        private void SwitchUpgradeView()
        {
            if (CharacterMemoryKit.AppliesTo(_currentCharacter)) return;
            CloseUpgradeWindow();
            _showPreviousUpgrades = !_showPreviousUpgrades;
            if (!_showPreviousUpgrades)
            {
                PresentMemoryTrack();
                return;
            }

            RestorePotentialView();
            EventSystem.current?.SetSelectedGameObject(_switchUpgradeView.gameObject);
        }

        private void RestorePotentialView()
        {
            if (_memoryPanel == null) return;
            contentScrollRect.StopMovement();
            contentScrollRect.content = _potentialContent;
            contentScrollRect.scrollSensitivity = _potentialScrollSensitivity;
            _memoryPanel.gameObject.SetActive(false);
            _potentialContent.gameObject.SetActive(true);
            if (_panelTitle != null)
            {
                _panelTitle.text = _potentialTitle;
                _panelTitle.rectTransform.anchorMax = _potentialTitleAnchorMax;
            }
            foreach (Button button in _memoryPageButtons) button.gameObject.SetActive(false);
            foreach (GameObject header in _legacyTrackHeaders) header.SetActive(true);
            HideMemoryLockNotice();
            if (exitButton != null) exitButton.navigation = _potentialExitNavigation;
            if (_switchUpgradeView != null)
            {
                _switchUpgradeView.GetComponentInChildren<TMP_Text>(true).text = "MEMORY TRACK";
                _switchUpgradeView.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            }
        }

        private void OpenMemoryPreview(Button source, string title, int step, PotentialTrack family, string description,
            string status, Sprite icon)
        {
            if (upgradeWindow == null) return;
            _memoryPreviewSource = source;
            _ignoreOutsideClickFrame = Time.frameCount;
            contentScrollRect.StopMovement();
            Action select = null;
            if (_practiceMemory != null)
            {
                status += "\nTraining: Completes prerequisites. A full family swaps its latest upgrade.";
                select = () =>
                {
                    if (!_practiceMemory.TrySelectStep(step, family)) return;
                    _memoryCharacter = null;
                    PresentMemoryTrack((step, family), contentScrollRect.normalizedPosition);
                };
            }
            Button dismiss = upgradeWindow.ShowMemoryPreview(family, title, $"STEP {step} / {CharacterMemoryProgress.StepCount}",
                description, status, icon, () =>
                {
                    CloseUpgradeWindow();
                    EventSystem.current?.SetSelectedGameObject(source.gameObject);
                }, select);
            if (dismiss != null && dismiss.isActiveAndEnabled)
                EventSystem.current?.SetSelectedGameObject(dismiss.gameObject);
        }

        public void UpgradeSkillPotential() => QueueLegacyUpgrade(PotentialTrack.Skill);
        public void UpgradeUltimatePotential() => QueueLegacyUpgrade(PotentialTrack.Ultimate);
        public void UpgradePassivePotential() => QueueLegacyUpgrade(PotentialTrack.Passive);

        private bool _legacyUpgradeQueued;
        private PotentialTrack _legacyUpgradeTrack;

        private void QueueLegacyUpgrade(PotentialTrack track)
        {
            _legacyUpgradeQueued = true;
            _legacyUpgradeTrack = track;
        }

        private void LateUpdate()
        {
            if (!_legacyUpgradeQueued)
            {
                return;
            }

            if (Pointer.current != null && Pointer.current.press.isPressed)
            {
                return;
            }

            _legacyUpgradeQueued = false;
            TryUpgradeTrack(_legacyUpgradeTrack);
        }

        public void ExitPotentialPanel()
        {
            if (inspectScreenUI != null)
            {
                inspectScreenUI.ShowInfoPage();
                return;
            }

            Hide();
        }

        public void SelectNode(PotentialNodeDefinition node)
        {
            _selectedNode = node;
            _upgradeWindowOpen = node != null;
            _ignoreOutsideClickFrame = Time.frameCount;
            Refresh();
        }

        public bool TryUpgradeSelectedNode()
        {
            if (_currentCharacter == null || _selectedNode == null || GetMetaProgression() == null)
            {
                return false;
            }

            CharacterPotentialNodeState state = ResolveNodeState(_currentCharacter, metaProgression, _selectedNode);
            if (state != CharacterPotentialNodeState.Upgradeable)
            {
                Refresh();
                return false;
            }

            bool upgraded = metaProgression.TryUpgradePotential(_currentCharacter, _selectedNode.track);
            if (!upgraded)
            {
                Refresh();
                return false;
            }

            CharacterEvents.NotifyStatsChanged(_currentCharacter);
            Refresh();
            PlayUpgradeFeedback();
            return true;
        }

        public static CharacterPotentialNodeState ResolveNodeState(
            CharacterObject character,
            CharacterProgressionState metaProgression,
            PotentialNodeDefinition node)
        {
            if (character == null || metaProgression == null || node == null)
            {
                return CharacterPotentialNodeState.Locked;
            }

            if (metaProgression.IsPotentialNodeUnlocked(character, node))
            {
                return CharacterPotentialNodeState.Upgraded;
            }

            int currentLevel = metaProgression.GetPotentialLevel(character, node.track);
            if (currentLevel < node.level - 1)
            {
                return CharacterPotentialNodeState.Locked;
            }

            PotentialNodeDefinition nextNode = metaProgression.GetNextPotentialNode(character, node.track);
            if (nextNode == null || nextNode.level != node.level)
            {
                return CharacterPotentialNodeState.Locked;
            }

            int cost = Mathf.Max(0, nextNode.fragmentCost);
            return cost == 0 || metaProgression.GetFragmentCount(character.characterId) >= cost
                ? CharacterPotentialNodeState.Upgradeable
                : CharacterPotentialNodeState.InsufficientFragments;
        }

        private void Refresh()
        {
            PopulateOwnedFragments();
            PopulateHeader();
            BuildDisplayNodes();
            RefreshNodes();
            RefreshUpgradeWindow();
            RefreshLegacyFallback();
        }

        private void PopulateHeader()
        {
            if (_currentCharacter == null)
            {
                if (characterNameText != null) characterNameText.text = "No Character";
                if (elementRoleText != null) elementRoleText.text = string.Empty;
                if (elementIcon != null) elementIcon.enabled = false;
                if (elementRoleUI != null && elementRoleUI.transform.IsChildOf(transform))
                {
                    elementRoleUI.SetCharacter(null);
                }
                return;
            }

            if (characterNameText != null)
            {
                characterNameText.text = ResolveCharacterDisplayName();
            }

            PopulateElementRoleFallback();

            if (elementRoleUI != null && elementRoleUI.transform.IsChildOf(transform))
            {
                elementRoleUI.SetCharacter(_currentCharacter);
            }
        }

        private void PopulateElementRoleFallback()
        {
            if (elementIcon != null)
            {
                elementIcon.enabled = false;
            }

            if (elementRoleText == null)
            {
                return;
            }

            elementRoleText.text = _currentCharacter.characterRole == CharacterRole.None
                ? string.Empty
                : _currentCharacter.characterRole.ToString().ToUpperInvariant();
        }

        private void BuildDisplayNodes()
        {
            _displayNodes.Clear();
            if (_currentCharacter == null)
            {
                return;
            }

            for (int trackIndex = 0; trackIndex < TrackOrder.Length; trackIndex++)
            {
                PotentialTrack track = TrackOrder[trackIndex];
                for (int level = 1; level <= CharacterKitDefinition.MaxPotentialLevel; level++)
                {
                    _displayNodes.Add(FindPotentialNode(_currentCharacter, track, level) ?? CreateFallbackNode(track, level));
                }
            }
        }

        private void RefreshNodes()
        {
            EnsureNodeSlots();

            for (int i = 0; i < nodeSlots.Length; i++)
            {
                CharacterPotentialNodeUI slot = nodeSlots[i];
                if (slot == null)
                {
                    continue;
                }

                if (i >= _displayNodes.Count)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                PotentialNodeDefinition node = _displayNodes[i];
                CharacterPotentialNodeState state = ResolveNodeState(_currentCharacter, GetMetaProgression(), node);
                bool selected = ReferenceEquals(node, _selectedNode)
                    || (_selectedNode != null && node.track == _selectedNode.track && node.level == _selectedNode.level);

                slot.gameObject.SetActive(true);
                slot.Bind(node, state, selected, SelectNode, ResolvePotentialIcon(node.track));
            }
        }

        private void RefreshUpgradeWindow()
        {
            if (upgradeWindow == null || _memoryPreviewSource != null)
            {
                return;
            }

            upgradeWindow.BindUpgradeHandler(_ => TryUpgradeSelectedNode());
            upgradeWindow.BindCloseHandler(() =>
            {
                _selectedNode = null;
                CloseUpgradeWindow();
                RefreshNodes();
            });

            if (!_upgradeWindowOpen || _currentCharacter == null || _selectedNode == null)
            {
                upgradeWindow.Hide();
                return;
            }

            upgradeWindow.Show(_currentCharacterItem, _currentCharacter, _selectedNode, GetMetaProgression());
            upgradeWindow.SetIcon(ResolvePotentialIcon(_selectedNode.track));
        }

        private Sprite ResolvePotentialIcon(PotentialTrack track)
        {
            CharacterPotentialArtwork characterArt = _currentCharacter != null
                ? _currentCharacter.potentialArtwork : null;
            Sprite characterIcon = track == PotentialTrack.Skill ? characterArt?.skill
                : track == PotentialTrack.Ultimate ? characterArt?.ultimate : characterArt?.passive;
            if (characterIcon != null) return characterIcon;

            if (potentialArtwork != null)
            {
                for (int i = 0; i < potentialArtwork.Length; i++)
                {
                    PotentialArtwork art = potentialArtwork[i];
                    if (art.character != _currentCharacter) continue;
                    Sprite icon = track == PotentialTrack.Skill ? art.skill : track == PotentialTrack.Ultimate ? art.ultimate : art.passive;
                    if (icon != null) return icon;
                }
            }
            CharacterKitDefinition kit = _currentCharacter != null ? _currentCharacter.kitDefinition : null;
            return track == PotentialTrack.Skill ? kit?.skill?.icon : track == PotentialTrack.Ultimate ? kit?.ultimate?.icon : kit?.passive?.icon;
        }

        private void RefreshLegacyFallback()
        {
            if (potentialText != null)
            {
                potentialText.text = _currentCharacter != null ? BuildPotentialSummary(_currentCharacter) : "No Character";
                ConfigurePotentialHoverFallback();
            }

            RefreshLegacyButton(skillUpgradeButton, PotentialTrack.Skill);
            RefreshLegacyButton(ultimateUpgradeButton, PotentialTrack.Ultimate);
            RefreshLegacyButton(passiveUpgradeButton, PotentialTrack.Passive);
        }

        private bool TryUpgradeTrack(PotentialTrack track)
        {
            PotentialNodeDefinition nextNode = GetMetaProgression()?.GetNextPotentialNode(_currentCharacter, track);
            if (nextNode == null)
            {
                Refresh();
                return false;
            }

            _selectedNode = FindPotentialNode(_currentCharacter, track, nextNode.level) ?? nextNode;
            _upgradeWindowOpen = true;
            _ignoreOutsideClickFrame = Time.frameCount;
            return TryUpgradeSelectedNode();
        }

        private void HandleUpgradeWindowOutsideClick()
        {
            if ((!_upgradeWindowOpen && _memoryPreviewSource == null) || upgradeWindow == null || !upgradeWindow.gameObject.activeInHierarchy)
            {
                return;
            }

            if (Time.frameCount == _ignoreOutsideClickFrame)
            {
                return;
            }

            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
            {
                return;
            }

            Vector2 screenPosition = pointer.position.ReadValue();
            if (IsScreenPointInside(upgradeWindow.transform as RectTransform, screenPosition) ||
                (_memoryPreviewSource != null
                    ? _memoryPanel != null && _memoryPanel.IsScreenPointInsideNode(screenPosition)
                    : IsScreenPointInsideAnyNode(screenPosition)))
            {
                return;
            }

            bool previewingMemory = _memoryPreviewSource != null;
            _selectedNode = null;
            CloseUpgradeWindow();
            if (!previewingMemory) RefreshNodes();
        }

        private bool IsScreenPointInsideAnyNode(Vector2 screenPosition)
        {
            if (nodeSlots == null)
            {
                return false;
            }

            for (int i = 0; i < nodeSlots.Length; i++)
            {
                CharacterPotentialNodeUI slot = nodeSlots[i];
                if (slot != null && slot.gameObject.activeInHierarchy && IsScreenPointInside(slot.transform as RectTransform, screenPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsScreenPointInside(RectTransform rectTransform, Vector2 screenPosition)
        {
            if (rectTransform == null)
            {
                return false;
            }

            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
        }

        private void CloseUpgradeWindow()
        {
            _upgradeWindowOpen = false;
            _memoryPreviewSource = null;
            if (upgradeWindow != null)
            {
                upgradeWindow.Hide();
            }
        }

        private PotentialNodeDefinition ResolveInitialSelection()
        {
            CharacterProgressionState progression = GetMetaProgression();
            if (_currentCharacter == null || progression == null)
            {
                return null;
            }

            for (int i = 0; i < _displayNodes.Count; i++)
            {
                PotentialNodeDefinition node = _displayNodes[i];
                if (ResolveNodeState(_currentCharacter, progression, node) == CharacterPotentialNodeState.Upgradeable)
                {
                    return node;
                }
            }

            return _displayNodes.Count > 0 ? _displayNodes[0] : null;
        }

        private bool ContainsNode(PotentialNodeDefinition target)
        {
            if (target == null)
            {
                return false;
            }

            for (int i = 0; i < _displayNodes.Count; i++)
            {
                PotentialNodeDefinition node = _displayNodes[i];
                if (node != null && node.track == target.track && node.level == target.level)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveReferences()
        {
            metaProgression = Project.Training.CharacterStateContext.ResolveProgression(metaProgression);
            if (inspectScreenUI == null)
            {
                inspectScreenUI = GetComponentInParent<InspectScreenUI>(true);
            }

            if (metaProgression == null)
            {
                metaProgression = Project.Training.CharacterStateContext.ResolveProgression();
            }

            if (exitButton == null)
            {
                exitButton = FindButtonByNames("ExitButton", "BackButton", "Exit", "Back");
            }

            if (characterNameText == null)
            {
                characterNameText = FindTextByNames("CharacterNameText", "HeaderNameText", "NameText", "CharacterName", "Name");
            }

            if (elementRoleText == null)
            {
                elementRoleText = FindTextByNames("ElementRoleText", "AttributeRoleText", "AttributeText", "RoleText");
            }

            if (elementRoleUI == null)
            {
                elementRoleUI = GetComponentInChildren<CharacterElementRoleUI>(true);
            }

            if (contentScrollRect == null)
            {
                contentScrollRect = GetComponentInChildren<ScrollRect>(true);
            }

            if (nodeRoot == null)
            {
                Transform scrollContent = contentScrollRect != null ? contentScrollRect.content : null;
                nodeRoot = scrollContent != null
                    ? scrollContent
                    : FindTransformByNames("PotentialNodes", "NodeRoot", "NodeGrid", "Content");
            }

            if (upgradeWindow == null)
            {
                upgradeWindow = GetComponentInChildren<CharacterPotentialUpgradeWindow>(true);
            }

            if (upgradeWindow == null)
            {
                Transform windowTransform = FindTransformByNames("UpgradeWindow", "Upgrade Window", "PotentialPreviewPanel", "PreviewPanel");
                if (windowTransform != null)
                {
                    upgradeWindow = windowTransform.gameObject.AddComponent<CharacterPotentialUpgradeWindow>();
                }
            }

            if (skillUpgradeButton == null)
            {
                skillUpgradeButton = FindButtonByNames("SkillUpgradeButton", "UpgradeSkill", "SkillPotentialButton");
            }

            if (ultimateUpgradeButton == null)
            {
                ultimateUpgradeButton = FindButtonByNames("UltimateUpgradeButton", "UpgradeUltimate", "UltimatePotentialButton");
            }

            if (passiveUpgradeButton == null)
            {
                passiveUpgradeButton = FindButtonByNames("PassiveUpgradeButton", "UpgradePassive", "PassivePotentialButton");
            }
        }

        private void EnsureRuntimeFallbacks()
        {
            EnsureUpgradeWindow();
            EnsureNodeSlots();
        }

        private void PopulateOwnedFragments()
        {
            if (fragmentOwnedText == null)
            {
                return;
            }

            CharacterProgressionState progression = GetMetaProgression();
            int count = progression != null && _currentCharacter != null
                ? progression.GetFragmentCount(_currentCharacter.characterId)
                : 0;
            fragmentOwnedText.text = PlayerHUD.FormatCharacterFragments(count);
        }

        private void BindFragmentEvents()
        {
            CharacterProgressionState progression = GetMetaProgression();
            if (progression == null)
            {
                return;
            }

            progression.OnFragmentsChanged -= HandleFragmentsChanged;
            progression.OnFragmentsChanged += HandleFragmentsChanged;
            progression.OnMemoryChanged -= HandleMemoryChanged;
            progression.OnMemoryChanged += HandleMemoryChanged;
        }

        private void UnbindFragmentEvents()
        {
            if (metaProgression == null)
            {
                return;
            }

            metaProgression.OnFragmentsChanged -= HandleFragmentsChanged;
            metaProgression.OnMemoryChanged -= HandleMemoryChanged;
        }

        private void HandleMemoryChanged(string characterId)
        {
            if (characterId != null && _currentCharacter?.characterId != characterId) return;
            _memoryCharacter = null;
            PresentMemoryTrack();
        }

        private void HandleFragmentsChanged()
        {
            Refresh();
        }

        private void EnsureUpgradeWindow()
        {
            if (upgradeWindow != null || !createRuntimeFallbackNodes)
            {
                return;
            }

            GameObject windowObject = new GameObject("UpgradeWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CharacterPotentialUpgradeWindow));
            windowObject.transform.SetParent(transform, false);
            RectTransform rectTransform = windowObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 0.5f);
            rectTransform.sizeDelta = new Vector2(380f, -160f);
            rectTransform.anchoredPosition = new Vector2(-24f, -24f);

            Image image = windowObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.82f);

            upgradeWindow = windowObject.GetComponent<CharacterPotentialUpgradeWindow>();
        }

        private void EnsureNodeSlots()
        {
            if (nodeSlots == null || nodeSlots.Length == 0)
            {
                nodeSlots = GetComponentsInChildren<CharacterPotentialNodeUI>(true);
            }

            if ((nodeSlots != null && nodeSlots.Length >= 12) || !createRuntimeFallbackNodes)
            {
                return;
            }

            Transform parent = nodeRoot != null ? nodeRoot : transform;
            GameObject gridObject = new GameObject("PotentialNodeRuntimeGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObject.transform.SetParent(parent, false);

            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.sizeDelta = new Vector2(920f, 360f);

            GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CharacterKitDefinition.MaxPotentialLevel;
            grid.cellSize = new Vector2(200f, 84f);
            grid.spacing = new Vector2(28f, 44f);
            grid.childAlignment = TextAnchor.MiddleCenter;

            List<CharacterPotentialNodeUI> slots = new List<CharacterPotentialNodeUI>(12);
            for (int trackIndex = 0; trackIndex < TrackOrder.Length; trackIndex++)
            {
                for (int level = 1; level <= CharacterKitDefinition.MaxPotentialLevel; level++)
                {
                    CharacterPotentialNodeUI slot = CharacterPotentialNodeUI.CreateRuntimeNode(gridObject.transform);
                    slots.Add(slot);
                }
            }

            nodeSlots = slots.ToArray();
        }

        private CharacterProgressionState GetMetaProgression()
        {
            metaProgression = Project.Training.CharacterStateContext.ResolveProgression(metaProgression);
            if (metaProgression == null)
            {
                metaProgression = Project.Training.CharacterStateContext.ResolveProgression();
            }

            return metaProgression;
        }

        private string ResolveCharacterDisplayName()
        {
            if (_currentCharacter == null)
            {
                return "No Character";
            }

            if (_currentCharacter.metaData != null && !string.IsNullOrWhiteSpace(_currentCharacter.metaData.displayName))
            {
                return _currentCharacter.metaData.displayName;
            }

            if (_currentCharacterItem != null && !string.IsNullOrWhiteSpace(_currentCharacterItem.itemName))
            {
                return _currentCharacterItem.itemName;
            }

            return _currentCharacter.name;
        }

        private string BuildPotentialSummary(CharacterObject data)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < TrackOrder.Length; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                }

                AppendTrackSummary(builder, data, TrackOrder[i]);
            }

            return builder.ToString();
        }

        private void AppendTrackSummary(StringBuilder builder, CharacterObject data, PotentialTrack track)
        {
            CharacterProgressionState progression = GetMetaProgression();
            int currentLevel = progression != null ? progression.GetPotentialLevel(data, track) : 0;
            builder.Append(track);
            builder.Append(" Lv.");
            builder.Append(currentLevel);
            builder.Append("/");
            builder.Append(CharacterKitDefinition.MaxPotentialLevel);

            for (int level = 1; level <= CharacterKitDefinition.MaxPotentialLevel; level++)
            {
                PotentialNodeDefinition node = FindPotentialNode(data, track, level) ?? CreateFallbackNode(track, level);
                CharacterPotentialNodeState state = ResolveNodeState(data, progression, node);
                builder.AppendLine();
                builder.Append("Lv.");
                builder.Append(level);
                builder.Append(" [");
                builder.Append(FormatStateLabel(state));
                builder.Append("] ");
                builder.Append(ResolveNodeName(node));
            }
        }

        private void RefreshLegacyButton(Button button, PotentialTrack track)
        {
            if (button == null)
            {
                return;
            }

            CharacterProgressionState progression = GetMetaProgression();
            bool canUpgrade = progression != null
                && _currentCharacter != null
                && progression.CanUpgradePotential(_currentCharacter, track, out _);
            button.interactable = canUpgrade;
        }

        private void BindButtons()
        {
            if (_buttonsBound)
            {
                return;
            }

            if (exitButton != null) exitButton.onClick.AddListener(ExitPotentialPanel);
            if (skillUpgradeButton != null) skillUpgradeButton.onClick.AddListener(UpgradeSkillPotential);
            if (ultimateUpgradeButton != null) ultimateUpgradeButton.onClick.AddListener(UpgradeUltimatePotential);
            if (passiveUpgradeButton != null) passiveUpgradeButton.onClick.AddListener(UpgradePassivePotential);
            _buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!_buttonsBound)
            {
                return;
            }

            if (exitButton != null) exitButton.onClick.RemoveListener(ExitPotentialPanel);
            if (skillUpgradeButton != null) skillUpgradeButton.onClick.RemoveListener(UpgradeSkillPotential);
            if (ultimateUpgradeButton != null) ultimateUpgradeButton.onClick.RemoveListener(UpgradeUltimatePotential);
            if (passiveUpgradeButton != null) passiveUpgradeButton.onClick.RemoveListener(UpgradePassivePotential);
            _buttonsBound = false;
        }

        private void PlayUpgradeFeedback()
        {
            UIFeedbackDriver feedback = UIFeedbackDriver.GetOrAdd(gameObject);
            if (feedback == null)
            {
                return;
            }

            if (upgradeWindow != null)
            {
                feedback.PlayPulse(upgradeWindow.transform, 1.04f, 0.18f);
            }
        }

        private void ConfigurePotentialHoverFallback()
        {
            if (!enablePotentialHoverFallback || potentialText == null) return;

            UIHoverFallbackFeedback hoverFeedback = potentialText.GetComponent<UIHoverFallbackFeedback>();
            if (hoverFeedback == null) hoverFeedback = potentialText.gameObject.AddComponent<UIHoverFallbackFeedback>();

            hoverFeedback.Configure(potentialHoverScale, potentialHoverGlowColor, potentialHoverDuration);
        }

        private Button FindButtonByNames(params string[] names)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                {
                    continue;
                }

                string buttonName = buttons[i].gameObject.name;
                for (int j = 0; j < names.Length; j++)
                {
                    if (string.Equals(buttonName, names[j], StringComparison.OrdinalIgnoreCase))
                    {
                        return buttons[i];
                    }
                }
            }

            return null;
        }

        private TMP_Text FindTextByNames(params string[] names)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null)
                {
                    continue;
                }

                string textName = texts[i].gameObject.name;
                for (int j = 0; j < names.Length; j++)
                {
                    if (string.Equals(textName, names[j], StringComparison.OrdinalIgnoreCase))
                    {
                        return texts[i];
                    }
                }
            }

            return null;
        }

        private Transform FindTransformByNames(params string[] names)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null)
                {
                    continue;
                }

                string transformName = transforms[i].gameObject.name;
                for (int j = 0; j < names.Length; j++)
                {
                    if (string.Equals(transformName, names[j], StringComparison.OrdinalIgnoreCase))
                    {
                        return transforms[i];
                    }
                }
            }

            return null;
        }

        public static PotentialNodeDefinition FindPotentialNode(CharacterObject data, PotentialTrack track, int level)
        {
            CharacterKitDefinition kit = data != null ? data.kitDefinition : null;
            if (kit == null || kit.potentialNodes == null) return null;

            for (int i = 0; i < kit.potentialNodes.Count; i++)
            {
                PotentialNodeDefinition node = kit.potentialNodes[i];
                if (node != null && node.track == track && node.level == level) return node;
            }

            return null;
        }

        public static PotentialNodeDefinition CreateFallbackNode(PotentialTrack track, int level)
        {
            return new PotentialNodeDefinition
            {
                nodeId = $"{track}.lv{level}",
                nodeName = $"{track} Lv.{level}",
                description = $"Unlocks {track} Potential Lv.{level}.",
                track = track,
                level = Mathf.Clamp(level, 1, CharacterKitDefinition.MaxPotentialLevel),
                fragmentCost = 1
            };
        }

        public static string ResolveNodeName(PotentialNodeDefinition node)
        {
            if (node == null)
            {
                return "Unknown Potential";
            }

            return !string.IsNullOrWhiteSpace(node.nodeName)
                ? node.nodeName
                : $"{node.track} Lv.{node.level}";
        }

        public static string FormatStateLabel(CharacterPotentialNodeState state)
        {
            return state switch
            {
                CharacterPotentialNodeState.Upgraded => "Upgraded",
                CharacterPotentialNodeState.Upgradeable => "Ready",
                CharacterPotentialNodeState.InsufficientFragments => "Locked",
                _ => "Locked"
            };
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
