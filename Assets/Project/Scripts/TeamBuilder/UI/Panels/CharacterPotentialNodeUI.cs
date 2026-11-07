using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.TeamBuilder.UI.Panels
{
    [RequireComponent(typeof(Button))]
    public class CharacterPotentialNodeUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image nodeImage;
        [SerializeField] private Graphic connectorGraphic;
        [SerializeField] private Button selectButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image selectionFrame;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private Sprite lockedFrame;
        [SerializeField] private Sprite availableFrame;
        [SerializeField] private Sprite upgradedFrame;

        [Header("State Colors")]
        [SerializeField] private Color lockedColor = new Color(0.22f, 0.22f, 0.26f, 0.78f);
        [SerializeField] private Color insufficientColor = new Color(0.55f, 0.42f, 0.24f, 0.9f);
        [SerializeField] private Color upgradeableColor = new Color(0.95f, 0.78f, 0.22f, 1f);
        [SerializeField] private Color upgradedColor = new Color(0.32f, 0.78f, 0.48f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.58f, 0.86f, 1f, 1f);

        private PotentialNodeDefinition _node;
        private Action<PotentialNodeDefinition> _onSelected;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelected);
                selectButton.onClick.AddListener(HandleSelected);
            }
        }

        private void OnDisable()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelected);
            }
        }

        public void Bind(
            PotentialNodeDefinition node,
            CharacterPotentialNodeState state,
            bool selected,
            Action<PotentialNodeDefinition> onSelected,
            Sprite icon = null)
        {
            _node = node;
            _onSelected = onSelected;
            ApplyAppearance(state, selected, icon);
            if (selectButton != null) selectButton.interactable = node != null;
        }

        internal Button BindMemoryPreview(string caption, Sprite icon, CharacterPotentialNodeState state, Action preview,
            bool unavailable)
        {
            ResolveReferences();
            if (iconImage == null)
            {
                var host = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                host.transform.SetParent(transform, false);
                iconImage = host.GetComponent<Image>();
                iconImage.rectTransform.anchorMin = Vector2.one * .15f;
                iconImage.rectTransform.anchorMax = Vector2.one * .85f;
                iconImage.rectTransform.offsetMin = iconImage.rectTransform.offsetMax = Vector2.zero;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }
            if (stateLabel == null)
            {
                var host = new GameObject("StateLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                host.transform.SetParent(transform, false);
                stateLabel = host.GetComponent<TMP_Text>();
                stateLabel.rectTransform.anchorMin = new Vector2(-.6f, -.35f);
                stateLabel.rectTransform.anchorMax = new Vector2(1.6f, 0);
                stateLabel.rectTransform.offsetMin = stateLabel.rectTransform.offsetMax = Vector2.zero;
                stateLabel.fontSize = 16;
                stateLabel.alignment = TextAlignmentOptions.Center;
                stateLabel.raycastTarget = false;
            }
            ApplyAppearance(state, false, icon);
            stateLabel.text = caption;
            if (unavailable) AddUnavailableOverlay();
            Transform level = transform.Find("Level");
            if (level != null) level.gameObject.SetActive(false);
            selectButton.interactable = true;
            selectButton.onClick.AddListener(() => preview());
            return selectButton;
        }

        private void AddUnavailableOverlay()
        {
            var host = new GameObject("Unavailable", typeof(RectTransform), typeof(Image));
            host.transform.SetParent(transform, false);
            var shade = host.GetComponent<Image>();
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            shade.color = new Color(0f, 0f, 0f, .82f);
            shade.raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(host.transform, false);
            var text = label.GetComponent<TMP_Text>();
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = stateLabel.font;
            text.fontSize = 19f;
            text.alignment = TextAlignmentOptions.Center;
            text.text = "Unavailable";
            text.raycastTarget = false;
        }

        private void ApplyAppearance(CharacterPotentialNodeState state, bool selected, Sprite icon)
        {
            ResolveReferences();

            Color resolvedColor = selected ? selectedColor : ResolveStateColor(state);
            if (nodeImage != null)
            {
                Sprite frame = state == CharacterPotentialNodeState.Upgraded ? upgradedFrame
                    : state == CharacterPotentialNodeState.Upgradeable ? availableFrame : lockedFrame;
                if (frame != null) nodeImage.sprite = frame;
                nodeImage.color = frame != null
                    ? state == CharacterPotentialNodeState.Upgraded ? new Color(0.72f, 0.4f, 1f) : Color.white
                    : resolvedColor;
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.color = state == CharacterPotentialNodeState.Locked
                    ? new Color(0.42f, 0.42f, 0.48f, 1f) : Color.white;
            }
            if (selectionFrame != null) selectionFrame.enabled = selected;
            if (stateLabel != null)
            {
                stateLabel.text = state switch
                {
                    CharacterPotentialNodeState.Upgraded => "UNLOCKED",
                    CharacterPotentialNodeState.Upgradeable => "UPGRADE",
                    CharacterPotentialNodeState.InsufficientFragments => "UNAVAILABLE",
                    _ => "LOCKED"
                };
                stateLabel.color = state == CharacterPotentialNodeState.Upgraded ? new Color(0.78f, 0.52f, 1f)
                    : state == CharacterPotentialNodeState.Upgradeable ? new Color(1f, 0.86f, 0.2f)
                    : new Color(0.58f, 0.6f, 0.69f);
            }

            if (connectorGraphic != null)
            {
                connectorGraphic.color = state == CharacterPotentialNodeState.Upgraded
                    ? upgradedColor
                    : new Color(1f, 1f, 1f, 0.28f);
            }

        }

        public static CharacterPotentialNodeUI CreateRuntimeNode(Transform parent)
        {
            GameObject root = new GameObject("PotentialNode", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CharacterPotentialNodeUI));
            root.transform.SetParent(parent, false);

            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200f, 84f);

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.22f, 0.22f, 0.26f, 0.78f);

            CharacterPotentialNodeUI nodeUI = root.GetComponent<CharacterPotentialNodeUI>();
            nodeUI.nodeImage = image;
            nodeUI.selectButton = root.GetComponent<Button>();
            return nodeUI;
        }

        private void HandleSelected()
        {
            if (_node != null)
            {
                _onSelected?.Invoke(_node);
            }
        }

        private void ResolveReferences()
        {
            if (selectButton == null)
            {
                selectButton = GetComponent<Button>();
            }

            if (nodeImage == null)
            {
                nodeImage = GetComponent<Image>();
            }
        }

        private Color ResolveStateColor(CharacterPotentialNodeState state)
        {
            return state switch
            {
                CharacterPotentialNodeState.Upgraded => upgradedColor,
                CharacterPotentialNodeState.Upgradeable => upgradeableColor,
                CharacterPotentialNodeState.InsufficientFragments => insufficientColor,
                _ => lockedColor
            };
        }
    }
}
