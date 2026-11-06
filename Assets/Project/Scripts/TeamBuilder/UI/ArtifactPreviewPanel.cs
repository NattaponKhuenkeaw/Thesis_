using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.InventorySystem.Artifacts;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Artifact details shared by the inline inventory preview and the inspector.
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
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;


        [Header("Behavior Settings")]
        [SerializeField] private bool closeOnOutsideClick = false;
        [SerializeField] private bool keepVisibleWhenEmpty = false;
        private Action<ArtifactInstance> equipAction;

        // =========================================================
        // Runtime State
        // =========================================================
        private RectTransform panelRect;
        private ArtifactInstance currentArtifact;
        private ArtifactDefinition currentDefinition;
        private bool isOpen;
        private int openedFrame = -1;

        public bool IsOpen => isOpen;
        public ArtifactInstance CurrentArtifact => currentArtifact;
        public ArtifactDefinition CurrentDefinition => currentDefinition;

        public void SetEquipAction(Action<ArtifactInstance> action)
        {
            equipAction = action;
            if (equipButton != null)
            {
                equipButton.onClick.RemoveListener(EquipSelection);
                equipButton.onClick.AddListener(EquipSelection);
            }
        }

        private void EquipSelection()
        {
            if (currentArtifact != null) equipAction?.Invoke(currentArtifact);
        }

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
                ShowEmpty();
                return;
            }

            // Awake may run on the first activation of an authored inactive preview.
            gameObject.SetActive(true);
            currentArtifact = artifact;
            currentDefinition = definition;
            gameObject.SetActive(true);
            isOpen = true;
            openedFrame = Time.frameCount;
            ResolveMissingReferences();
            PopulateData();

            if (equipButton != null)
            {
                equipButton.gameObject.SetActive(equipAction != null);
                if (equipLabel != null) equipLabel.text = "EQUIP";
            }
            if (GetComponent<LayoutElement>() == null) transform.SetAsLastSibling();
        }

        public void ShowEmpty()
        {
            if (!keepVisibleWhenEmpty) { Close(); return; }
            currentArtifact = null;
            currentDefinition = null;
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

            ClearStatItems();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            isOpen = false;
            currentArtifact = null;
            currentDefinition = null;
        }

        public void ClearStatItems()
        {
            if (statItemContainer != null)
            {
                foreach (Transform child in statItemContainer)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
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
            if (descriptionText != null) descriptionText.richText = true;
            if (loreText != null) loreText.richText = true;
            TMP_Text legacyQualityText = FindTextByName(texts, "Quality", "RollQuality", "headlv");
            if (legacyQualityText != null)
                legacyQualityText.gameObject.SetActive(false);
            artifactIcon ??= FindImageByName(images, "Icon", "ArtifactIcon", "p_Icon");

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    if (buttons[i].name.IndexOf("Ascend", StringComparison.OrdinalIgnoreCase) >= 0
                        || (buttons[i] != equipButton && buttons[i].name.IndexOf("Equip", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        buttons[i].gameObject.SetActive(false);
                    }
                }
            }

            if (statItemContainer == null)
            {
                Transform statsRoot = FindChildByName(transform, "Stats", "StatContainer", "Stat Item Container", "Content", "StatPanel");
                statItemContainer = statsRoot != null ? statsRoot : transform;
            }
            if (statItemContainer != null && statItemContainer.TryGetComponent(out VerticalLayoutGroup statLayout))
            {
                statLayout.childControlWidth = true;
                statLayout.childForceExpandWidth = true;
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
                descriptionText.text = string.IsNullOrWhiteSpace(currentDefinition.passiveDescription)
                    ? string.Empty
                    : currentDefinition.passiveDescription;
                if (loreText == null && !string.IsNullOrWhiteSpace(currentDefinition.description))
                    descriptionText.text += $"\n{currentDefinition.description}";
            }

            if (loreText != null)
            {
                loreText.text = currentDefinition.description;
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
                    // The shared stat prefab may have a fixed authoring width wider than this preview.
                    ui.FitToContainer();
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
