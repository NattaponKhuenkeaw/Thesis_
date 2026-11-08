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
        [SerializeField] private ScrollRect detailsScroll;
        [SerializeField] private RectTransform detailsContent;


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

            if (loreText != null) loreText.gameObject.SetActive(false);

            ClearStatItems();
            ResetDetailsScroll();
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
                for (int i = statItemContainer.childCount - 1; i >= 0; i--)
                {
                    Transform child = statItemContainer.GetChild(i);
                    child.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
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
            if (loreText != null) loreText.gameObject.SetActive(false);
            if (descriptionText != null)
            {
                descriptionText.richText = true;
                descriptionText.textWrappingMode = TextWrappingModes.Normal;
                descriptionText.overflowMode = TextOverflowModes.Overflow;
            }
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
            ConfigureDetailsScroll();
        }

        private void ConfigureDetailsScroll()
        {
            if (descriptionText == null || statItemContainer == null || panelRect == null) return;
            if (detailsScroll == null)
            {
                var viewportObject = new GameObject("Artifact Details Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                viewportObject.layer = gameObject.layer;
                viewportObject.transform.SetParent(transform, false);
                detailsScroll = viewportObject.GetComponent<ScrollRect>();
            }

            RectTransform viewport = (RectTransform)detailsScroll.transform;
            if (viewport.GetComponent<Image>() == null) viewport.gameObject.AddComponent<Image>();
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(.5f, .5f);
            RectTransform headerRect = artifactIcon != null
                ? artifactIcon.rectTransform
                : artifactNameText != null ? artifactNameText.rectTransform : null;
            GetLocalVerticalBounds(headerRect, out float iconBottom, out _);
            float topInset = Mathf.Max(0f, panelRect.rect.yMax - iconBottom + 8f);
            float bottomInset = 48f;
            if (equipButton != null && equipButton.transform is RectTransform equipRect)
            {
                GetLocalVerticalBounds(equipRect, out _, out float buttonTop);
                bottomInset = Mathf.Max(0f, buttonTop - panelRect.rect.yMin + 8f);
            }
            viewport.offsetMin = new Vector2(12f, bottomInset);
            viewport.offsetMax = new Vector2(-12f, -topInset);
            var surface = viewport.GetComponent<Image>();
            surface.color = Color.clear;
            surface.raycastTarget = true;

            if (detailsContent == null)
            {
                var contentObject = new GameObject("Artifact Details Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentObject.layer = gameObject.layer;
                contentObject.transform.SetParent(viewport, false);
                detailsContent = (RectTransform)contentObject.transform;
            }
            else if (detailsContent.parent != viewport)
            {
                detailsContent.SetParent(viewport, false);
            }
            if (detailsContent.GetComponent<VerticalLayoutGroup>() == null)
                detailsContent.gameObject.AddComponent<VerticalLayoutGroup>();
            if (detailsContent.GetComponent<ContentSizeFitter>() == null)
                detailsContent.gameObject.AddComponent<ContentSizeFitter>();

            detailsContent.anchorMin = new Vector2(0f, 1f);
            detailsContent.anchorMax = Vector2.one;
            detailsContent.pivot = new Vector2(.5f, 1f);
            detailsContent.sizeDelta = Vector2.zero;
            var stack = detailsContent.GetComponent<VerticalLayoutGroup>();
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.spacing = 8f;
            stack.padding = new RectOffset(2, 16, 2, 2);
            detailsContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (descriptionText.transform.parent != detailsContent) descriptionText.transform.SetParent(detailsContent, false);
            if (statItemContainer != detailsContent && statItemContainer.parent != detailsContent)
                statItemContainer.SetParent(detailsContent, false);
            descriptionText.alignment = TextAlignmentOptions.TopLeft;
            descriptionText.enableAutoSizing = false;
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.overflowMode = TextOverflowModes.Overflow;
            descriptionText.raycastTarget = false;
            var descriptionSizing = descriptionText.GetComponent<LayoutElement>() ?? descriptionText.gameObject.AddComponent<LayoutElement>();
            descriptionSizing.minHeight = 0f;
            descriptionSizing.flexibleHeight = 0f;
            var statSizing = statItemContainer.GetComponent<LayoutElement>() ?? statItemContainer.gameObject.AddComponent<LayoutElement>();
            statSizing.minHeight = 0f;
            statSizing.flexibleHeight = 0f;

            detailsScroll.viewport = viewport;
            detailsScroll.content = detailsContent;
            detailsScroll.horizontal = false;
            detailsScroll.vertical = true;
            detailsScroll.movementType = ScrollRect.MovementType.Clamped;
            detailsScroll.scrollSensitivity = 30f;
            detailsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            viewport.SetAsLastSibling();
            if (equipButton != null) equipButton.transform.SetAsLastSibling();
        }

        private void GetLocalVerticalBounds(RectTransform target, out float bottom, out float top)
        {
            bottom = panelRect.rect.yMin;
            top = panelRect.rect.yMax;
            if (target == null) return;

            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            bottom = float.PositiveInfinity;
            top = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                float y = panelRect.InverseTransformPoint(corners[i]).y;
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }
        }

        private void ResetDetailsScroll()
        {
            if (detailsScroll == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailsContent);
            detailsScroll.StopMovement();
            detailsScroll.verticalNormalizedPosition = 1f;
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

            ArtifactRoll fixedBonus = currentDefinition.fixedStatBonus;
            if (fixedBonus.valueUnits != 0 && ArtifactStatCatalog.IsPlayerVisible(fixedBonus.statKind))
                AddStatItemIfNonZero(ArtifactStatCatalog.GetLabel(fixedBonus.statKind), fixedBonus.Value,
                    ArtifactStatCatalog.GetFormatKey(fixedBonus.statKind));

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
            }

            var statsSizing = statItemContainer != null ? statItemContainer.GetComponent<LayoutElement>() : null;
            if (statsSizing != null)
            {
                statsSizing.minHeight = statItemContainer.childCount * 34f;
                statsSizing.preferredHeight = statsSizing.minHeight;
            }
            ResetDetailsScroll();

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
