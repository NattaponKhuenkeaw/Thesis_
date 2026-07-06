using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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
        [SerializeField] private TMP_Text elementRoleText;
        [SerializeField] private Image elementIcon;
        [SerializeField] private CharacterElementRoleUI elementRoleUI;

        [Header("Content")]
        [SerializeField] private ScrollRect contentScrollRect;
        [SerializeField] private Transform nodeRoot;
        [SerializeField] private CharacterPotentialNodeUI[] nodeSlots;
        [SerializeField] private bool createRuntimeFallbackNodes = true;

        [Header("Upgrade Window")]
        [SerializeField] private CharacterPotentialUpgradeWindow upgradeWindow;

        [Header("Data Sources")]
        [SerializeField] private PlayerMetaProgression metaProgression;
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
        private Artifact[] _currentArtifacts = Array.Empty<Artifact>();
        private PotentialNodeDefinition _selectedNode;
        private readonly List<PotentialNodeDefinition> _displayNodes = new List<PotentialNodeDefinition>(12);
        private bool _buttonsBound;
        private bool _upgradeWindowOpen;
        private int _ignoreOutsideClickFrame = -1;

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
            CloseUpgradeWindow();
            Refresh();
        }

        private void OnDisable()
        {
            UnbindButtons();
            CloseUpgradeWindow();
        }

        private void Update()
        {
            HandleUpgradeWindowOutsideClick();
        }

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            _currentCharacterItem = character;
            _currentCharacter = character != null ? character.characterObjectData : null;
            _currentArtifacts = equippedArtifacts ?? Array.Empty<Artifact>();
            ResolveReferences();
            EnsureRuntimeFallbacks();
            BuildDisplayNodes();

            if (_selectedNode == null || !ContainsNode(_selectedNode))
            {
                _selectedNode = null;
            }

            CloseUpgradeWindow();
            Refresh();
        }

        public void UpgradeSkillPotential() => TryUpgradeTrack(PotentialTrack.Skill);
        public void UpgradeUltimatePotential() => TryUpgradeTrack(PotentialTrack.Ultimate);
        public void UpgradePassivePotential() => TryUpgradeTrack(PotentialTrack.Passive);

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
            PlayerMetaProgression metaProgression,
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
            ElementDatabase database = ElementDatabase.Load();
            Sprite icon = database != null ? database.GetIcon(_currentCharacter.elementType) : null;
            if (elementIcon != null)
            {
                elementIcon.sprite = icon;
                elementIcon.enabled = icon != null;
            }

            if (elementRoleText == null)
            {
                return;
            }

            string elementName = _currentCharacter.elementType.ToString();
            if (database != null)
            {
                ElementDefinition definition = database.GetDefinition(_currentCharacter.elementType);
                if (definition != null && !string.IsNullOrWhiteSpace(definition.displayName))
                {
                    elementName = definition.displayName;
                }
            }

            elementRoleText.text = _currentCharacter.characterRole == CharacterRole.None
                ? elementName.ToUpperInvariant()
                : $"{elementName.ToUpperInvariant()} - {_currentCharacter.characterRole.ToString().ToUpperInvariant()}";
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
                slot.Bind(node, state, selected, SelectNode);
            }
        }

        private void RefreshUpgradeWindow()
        {
            if (upgradeWindow == null)
            {
                return;
            }

            upgradeWindow.BindUpgradeHandler(_ => TryUpgradeSelectedNode());

            if (!_upgradeWindowOpen || _currentCharacter == null || _selectedNode == null)
            {
                upgradeWindow.Hide();
                return;
            }

            upgradeWindow.Show(_currentCharacterItem, _currentCharacter, _selectedNode, GetMetaProgression());
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
            if (!_upgradeWindowOpen || upgradeWindow == null || !upgradeWindow.gameObject.activeInHierarchy)
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
            if (IsScreenPointInside(upgradeWindow.transform as RectTransform, screenPosition) || IsScreenPointInsideAnyNode(screenPosition))
            {
                return;
            }

            _selectedNode = null;
            CloseUpgradeWindow();
            RefreshNodes();
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
            if (upgradeWindow != null)
            {
                upgradeWindow.Hide();
            }
        }

        private PotentialNodeDefinition ResolveInitialSelection()
        {
            PlayerMetaProgression progression = GetMetaProgression();
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
            if (inspectScreenUI == null)
            {
                inspectScreenUI = GetComponentInParent<InspectScreenUI>(true);
            }

            if (metaProgression == null)
            {
                metaProgression = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
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

        private PlayerMetaProgression GetMetaProgression()
        {
            if (metaProgression == null)
            {
                metaProgression = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
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
            PlayerMetaProgression progression = GetMetaProgression();
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

            PlayerMetaProgression progression = GetMetaProgression();
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
                CharacterPotentialNodeState.InsufficientFragments => "Need Fragments",
                _ => "Locked"
            };
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
