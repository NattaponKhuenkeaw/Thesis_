using System;
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
            Action<PotentialNodeDefinition> onSelected)
        {
            _node = node;
            _onSelected = onSelected;
            ResolveReferences();

            Color resolvedColor = selected ? selectedColor : ResolveStateColor(state);
            if (nodeImage != null)
            {
                nodeImage.color = resolvedColor;
            }

            if (connectorGraphic != null)
            {
                connectorGraphic.color = state == CharacterPotentialNodeState.Upgraded
                    ? upgradedColor
                    : new Color(1f, 1f, 1f, 0.28f);
            }

            if (selectButton != null)
            {
                selectButton.interactable = node != null;
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
