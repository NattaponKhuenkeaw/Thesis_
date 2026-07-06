using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Project.InventorySystem.Items;
using Project.Progression;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterPotentialUpgradeWindow : MonoBehaviour
    {
        [Header("Node Details")]
        [SerializeField] private TMP_Text nodeNameText;
        [SerializeField] private TMP_Text nodeLevelText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text stateText;

        [Header("Fragment Requirement")]
        [SerializeField] private GameObject fragmentRequirementObject;
        [SerializeField] private TMP_Text fragmentText;

        [Header("Actions")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text upgradeButtonText;
        [SerializeField] private GameObject upgradedStateObject;
        [SerializeField] private TMP_Text upgradedStateText;

        private CharacterItem _characterItem;
        private CharacterObject _character;
        private PotentialNodeDefinition _node;
        private PlayerMetaProgression _metaProgression;
        private Action<PotentialNodeDefinition> _upgradeRequested;

        private void Awake()
        {
            ResolveReferences();
            EnsureRuntimeLayout();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureRuntimeLayout();
            BindButton();
        }

        private void OnDisable()
        {
            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
            }
        }

        public void BindUpgradeHandler(Action<PotentialNodeDefinition> upgradeRequested)
        {
            _upgradeRequested = upgradeRequested;
        }

        public void Show(
            CharacterItem characterItem,
            CharacterObject character,
            PotentialNodeDefinition node,
            PlayerMetaProgression metaProgression)
        {
            _characterItem = characterItem;
            _character = character;
            _node = node;
            _metaProgression = metaProgression;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            Refresh();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Refresh()
        {
            if (_character == null || _node == null)
            {
                if (nodeNameText != null) nodeNameText.text = "Select Potential";
                if (nodeLevelText != null) nodeLevelText.text = string.Empty;
                if (descriptionText != null) descriptionText.text = string.Empty;
                if (stateText != null) stateText.text = string.Empty;
                if (fragmentRequirementObject != null) fragmentRequirementObject.SetActive(false);
                if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);
                if (upgradedStateObject != null) upgradedStateObject.SetActive(false);
                return;
            }

            CharacterPotentialNodeState state = CharacterPotentialPanel.ResolveNodeState(_character, _metaProgression, _node);
            int availableFragments = _metaProgression != null ? _metaProgression.GetFragmentCount(_character.characterId) : 0;
            int fragmentCost = ResolveFragmentCost();

            if (nodeNameText != null)
            {
                nodeNameText.text = CharacterPotentialPanel.ResolveNodeName(_node);
            }

            if (nodeLevelText != null)
            {
                nodeLevelText.text = $"{_node.track} Lv.{_node.level}";
            }

            if (descriptionText != null)
            {
                descriptionText.text = !string.IsNullOrWhiteSpace(_node.description)
                    ? _node.description
                    : $"Unlocks {_node.track} Potential Lv.{_node.level}.";
            }

            bool upgraded = state == CharacterPotentialNodeState.Upgraded;
            if (fragmentRequirementObject != null)
            {
                fragmentRequirementObject.SetActive(!upgraded);
            }

            if (fragmentText != null)
            {
                fragmentText.text = $"({availableFragments}/{fragmentCost})";
            }

            if (upgradeButton != null)
            {
                upgradeButton.gameObject.SetActive(!upgraded);
                upgradeButton.interactable = state == CharacterPotentialNodeState.Upgradeable;
            }

            if (upgradeButtonText != null)
            {
                upgradeButtonText.text = state == CharacterPotentialNodeState.Upgradeable ? "Upgrade" : "Locked";
            }

            if (upgradedStateObject != null)
            {
                upgradedStateObject.SetActive(upgraded);
            }

            if (upgradedStateText != null)
            {
                upgradedStateText.text = "Upgraded";
            }

            if (stateText != null)
            {
                stateText.text = ResolveStateMessage(state, availableFragments, fragmentCost);
            }
        }

        private int ResolveFragmentCost()
        {
            if (_node == null)
            {
                return 0;
            }

            PotentialNodeDefinition nextNode = _metaProgression != null
                ? _metaProgression.GetNextPotentialNode(_character, _node.track)
                : null;

            if (nextNode != null && nextNode.level == _node.level)
            {
                return Mathf.Max(0, nextNode.fragmentCost);
            }

            return Mathf.Max(0, _node.fragmentCost);
        }

        private static string ResolveStateMessage(CharacterPotentialNodeState state, int availableFragments, int fragmentCost)
        {
            return state switch
            {
                CharacterPotentialNodeState.Upgraded => "Upgraded",
                CharacterPotentialNodeState.Upgradeable => "Ready to upgrade",
                CharacterPotentialNodeState.InsufficientFragments => $"Need {Mathf.Max(0, fragmentCost - availableFragments)} more fragment",
                _ => "Unlock previous potential first"
            };
        }

        private void BindButton()
        {
            if (upgradeButton == null)
            {
                return;
            }

            upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
            upgradeButton.onClick.AddListener(HandleUpgradeClicked);
        }

        private void HandleUpgradeClicked()
        {
            _upgradeRequested?.Invoke(_node);
        }

        private void ResolveReferences()
        {
            if (nodeNameText == null)
            {
                nodeNameText = FindTextByNames("NodeNameText", "PotentialNameText", "NameText", "Title");
            }

            if (descriptionText == null)
            {
                descriptionText = FindTextByNames("DescriptionText", "ContextText", "Description", "Lore");
            }

            if (nodeLevelText == null)
            {
                nodeLevelText = FindTextByNames("NodeLevelText", "LevelText", "PotentialLevelText", "TrackLevelText");
            }

            if (fragmentText == null)
            {
                fragmentText = FindTextByNames("FragmentText", "CostText", "ItemNeededText", "RequirementText");
            }

            if (stateText == null)
            {
                stateText = FindTextByNames("StateText", "StatusText", "LockedText");
            }

            if (upgradedStateText == null)
            {
                upgradedStateText = FindTextByNames("UpgradedText");
            }

            if (upgradeButton == null)
            {
                upgradeButton = FindButtonByNames("UpgradeButton", "AscendButton", "ConfirmButton");
            }

            if (upgradeButton != null && upgradeButtonText == null)
            {
                upgradeButtonText = upgradeButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (fragmentRequirementObject == null && fragmentText != null)
            {
                fragmentRequirementObject = fragmentText.gameObject;
            }

            if (upgradedStateObject == null && upgradedStateText != null)
            {
                upgradedStateObject = upgradedStateText.gameObject;
            }
        }

        private void EnsureRuntimeLayout()
        {
            if (nodeNameText != null && nodeLevelText != null && descriptionText != null && fragmentText != null && upgradeButton != null && upgradedStateText != null)
            {
                return;
            }

            VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(18, 18, 18, 18);
                layout.spacing = 12f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            if (nodeNameText == null)
            {
                nodeNameText = CreateRuntimeText("NodeNameText", 26f, FontStyles.Bold);
            }

            if (nodeLevelText == null)
            {
                nodeLevelText = CreateRuntimeText("NodeLevelText", 18f, FontStyles.Bold);
            }

            if (descriptionText == null)
            {
                descriptionText = CreateRuntimeText("DescriptionText", 18f, FontStyles.Normal);
            }

            if (fragmentText == null)
            {
                fragmentText = CreateRuntimeText("FragmentText", 18f, FontStyles.Normal);
                fragmentRequirementObject = fragmentText.gameObject;
            }

            if (stateText == null)
            {
                stateText = CreateRuntimeText("StateText", 17f, FontStyles.Bold);
            }

            if (upgradeButton == null)
            {
                upgradeButton = CreateRuntimeButton("UpgradeButton", "Upgrade");
                upgradeButtonText = upgradeButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (upgradedStateText == null)
            {
                upgradedStateText = CreateRuntimeText("UpgradedText", 20f, FontStyles.Bold);
                upgradedStateObject = upgradedStateText.gameObject;
            }
        }

        private TMP_Text CreateRuntimeText(string name, float fontSize, FontStyles fontStyle)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;
            return text;
        }

        private Button CreateRuntimeButton(string name, string label)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(220f, 52f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.95f, 0.78f, 0.22f, 1f);

            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 18f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.black;

            return buttonObject.GetComponent<Button>();
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
    }
}
