using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI.Panels;
using Project.TeamBuilder.UI.Interfaces;
using Project.Core.ZoneState;
using Project.UI;

namespace Project.TeamBuilder.UI
{
    public class InspectScreenUI : MonoBehaviour, IOrchestratedPanel
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private GameObject emptyStatePanel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button infoTabButton;
        [SerializeField] private Button skillTabButton;
        [SerializeField] private Button equipmentTabButton;
        [SerializeField] private Transform characterSliderGridParent;
        [SerializeField] private GameObject characterSliderItemPrefab;

        [Header("Data Sources")]
        [SerializeField] private PlayerShopInventory playerInventory;
        [SerializeField] private TeamLoader teamLoader;
        [SerializeField] private TeamUI owningTeamUI;

        [Header("Animation")]
        [SerializeField] private float slideOffset = 480f;
        [SerializeField] private float slideDuration = 0.25f;

        private CharacterItem currentCharacter;
        private Artifact[] currentArtifacts = new Artifact[0];
        private readonly List<CharacterItem> availableCharacters = new List<CharacterItem>();
        private readonly List<CharacterGridItemUI> sliderItems = new List<CharacterGridItemUI>();
        private RectTransform panelRectTransform;
        private CanvasGroup panelCanvasGroup;
        private Coroutine transitionCoroutine;
        private Vector2 openAnchoredPosition;
        private int selectedCharacterIndex = -1;
        private InspectPage activePage = InspectPage.Info;
        
        [Header("Mode Settings")]
        [SerializeField] private ZoneStateProvider zoneStateProvider;
        private ZoneMode currentMode = ZoneMode.CombatZone;
        
        private IInspectSubPanel[] subPanels;
        private CharacterInfoPanel infoPanel;
        private CharacterSkillPanel skillPanel;
        private EquipmentPanel equipmentPanel;
        private CharacterLevelPanel levelPanel;
        private CharacterPotentialPanel potentialPanel;
        private CharacterVisualPanel visualPanel;

        private enum InspectPage
        {
            Info,
            Skill,
            Equipment,
            Level,
            Potential
        }

        private void OnEnable()
        {
            ResolveMissingReferences();

            if (zoneStateProvider != null)
            {
                zoneStateProvider.OnZoneModeChanged += HandleZoneChanged;
                HandleZoneChanged(zoneStateProvider.CurrentMode);
            }

            if (openButton != null)
            {
                openButton.onClick.RemoveListener(OpenFirstAvailableCharacter);
                openButton.onClick.AddListener(OpenFirstAvailableCharacter);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }

            if (infoTabButton != null)
            {
                infoTabButton.onClick.RemoveListener(ShowInfoPage);
                infoTabButton.onClick.AddListener(ShowInfoPage);
            }

            if (skillTabButton != null)
            {
                skillTabButton.onClick.RemoveListener(ShowSkillPage);
                skillTabButton.onClick.AddListener(ShowSkillPage);
            }

            if (equipmentTabButton != null)
            {
                equipmentTabButton.onClick.RemoveListener(ShowEquipmentPage);
                equipmentTabButton.onClick.AddListener(ShowEquipmentPage);
            }
        }

        private void OnDisable()
        {
            if (zoneStateProvider != null)
            {
                zoneStateProvider.OnZoneModeChanged -= HandleZoneChanged;
            }

            if (openButton != null)
            {
                openButton.onClick.RemoveListener(OpenFirstAvailableCharacter);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }

            if (infoTabButton != null)
            {
                infoTabButton.onClick.RemoveListener(ShowInfoPage);
            }

            if (skillTabButton != null)
            {
                skillTabButton.onClick.RemoveListener(ShowSkillPage);
            }

            if (equipmentTabButton != null)
            {
                equipmentTabButton.onClick.RemoveListener(ShowEquipmentPage);
            }
        }

        private void HandleZoneChanged(ZoneMode newMode)
        {
            currentMode = newMode;
            if (currentMode == ZoneMode.CombatZone)
            {
                Close();
            }

            if (openButton != null)
            {
                openButton.gameObject.SetActive(currentMode == ZoneMode.SafeZone);
            }

            if (subPanels != null)
            {
                foreach (var panel in subPanels)
                {
                    if (panel is IZoneStateListener listener)
                    {
                        listener.OnZoneChanged(newMode);
                    }
                }
            }
        }

