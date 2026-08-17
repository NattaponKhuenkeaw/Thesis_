using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.InventorySystem.Artifacts;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Detail panel for inspecting an artifact. Overlays the screen and can be dismissed
    /// by clicking outside its bounds or kept open permanently.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ArtifactPreviewPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image artifactIcon;
        [SerializeField] private TMP_Text artifactNameText;
        [SerializeField] private GameObject statItemPrefab;
        [SerializeField] private Transform statItemContainer;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text loreText; // Optional lore/flavor text
        [SerializeField] private TMP_Text qualityText;

        [Header("Equip Controls")]
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipButtonText;

        [Header("Behavior Settings")]
        [SerializeField] private bool closeOnOutsideClick = false;
        [SerializeField] private bool keepVisibleWhenEmpty = true;

        // =========================================================
        // Runtime State
        // =========================================================
        private RectTransform panelRect;
        private ArtifactInstance currentArtifact;
        private ArtifactDefinition currentDefinition;
        private ArtifactRollTable currentRollTable;
        private bool isOpen;
        private int openedFrame = -1;

        public event Action<ArtifactInstance> OnEquipRequested;
        public bool IsOpen => isOpen;
        public ArtifactInstance CurrentArtifact => currentArtifact;
        public ArtifactDefinition CurrentDefinition => currentDefinition;

        private void Awake()
        {
            panelRect = GetComponent<RectTransform>();
            ResolveMissingReferences();
            if (keepVisibleWhenEmpty)
            {
                ShowEmpty();
            }
            else
            {
                gameObject.SetActive(false);
                isOpen = false;
            }
        }

        private void Update()
        {
            if (!isOpen || !closeOnOutsideClick) return;
            if (Time.frameCount == openedFrame) return;

            // Dismiss when clicking outside the panel if closeOnOutsideClick is enabled
            if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, UnityEngine.InputSystem.Pointer.current.position.ReadValue(), null))
                {
                    if (keepVisibleWhenEmpty)
                    {
                        ShowEmpty();
                    }
                    else
                    {
                        Close();
                    }
                }
            }
        }

        public void Open(ArtifactInstance artifact, ArtifactDefinition definition, ArtifactRollTable rollTable)
        {
            if (artifact == null || definition == null)
            {
                if (keepVisibleWhenEmpty)
                {
                    ShowEmpty();
                }
                return;
            }

            currentArtifact = artifact;
            currentDefinition = definition;
            currentRollTable = rollTable;
            gameObject.SetActive(true);
            isOpen = true;
            openedFrame = Time.frameCount;
            ResolveMissingReferences();
            PopulateData();

            // Move to front of hierarchy so it renders on top
            transform.SetAsLastSibling();
        }

        public void ShowEmpty()
        {
            currentArtifact = null;
            currentDefinition = null;
            currentRollTable = null;
            gameObject.SetActive(true);
            isOpen = true;
            openedFrame = Time.frameCount;
            ResolveMissingReferences();

            if (artifactIcon != null)
            {
                artifactIcon.sprite = null;
                artifactIcon.enabled = false;
            }

            if (artifactNameText != null)
            {
                artifactNameText.text = "Select an Artifact";
            }

            if (descriptionText != null)
            {
                descriptionText.text = "Select an artifact to view its stats and effects.";
            }

            if (loreText != null)
            {
                loreText.text = string.Empty;
            }

            if (qualityText != null)
            {
                qualityText.text = string.Empty;
            }

            if (equipButton != null)
            {
                equipButton.gameObject.SetActive(false);
            }

            ClearStatItems();
        }

        public void Close()
        {
            if (!isOpen) return;

            gameObject.SetActive(false);
            isOpen = false;
            currentArtifact = null;
            currentDefinition = null;
            currentRollTable = null;
        }

        public void ClearStatItems()
        {
            if (statItemContainer != null)
            {
                foreach (Transform child in statItemContainer)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        public void SetEquipButtonState(bool visible, string buttonLabel = "Equip")
        {
            if (equipButton != null)
            {
                equipButton.gameObject.SetActive(visible);
                if (equipButtonText != null && !string.IsNullOrEmpty(buttonLabel))
                {
                    equipButtonText.text = buttonLabel;
                }
            }
        }

        private void ResolveMissingReferences()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            Image[] images = GetComponentsInChildren<Image>(true);
            Button[] buttons = GetComponentsInChildren<Button>(true);

            artifactNameText ??= FindTextByName(texts, "Name", "Title", "ArtifactName", "p_Text");
            descriptionText ??= FindTextByName(texts, "Description", "Passive", "Effect", "Info", "p_Desc");
            loreText ??= FindTextByName(texts, "Lore", "Flavor");
            qualityText ??= FindTextByName(texts, "Quality", "RollQuality", "headlv");
            artifactIcon ??= FindImageByName(images, "Icon", "ArtifactIcon", "p_Icon");

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null
                    && buttons[i].name.IndexOf("Ascend", StringComparison.OrdinalIgnoreCase) >= 0)
                    buttons[i].gameObject.SetActive(false);
            }

            if (equipButton == null)
            {
                equipButton = FindButtonByName(buttons, "Equip", "EquipButton", "Use", "Confirm", "Select");
                if (equipButton == null)
                {
                    equipButton = CreateRuntimeEquipButton();
                }
            }

            if (equipButton != null)
            {
                equipButtonText ??= equipButton.GetComponentInChildren<TMP_Text>(true);
                equipButton.onClick.RemoveListener(HandleEquipClicked);
                equipButton.onClick.AddListener(HandleEquipClicked);
            }

            if (statItemContainer == null)
            {
                Transform statsRoot = FindChildByName(transform, "Stats", "StatContainer", "Stat Item Container", "Content", "StatPanel");
                statItemContainer = statsRoot != null ? statsRoot : transform;
            }
        }

        private Button CreateRuntimeEquipButton()
        {
            GameObject buttonObj = new GameObject("ArtifactEquipButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObj.transform.SetParent(transform, false);
            RectTransform rect = buttonObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 10f);
            rect.sizeDelta = new Vector2(160f, 40f);
            buttonObj.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.3f, 0.95f);

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(buttonObj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TMP_Text label = textObj.GetComponent<TextMeshProUGUI>();
            label.text = "Equip";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            equipButtonText = label;

            return buttonObj.GetComponent<Button>();
        }

        private void HandleEquipClicked()
        {
            if (currentArtifact != null)
            {
                OnEquipRequested?.Invoke(currentArtifact);
            }
        }

        private void PopulateData()
        {
            if (currentArtifact == null || currentDefinition == null) return;

            if (artifactIcon != null && currentDefinition.icon != null)
            {
                artifactIcon.sprite = currentDefinition.icon;
                artifactIcon.enabled = true;
            }

            if (artifactNameText != null)
            {
                artifactNameText.text = currentDefinition.itemName;
            }

            // Clear existing stat items
            ClearStatItems();

            if (statItemContainer != null && statItemPrefab != null && currentArtifact.rolls != null)
            {
                for (int i = 0; i < currentArtifact.rolls.Count; i++)
                {
                    ArtifactRoll roll = currentArtifact.rolls[i];
                    if (!ArtifactStatCatalog.IsPlayerVisible(roll.statKind)) continue;
                    AddStatItemIfNonZero(
                        ArtifactStatCatalog.GetLabel(roll.statKind),
                        roll.Value,
                        ArtifactStatCatalog.GetFormatKey(roll.statKind));
                }
            }

            if (descriptionText != null)
            {
                string qualityLabel = ArtifactRollQuality.TryGetAverageQuality(currentArtifact, currentRollTable, out float quality)
                    ? $"Roll Quality {quality * 100f:0.##}"
                    : "Roll Quality —";
                descriptionText.text = string.IsNullOrWhiteSpace(currentDefinition.passiveDescription)
                    ? qualityLabel
                    : $"{qualityLabel}\n{currentDefinition.passiveDescription}";
            }

            if (loreText != null)
            {
                loreText.text = currentDefinition.description;
            }

            if (qualityText != null)
            {
                qualityText.text = ArtifactRollQuality.TryGetAverageQuality(currentArtifact, currentRollTable, out float quality)
                    ? $"Roll Quality {quality * 100f:0.##}"
                    : "Roll Quality —";
            }

            if (equipButton != null)
            {
                equipButton.gameObject.SetActive(true);
                if (equipButtonText != null)
                {
                    equipButtonText.text = "Equip";
                }
            }
        }

        private void AddStatItemIfNonZero(string statName, float value, string statKey = "")
        {
            if (Mathf.Abs(value) > 0.001f)
            {
                if (statItemPrefab == null || statItemContainer == null)
                {
                    return;
                }

                GameObject obj = Instantiate(statItemPrefab, statItemContainer);
                StatItemUI ui = obj.GetComponent<StatItemUI>();
                if (ui != null)
                {
                    ui.Setup(statName, value, statKey);
                }
            }
        }

        private static Button FindButtonByName(Button[] buttons, params string[] names)
        {
            if (buttons == null || names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < buttons.Length; j++)
                {
                    if (buttons[j] != null && buttons[j].name.IndexOf(names[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return buttons[j];
                }
            }
            return null;
        }

        private static TMP_Text FindTextByName(TMP_Text[] texts, params string[] names)
        {
            if (texts == null || names == null) return null;

            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < texts.Length; j++)
                {
                    if (texts[j] != null && texts[j].name.IndexOf(names[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return texts[j];
                }
            }

            return null;
        }

        private static Image FindImageByName(Image[] images, params string[] names)
        {
            if (images == null || names == null) return null;

            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < images.Length; j++)
                {
                    if (images[j] != null && images[j].name.IndexOf(names[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return images[j];
                }
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, params string[] names)
        {
            if (root == null || names == null) return null;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < children.Length; j++)
                {
                    if (children[j] != null && children[j].name.IndexOf(names[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return children[j];
                }
            }

            return null;
        }
    }
}