        private void Awake()
        {
            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            panelRectTransform = targetPanel.GetComponent<RectTransform>();
            panelCanvasGroup = targetPanel.GetComponent<CanvasGroup>();

            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = targetPanel.AddComponent<CanvasGroup>();
            }

            if (panelRectTransform != null)
            {
                openAnchoredPosition = panelRectTransform.anchoredPosition;
            }

            // Gather all sub-panels
            subPanels = targetPanel.GetComponentsInChildren<IInspectSubPanel>(true);
            infoPanel = targetPanel.GetComponentInChildren<CharacterInfoPanel>(true);
            skillPanel = targetPanel.GetComponentInChildren<CharacterSkillPanel>(true);
            equipmentPanel = targetPanel.GetComponentInChildren<EquipmentPanel>(true);
            levelPanel = targetPanel.GetComponentInChildren<CharacterLevelPanel>(true);
            potentialPanel = targetPanel.GetComponentInChildren<CharacterPotentialPanel>(true);
            visualPanel = targetPanel.GetComponentInChildren<CharacterVisualPanel>(true);
            ResolveMissingReferences();

            // Initialize visibility states without deactivating the host gameObject
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }
            if (targetPanel != gameObject)
            {
                targetPanel.SetActive(false);
            }
        }

        private void Start()
        {
            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<PlayerShopInventory>();
            }

            if (teamLoader == null)
            {
                teamLoader = FindFirstObjectByType<TeamLoader>();
            }

            if (owningTeamUI == null)
            {
                owningTeamUI = GetComponentInParent<TeamUI>();
            }
        }

        public void OnPanelOpened()
        {
            OpenFirstAvailableCharacter();
        }

        public void OnPanelClosed()
        {
            Close();
        }

        public bool TryRequestClose(System.Action closeAction)
        {
            return false;
        }

        public void OpenFirstAvailableCharacter()
        {
            OpenInspector();
        }

        public void OpenInspector()
        {
            ZoneMode liveMode = zoneStateProvider != null ? zoneStateProvider.CurrentMode : currentMode;
            currentMode = liveMode;
            if (liveMode != ZoneMode.SafeZone)
            {
                return;
            }

            RefreshAvailableCharacters();
            CharacterItem firstCharacter = availableCharacters.Count > 0 ? availableCharacters[0] : null;
            activePage = InspectPage.Info;
            Open(firstCharacter);
        }

        public void Open(CharacterItem character, Artifact[] equippedArtifacts = null)
        {
            ZoneMode liveMode = zoneStateProvider != null ? zoneStateProvider.CurrentMode : currentMode;
            currentMode = liveMode;
            if (liveMode != ZoneMode.SafeZone)
            {
                return;
            }

            RefreshAvailableCharacters();
            currentCharacter = character;
            selectedCharacterIndex = availableCharacters.IndexOf(character);
            currentArtifacts = equippedArtifacts ?? ResolveEquippedArtifacts(character);
            if (owningTeamUI == null)
            {
                owningTeamUI = GetComponentInParent<TeamUI>();
            }

            owningTeamUI?.RegisterActivePanel(this);
            SetPanelVisible(true);

            PopulateCharacterSlider();
            PopulateData();
            PlayTransition(true);
        }

        public void Open(CharacterItem character)
        {
            Open(character, null);
        }

        public void Close()
        {
            bool isOpen = currentCharacter != null || IsPanelVisible();
            if (!isOpen)
            {
                return;
            }

            PlayTransition(false);
        }

        private void PopulateData()
        {
            bool hasCharacter = currentCharacter != null && currentCharacter.characterObjectData != null;
            if (emptyStatePanel != null)
            {
                emptyStatePanel.SetActive(!hasCharacter);
            }

            if (!hasCharacter)
            {
                if (subPanels != null)
                {
                    foreach (var panel in subPanels)
                    {
                        panel.Hide();
                    }
                }

                return;
            }

            if (subPanels == null) return;

            foreach (var panel in subPanels)
            {
                bool showPanel = ShouldShowPanel(panel);
                if (showPanel)
                {
                    panel.Show();
                }
                else
                {
                    panel.Hide();
                }

                panel.PopulateData(currentCharacter, currentArtifacts);
            }
        }

        private bool ShouldShowPanel(IInspectSubPanel panel)
        {
            if (panel == null)
            {
                return false;
            }

            if (panel == visualPanel)
            {
                return true;
            }

            return activePage switch
            {
                InspectPage.Info => panel == infoPanel,
                InspectPage.Skill => panel == skillPanel,
                InspectPage.Equipment => panel == equipmentPanel,
                InspectPage.Level => panel == levelPanel,
                InspectPage.Potential => panel == potentialPanel,
                _ => false
            };
        }

        private void RefreshAvailableCharacters()
        {
            availableCharacters.Clear();
            if (playerInventory == null || playerInventory.ownedItems == null)
            {
                return;
            }

            foreach (BaseItem item in playerInventory.ownedItems)
            {
                if (item is CharacterItem characterItem && characterItem.characterObjectData != null)
                {
                    availableCharacters.Add(characterItem);
                }
            }
        }

        private Artifact[] ResolveEquippedArtifacts(CharacterItem character)
        {
            if (teamLoader == null || character == null || character.characterObjectData == null)
            {
                return new Artifact[0];
            }

            Artifact[] artifacts = teamLoader.GetEquippedArtifactsFor(character.characterObjectData);
            return artifacts ?? new Artifact[0];
        }

        private void PlayTransition(bool opening)
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(PlayTransitionRoutine(opening));
        }

        private IEnumerator PlayTransitionRoutine(bool opening)
        {
            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            if (panelRectTransform == null)
            {
                SetPanelVisible(opening);
                if (!opening)
                {
                    currentCharacter = null;
                    currentArtifacts = new Artifact[0];
                }
                yield break;
            }

            if (opening)
            {
                panelRectTransform.anchoredPosition = openAnchoredPosition + new Vector2(slideOffset, 0f);
            }

            float elapsed = 0f;
            Vector2 startPosition = panelRectTransform.anchoredPosition;
            Vector2 endPosition = opening ? openAnchoredPosition : openAnchoredPosition + new Vector2(slideOffset, 0f);
            float startAlpha = panelCanvasGroup != null ? panelCanvasGroup.alpha : (opening ? 0f : 1f);
            float endAlpha = opening ? 1f : 0f;

            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                panelRectTransform.anchoredPosition = Vector2.Lerp(startPosition, endPosition, eased);

                if (panelCanvasGroup != null)
                {
                    panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                }

                yield return null;
            }

            panelRectTransform.anchoredPosition = endPosition;
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = endAlpha;
            }

            if (!opening)
            {
                SetPanelVisible(false);
                currentCharacter = null;
                currentArtifacts = new Artifact[0];
                ClearCharacterSlider();
                owningTeamUI?.UnregisterActivePanel(this);
            }

            transitionCoroutine = null;
        }

        public void ShowInfoPage()
        {
            SetActivePage(InspectPage.Info);
        }

        public void ShowSkillPage()
        {
            SetActivePage(InspectPage.Skill);
        }

        public void ShowEquipmentPage()
        {
            SetActivePage(InspectPage.Equipment);
        }

        public void ShowLevelPage()
        {
            SetActivePage(InspectPage.Level);
        }

        public void ShowPotentialPage()
        {
            SetActivePage(InspectPage.Potential);
        }

        private void SetActivePage(InspectPage page)
        {
            activePage = page;
            PopulateData();
        }

        private void PopulateCharacterSlider()
        {
            ClearCharacterSlider();
            if (characterSliderGridParent == null || characterSliderItemPrefab == null)
            {
                PopulateFallbackCharacterSlider();
                return;
            }

            for (int i = 0; i < availableCharacters.Count; i++)
            {
                CharacterItem character = availableCharacters[i];
                if (character == null)
                {
                    continue;
                }

                GameObject itemObject = Instantiate(characterSliderItemPrefab, characterSliderGridParent);
                CharacterGridItemUI itemUI = itemObject.GetComponent<CharacterGridItemUI>();
                if (itemUI == null)
                {
                    continue;
                }

                itemUI.Init(character, null, i == selectedCharacterIndex, SelectCharacterFromSlider);
                sliderItems.Add(itemUI);
            }
        }

        private void ClearCharacterSlider()
        {
            for (int i = 0; i < sliderItems.Count; i++)
            {
                if (sliderItems[i] != null)
                {
                    Destroy(sliderItems[i].gameObject);
                }
            }

            sliderItems.Clear();
        }

        private void SelectCharacterFromSlider(CharacterItem character)
        {
            Open(character);
        }

        private void PopulateFallbackCharacterSlider()
        {
            Transform parent = characterSliderGridParent != null
                ? characterSliderGridParent
                : (contentPanel != null ? contentPanel.transform : transform);

            for (int i = 0; i < availableCharacters.Count; i++)
            {
                CharacterItem character = availableCharacters[i];
                if (character == null)
                {
                    continue;
                }

                GameObject itemObject = new GameObject("CharacterSliderItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CharacterGridItemUI));
                itemObject.transform.SetParent(parent, false);

                RectTransform rectTransform = itemObject.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(72f, 72f);

                Image image = itemObject.GetComponent<Image>();
                image.sprite = character.icon;
                image.color = i == selectedCharacterIndex ? new Color(0.8f, 0.9f, 1f, 1f) : Color.white;

                GameObject labelObject = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(itemObject.transform, false);

                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0.3f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
                label.text = character.itemName;
                label.fontSize = 12f;
                label.alignment = TextAlignmentOptions.Center;

                CharacterGridItemUI itemUI = itemObject.GetComponent<CharacterGridItemUI>();
                itemUI.Init(character, null, i == selectedCharacterIndex, SelectCharacterFromSlider);
                sliderItems.Add(itemUI);
            }
        }

        private void ResolveMissingReferences()
        {
            if (owningTeamUI == null)
            {
                owningTeamUI = GetComponentInParent<TeamUI>();
            }

            if (openButton == null)
            {
                openButton = FindButtonByNames("OpenButton", "InspectorButton", "CharacterInspectorButton", "InspectButton");
            }

            if (openButton == null || IsHiddenWithPanel(openButton.transform))
            {
                openButton = CreateFallbackOpenButton();
            }

            if (infoTabButton == null)
            {
                infoTabButton = FindButtonByNames("Info", "InfoButton");
            }

            if (skillTabButton == null)
            {
                skillTabButton = FindButtonByNames("Skill", "SkillButton");
            }

            if (equipmentTabButton == null)
            {
                equipmentTabButton = FindButtonByNames("Equipment", "EquipmentButton");
            }

            if (characterSliderGridParent == null)
            {
                characterSliderGridParent = FindTransformByNames("CharacterSliderGrid", "CharacterSlider", "SliderGrid", "SliderContent");
            }

            if (characterSliderItemPrefab == null)
            {
                CharacterGridItemUI existingItem = GetComponentInChildren<CharacterGridItemUI>(true);
                if (existingItem != null)
                {
                    characterSliderItemPrefab = existingItem.gameObject;
                }
            }
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
                    if (string.Equals(buttonName, names[j], System.StringComparison.OrdinalIgnoreCase))
                    {
                        return buttons[i];
                    }
                }
            }

            return null;
        }

        private Button CreateFallbackOpenButton()
        {
            GameObject buttonObject = new GameObject("CharacterInspectorButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Transform parent = transform.parent != null ? transform.parent : transform;
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-24f, -24f);
            rectTransform.sizeDelta = new Vector2(132f, 36f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.14f, 0.16f, 0.2f, 0.9f);

            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "Inspector";
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            return buttonObject.GetComponent<Button>();
        }

        private bool IsHiddenWithPanel(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            return targetPanel == gameObject && target.IsChildOf(transform);
        }

        private bool IsPanelVisible()
        {
            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            if (targetPanel != gameObject)
            {
                return targetPanel.activeSelf;
            }

            return panelCanvasGroup == null || panelCanvasGroup.alpha > 0.01f;
        }

        private void SetPanelVisible(bool visible)
        {
            if (visible && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            if (targetPanel != gameObject)
            {
                targetPanel.SetActive(visible);
            }

            if (!visible && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = visible ? 1f : 0f;
                panelCanvasGroup.interactable = visible;
                panelCanvasGroup.blocksRaycasts = visible;
            }
        }

        private Transform FindTransformByNames(params string[] names)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                string transformName = transforms[i].gameObject.name;
                for (int j = 0; j < names.Length; j++)
                {
                    if (string.Equals(transformName, names[j], System.StringComparison.OrdinalIgnoreCase))
                    {
                        return transforms[i];
                    }
                }
            }

            return null;
        }
    }
}
