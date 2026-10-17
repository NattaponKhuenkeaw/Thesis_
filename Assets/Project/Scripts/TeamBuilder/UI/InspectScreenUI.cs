using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI.Panels;
using Project.TeamBuilder.UI.Interfaces;
using Project.Core.ZoneState;
using Project.UI;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Defines which tab is active in the Inspector panel.
    /// Public so UIManager can request a specific page when opening.
    /// </summary>
    public enum InspectPage
    {
        Info,
        Skill,
        Equipment,
        // LEGACY: Kept so existing serialized/prefab bindings remain valid.
        Level,
        Potential
    }

    public class InspectScreenUI : MonoBehaviour, IOrchestratedPanel
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private GameObject emptyStatePanel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button infoTabButton;
        [SerializeField] private Button skillTabButton;
        [SerializeField] private Button equipmentTabButton;
        [SerializeField] private Transform characterSliderGridParent;
        [SerializeField] private GameObject characterSliderItemPrefab;

        [Header("Data Sources")]
        [SerializeField] private PlayerRunInventory playerInventory;
        [SerializeField] private TeamLoader teamLoader;
        [SerializeField] private TeamUI owningTeamUI;

        [Header("Animation")]
        [SerializeField] private float slideOffset = 480f;
        [SerializeField] private float slideDuration = 0.25f;

        [Header("Page Transition")]
        [SerializeField] private Image pageTransitionFadeImage;
        [SerializeField] private float pageTransitionFadeDuration = 0.16f;
        [SerializeField] private Color pageTransitionFadeColor = Color.black;

        private CharacterItem currentCharacter;
        private ArtifactInstance[] currentArtifactInstances = Array.Empty<ArtifactInstance>();
        private Artifact[] currentArtifacts = new Artifact[0];
        private readonly List<CharacterItem> availableCharacters = new List<CharacterItem>();
        private readonly List<CharacterGridItemUI> sliderItems = new List<CharacterGridItemUI>();
        private RectTransform panelRectTransform;
        private CanvasGroup panelCanvasGroup;
        private CanvasGroup rootCanvasGroup;
        private CanvasGroup pageTransitionCanvasGroup;
        private Coroutine fadeCoroutine;
        private Coroutine slideCoroutine;
        private Coroutine pageTransitionCoroutine;
        private Vector2 openAnchoredPosition;
        private int selectedCharacterIndex = -1;
        private UIFeedbackDriver cachedDriver;
        private InspectPage activePage = InspectPage.Info;

        public CharacterItem CurrentCharacter => currentCharacter;
        
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



        private void OnEnable()
        {
            ResolveMissingReferences();

            if (zoneStateProvider != null)
            {
                zoneStateProvider.OnZoneModeChanged += HandleZoneChanged;
                HandleZoneChanged(zoneStateProvider.CurrentMode);
            }

            // Tab and close buttons live inside the panel — wire here so they re-fire after re-enables.
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

            StopPageTransition();
        }

        private void HandleZoneChanged(ZoneMode newMode)
        {
            currentMode = newMode;
            if (currentMode == ZoneMode.CombatZone)
            {
                Close();
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
            rootCanvasGroup = GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null)
            {
                rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

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

            infoPanel = targetPanel.GetComponentInChildren<CharacterInfoPanel>(true);
            skillPanel = targetPanel.GetComponentInChildren<CharacterSkillPanel>(true);
            equipmentPanel = targetPanel.GetComponentInChildren<EquipmentPanel>(true);
            levelPanel = targetPanel.GetComponentInChildren<CharacterLevelPanel>(true);
            potentialPanel = ResolvePotentialPanel(targetPanel);
            visualPanel = targetPanel.GetComponentInChildren<CharacterVisualPanel>(true);
            subPanels = BuildSubPanelList();

            // Cache driver once — coroutines must be stopped on the same component that started them.
            cachedDriver = UIFeedbackDriver.GetOrAdd(gameObject);

            // Set root CanvasGroup to transparent on awake
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 1f;
                panelCanvasGroup.interactable = true;
                panelCanvasGroup.blocksRaycasts = true;
            }
            if (targetPanel != gameObject)
            {
                targetPanel.SetActive(true);
            }
        }

        private void Start()
        {
            // Resolve missing references in Start() when other components are initialized.
            ResolveMissingReferences();
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
            return true;
        }

        public void OpenFirstAvailableCharacter()
        {
            OpenInspector();
        }

        public void OpenInspector()
        {
            // When zoneStateProvider is null (e.g. playground scenes) default to SafeZone
            // so the inspector is not silently blocked.
            ZoneMode liveMode = zoneStateProvider != null ? zoneStateProvider.CurrentMode : ZoneMode.SafeZone;
            currentMode = liveMode;
            if (liveMode != ZoneMode.SafeZone)
            {
                Debug.Log("[InspectScreenUI] OpenInspector blocked: not in SafeZone.");
                return;
            }

            RefreshAvailableCharacters();
            activePage = InspectPage.Info;
            Open(null);
        }

        public void OpenInspectorWithCharacter(string name)
        {
            OpenInspectorWithCharacterOnPage(name, InspectPage.Info);
        }

        /// <summary>
        /// Open the Inspector showing the first available character on a specific page.
        /// Called by UIManager (e.g. UIManager.OpenInspectorOnPotential).
        /// </summary>
        public void OpenInspectorOnPage(InspectPage page)
        {
            ZoneMode liveMode = zoneStateProvider != null ? zoneStateProvider.CurrentMode : ZoneMode.SafeZone;
            currentMode = liveMode;
            if (liveMode != ZoneMode.SafeZone)
            {
                Debug.Log("[InspectScreenUI] OpenInspectorOnPage blocked: not in SafeZone.");
                return;
            }

            RefreshAvailableCharacters();
            activePage = page;
            Open(null);
        }

        /// <summary>
        /// Open the Inspector on a specific character and jump to a specific page.
        /// Called by UIManager (e.g. UIManager.OpenInspectorOnPotentialWithCharacter).
        /// </summary>
        public void OpenInspectorWithCharacterOnPage(string name, InspectPage page)
        {
            ZoneMode liveMode = zoneStateProvider != null ? zoneStateProvider.CurrentMode : ZoneMode.SafeZone;
            currentMode = liveMode;
            if (liveMode != ZoneMode.SafeZone)
            {
                Debug.Log("[InspectScreenUI] OpenInspectorWithCharacterOnPage blocked: not in SafeZone.");
                return;
            }

            RefreshAvailableCharacters();
            CharacterItem targetChar = availableCharacters.Find(c =>
                c != null && c.characterObjectData != null &&
                (string.Equals(c.characterObjectData.name, name, System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c.characterObjectData.characterId, name, System.StringComparison.OrdinalIgnoreCase))
            );

            if (targetChar == null)
            {
                Debug.LogError($"[InspectScreenUI] Character '{name}' not found in available characters!");
                return;
            }

            activePage = page;
            Open(targetChar);
        }

        public void Open(CharacterItem character, ArtifactInstance[] equippedArtifacts = null)
        {
            ZoneMode liveMode = zoneStateProvider != null ? zoneStateProvider.CurrentMode : ZoneMode.SafeZone;
            currentMode = liveMode;
            if (liveMode != ZoneMode.SafeZone)
            {
                return;
            }

            RefreshAvailableCharacters();
            int characterIndex = availableCharacters.IndexOf(character);
            if (characterIndex < 0)
            {
                character = availableCharacters.Count > 0 ? availableCharacters[0] : null;
                characterIndex = character != null ? 0 : -1;
            }

            currentCharacter = character;
            selectedCharacterIndex = characterIndex;
            currentArtifactInstances = equippedArtifacts ?? ResolveEquippedArtifactInstances(character);
            currentArtifacts = ResolveLegacyArtifacts(currentArtifactInstances);
            if (owningTeamUI == null)
            {
                owningTeamUI = GetComponentInParent<TeamUI>();
            }

            owningTeamUI?.RegisterActivePanel(this);

            bool isAlreadyOpen = gameObject.activeSelf && rootCanvasGroup != null && rootCanvasGroup.alpha > 0.01f;

            if (!isAlreadyOpen)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }
                if (contentPanel != null)
                {
                    contentPanel.SetActive(true);
                }
                if (rootCanvasGroup != null)
                {
                    rootCanvasGroup.alpha = 0f;
                    rootCanvasGroup.interactable = true;
                    rootCanvasGroup.blocksRaycasts = true;
                }
                PlayFadeTransition(true);
            }

            PopulateCharacterSlider();
            PopulateData();
            PlayContentSlideTransition();
        }

        public void Open(CharacterItem character)
        {
            Open(character, (ArtifactInstance[])null);
        }

        public void RefreshCurrentArtifactInstances(ArtifactInstance[] equippedArtifacts, PlayerRunInventory inventory)
        {
            currentArtifactInstances = equippedArtifacts ?? Array.Empty<ArtifactInstance>();
            currentArtifacts = ResolveLegacyArtifacts(currentArtifactInstances);
            if (gameObject.activeSelf)
            {
                PopulateData();
            }
        }

        public void RefreshCurrentArtifacts(Artifact[] equippedArtifacts)
        {
            currentArtifacts = equippedArtifacts ?? new Artifact[0];
            if (gameObject.activeSelf)
            {
                PopulateData();
            }
        }

        public void Close()
        {
            bool isVisuallyOpen = gameObject.activeSelf && rootCanvasGroup != null && rootCanvasGroup.alpha > 0.01f;

            if (!isVisuallyOpen)
            {
                // Panel is not visually open, but may still hold a registered lock/action-map.
                // Always release ownership so the action map and input lock are never stranded.
                owningTeamUI?.UnregisterActivePanel(this);
                return;
            }

            StopPageTransition();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }
            PlayFadeTransition(false);
        }

        private void OnDestroy()
        {
            // Safety net: if this object is destroyed (e.g., scene unload) while a panel is registered,
            // unregister immediately so the action map and input lock are always restored.
            owningTeamUI?.UnregisterActivePanel(this);
        }

        private void PlayFadeTransition(bool opening)
        {
            // Always stop on the cached driver — the coroutine was started there.
            if (fadeCoroutine != null && cachedDriver != null)
            {
                cachedDriver.StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            if (cachedDriver != null)
            {
                fadeCoroutine = cachedDriver.PlayFade(rootCanvasGroup, opening, slideDuration, () =>
                {
                    if (!opening)
                    {
                        gameObject.SetActive(false);
                        currentCharacter = null;
                        currentArtifacts = new Artifact[0];
                        ClearCharacterSlider();
                        owningTeamUI?.UnregisterActivePanel(this);
                    }
                    fadeCoroutine = null;
                });
            }
            else
            {
                if (rootCanvasGroup != null)
                {
                    rootCanvasGroup.alpha = opening ? 1f : 0f;
                }
                if (!opening)
                {
                    gameObject.SetActive(false);
                    currentCharacter = null;
                    currentArtifacts = new Artifact[0];
                    ClearCharacterSlider();
                    owningTeamUI?.UnregisterActivePanel(this);
                }
            }
        }

        private void PlayContentSlideTransition()
        {
            if (panelRectTransform == null) return;

            // Stop on the correct owner.
            if (slideCoroutine != null && cachedDriver != null)
            {
                cachedDriver.StopCoroutine(slideCoroutine);
                slideCoroutine = null;
            }

            Vector2 startPos = openAnchoredPosition + new Vector2(slideOffset, 0f);
            Vector2 endPos = openAnchoredPosition;

            if (cachedDriver != null)
            {
                slideCoroutine = cachedDriver.PlaySlide(panelRectTransform, startPos, endPos, slideDuration, () =>
                {
                    slideCoroutine = null;
                });
            }
            else
            {
                panelRectTransform.anchoredPosition = endPos;
            }
        }

        private void PopulateData()
        {
            ResolveSubPanelsIfNeeded();

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

                panel.PopulateData(currentCharacter, currentArtifactInstances);
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

        private void ResolveSubPanelsIfNeeded()
        {
            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            bool missingRequiredPanel = infoPanel == null
                || skillPanel == null
                || equipmentPanel == null
                || potentialPanel == null;
            if (!missingRequiredPanel && subPanels != null && subPanels.Length > 0)
            {
                return;
            }

            infoPanel = targetPanel.GetComponentInChildren<CharacterInfoPanel>(true);
            skillPanel = targetPanel.GetComponentInChildren<CharacterSkillPanel>(true);
            equipmentPanel = targetPanel.GetComponentInChildren<EquipmentPanel>(true);
            levelPanel = targetPanel.GetComponentInChildren<CharacterLevelPanel>(true);
            potentialPanel = ResolvePotentialPanel(targetPanel);
            visualPanel = targetPanel.GetComponentInChildren<CharacterVisualPanel>(true);
            subPanels = BuildSubPanelList();
        }

        private CharacterPotentialPanel ResolvePotentialPanel(GameObject targetPanel)
        {
            CharacterPotentialPanel panel = targetPanel != null
                ? targetPanel.GetComponentInChildren<CharacterPotentialPanel>(true)
                : null;
            if (panel != null)
            {
                return panel;
            }

            panel = GetComponentInChildren<CharacterPotentialPanel>(true);
            if (panel != null)
            {
                return panel;
            }

            Transform parent = transform.parent;
            return parent != null ? parent.GetComponentInChildren<CharacterPotentialPanel>(true) : null;
        }

        private IInspectSubPanel[] BuildSubPanelList()
        {
            List<IInspectSubPanel> panels = new List<IInspectSubPanel>(6);
            AddSubPanel(panels, visualPanel);
            AddSubPanel(panels, infoPanel);
            AddSubPanel(panels, skillPanel);
            AddSubPanel(panels, equipmentPanel);
            AddSubPanel(panels, levelPanel);
            AddSubPanel(panels, potentialPanel);
            return panels.ToArray();
        }

        private static void AddSubPanel(List<IInspectSubPanel> panels, IInspectSubPanel panel)
        {
            if (panel != null && !panels.Contains(panel))
            {
                panels.Add(panel);
            }
        }

        private void RefreshAvailableCharacters()
        {
            ResolveRunInventory();
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

        public ArtifactInstance[] ResolveEquippedArtifactInstances(CharacterItem character)
        {
            if (character == null || character.characterObjectData == null)
            {
                return Array.Empty<ArtifactInstance>();
            }

            var runInv = ResolveRunInventory();
            if (runInv != null)
            {
                int slotIndex = runInv.FindTeamSlot(character.characterObjectData);
                if (slotIndex >= 0)
                {
                    return runInv.GetEquippedArtifactInstancesAt(slotIndex);
                }
            }
            else if (teamLoader != null)
            {
                return teamLoader.GetEquippedArtifactInstancesFor(character.characterObjectData);
            }

            return Array.Empty<ArtifactInstance>();
        }

        private Artifact[] ResolveLegacyArtifacts(ArtifactInstance[] instances)
        {
            if (instances == null || instances.Length == 0) return new Artifact[0];
            var runInv = ResolveRunInventory();
            Artifact[] legacy = new Artifact[instances.Length];
            for (int i = 0; i < instances.Length; i++)
            {
                ArtifactDefinition definition = runInv != null && instances[i] != null
                    ? runInv.ResolveArtifactDefinition(instances[i].definitionId)
                    : null;
                legacy[i] = definition as Artifact;
            }
            return legacy;
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
            // Existing prefab OnClick bindings land on the read-only Info page
            // while the legacy EXP panel remains serialized for migration.
            SetActivePage(InspectPage.Info);
        }

        public void ShowPotentialPage()
        {
            StopPageTransition();
            activePage = InspectPage.Potential;
            PopulateData();
        }

        private void SetActivePage(InspectPage page)
        {
            if (activePage == page)
            {
                PopulateData();
                return;
            }

            activePage = page;
            PopulateData();
        }

        private void PlayPageTransition(InspectPage page)
        {
            StopPageTransition();
            pageTransitionCoroutine = StartCoroutine(PageTransitionRoutine(page));
        }

        private IEnumerator PageTransitionRoutine(InspectPage page)
        {
            CanvasGroup transitionGroup = EnsurePageTransitionCanvasGroup();
            if (transitionGroup == null)
            {
                activePage = page;
                PopulateData();
                pageTransitionCoroutine = null;
                yield break;
            }

            transitionGroup.gameObject.SetActive(true);
            transitionGroup.alpha = 0f;
            transitionGroup.interactable = true;
            transitionGroup.blocksRaycasts = true;
            transitionGroup.transform.SetAsLastSibling();

            yield return FadePageTransition(transitionGroup, 0f, 1f);

            activePage = page;
            PopulateData();

            yield return FadePageTransition(transitionGroup, 1f, 0f);

            transitionGroup.interactable = false;
            transitionGroup.blocksRaycasts = false;
            transitionGroup.gameObject.SetActive(false);
            pageTransitionCoroutine = null;
        }

        private IEnumerator FadePageTransition(CanvasGroup transitionGroup, float startAlpha, float targetAlpha)
        {
            float duration = Mathf.Max(0.01f, pageTransitionFadeDuration);
            float elapsed = 0f;
            transitionGroup.alpha = startAlpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transitionGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            transitionGroup.alpha = targetAlpha;
        }

        private CanvasGroup EnsurePageTransitionCanvasGroup()
        {
            if (pageTransitionCanvasGroup != null)
            {
                return pageTransitionCanvasGroup;
            }

            if (pageTransitionFadeImage == null)
            {
                GameObject overlay = new GameObject("PageTransitionFade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                overlay.transform.SetParent(transform, false);

                RectTransform rectTransform = overlay.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;

                pageTransitionFadeImage = overlay.GetComponent<Image>();
            }

            pageTransitionFadeImage.color = pageTransitionFadeColor;
            pageTransitionCanvasGroup = pageTransitionFadeImage.GetComponent<CanvasGroup>();
            if (pageTransitionCanvasGroup == null)
            {
                pageTransitionCanvasGroup = pageTransitionFadeImage.gameObject.AddComponent<CanvasGroup>();
            }

            pageTransitionCanvasGroup.alpha = 0f;
            pageTransitionCanvasGroup.interactable = false;
            pageTransitionCanvasGroup.blocksRaycasts = false;
            pageTransitionCanvasGroup.gameObject.SetActive(false);
            return pageTransitionCanvasGroup;
        }

        private void StopPageTransition()
        {
            if (pageTransitionCoroutine != null)
            {
                StopCoroutine(pageTransitionCoroutine);
                pageTransitionCoroutine = null;
            }

            if (pageTransitionCanvasGroup != null)
            {
                pageTransitionCanvasGroup.alpha = 0f;
                pageTransitionCanvasGroup.interactable = false;
                pageTransitionCanvasGroup.blocksRaycasts = false;
                pageTransitionCanvasGroup.gameObject.SetActive(false);
            }
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

            // Eagerly resolve teamLoader and playerInventory here (called from Awake)
            // so they are ready before Start() — which is deferred when the GameObject
            // starts inactive (SetActive(false) at end of Awake).
            if (teamLoader == null)
            {
                teamLoader = FindFirstObjectByType<TeamLoader>(FindObjectsInactive.Include);
            }

            ResolveRunInventory();

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

        public PlayerRunInventory ResolveRunInventory()
        {
            if (playerInventory == null)
            {
                if (teamLoader == null)
                {
                    teamLoader = FindFirstObjectByType<TeamLoader>(FindObjectsInactive.Include);
                }
                if (teamLoader != null && teamLoader.RunInventory != null)
                {
                    playerInventory = teamLoader.RunInventory;
                }

                // NOTE: PlayerRunInventory is a ScriptableObject — FindFirstObjectByType
                // cannot locate ScriptableObjects. The correct fallbacks are:
                // TeamBuilderUI.RunInventory (which already holds the reference),
                // or Resources.Load as a last resort.
                if (playerInventory == null)
                {
                    playerInventory = FindFirstObjectByType<TeamBuilderUI>(FindObjectsInactive.Include)?.RunInventory;
                }

                if (playerInventory == null)
                {
                    playerInventory = Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
                }
            }
            return playerInventory;
        }

        public int ResolveRunGlobalLevel()
        {
            PlayerRunInventory inventory = ResolveRunInventory();
            return inventory != null ? inventory.GlobalLevel : 1;
        }

        public int ResolveRunGlobalMaxLevel()
        {
            PlayerRunInventory inventory = ResolveRunInventory();
            return inventory != null ? inventory.GlobalMaxLevel : 30;
        }

        public CharacterUpgrade ResolveCharacterUpgrade(CharacterObject character)
        {
            // Compatibility API for the retained CharacterLevelPanel only.
            if (character == null) return null;
            var runInv = ResolveRunInventory();
            if (runInv != null)
            {
                var state = runInv.GetRunCharacter(character);
                if (state != null && state.upgrade != null)
                {
                    return state.upgrade;
                }
            }
            return character.upgrade;
        }

        public CharacterStats ResolveCharacterStats(CharacterObject character)
        {
            if (character == null) return null;
            var runInv = ResolveRunInventory();
            if (runInv != null)
            {
                return runInv.GetCharacterStats(character);
            }
            return character.GetLeveledStats();
        }

        public CharacterStats ResolveCharacterBaseStats(CharacterObject character)
        {
            if (character == null) return null;
            var runInv = ResolveRunInventory();
            if (runInv != null)
            {
                return runInv.GetCharacterBaseStats(character);
            }
            return character.GetLeveledStats();
        }

        public CharacterStats ResolveCharacterRuntimeStats(CharacterObject character, ArtifactInstance[] equippedArtifacts = null)
        {
            if (character == null) return null;
            if (equippedArtifacts == null && teamLoader != null && teamLoader.ContainsCharacter(character))
            {
                return teamLoader.GetCharacterRuntimeStats(character);
            }

            CharacterStats stats = ResolveCharacterPermanentStats(character, equippedArtifacts);
            if (stats == null)
            {
                return null;
            }

            if (equippedArtifacts != null && equippedArtifacts.Length > 0)
            {
                CharacterStats baseStats = ResolveCharacterBaseStats(character);
                ArtifactStatAggregator.Apply(stats, baseStats, equippedArtifacts);
            }

            ResolveRunInventory()?.ApplyRuntimeEffectsToStats(character, stats);
            return stats;
        }

        public CharacterStats ResolveCharacterRuntimeStats(CharacterObject character, Artifact[] equippedArtifacts)
        {
            if (character == null) return null;
            if (equippedArtifacts == null && teamLoader != null && teamLoader.ContainsCharacter(character))
            {
                return teamLoader.GetCharacterRuntimeStats(character);
            }

            CharacterStats stats = ResolveCharacterPermanentStats(character);
            if (stats == null)
            {
                return null;
            }

            if (equippedArtifacts != null)
            {
                ApplyArtifactBonuses(stats, ResolveCharacterBaseStats(character), equippedArtifacts);
            }

            ResolveRunInventory()?.ApplyRuntimeEffectsToStats(character, stats);
            return stats;
        }

        public CharacterStats ResolveCharacterPermanentStats(CharacterObject character, ArtifactInstance[] equippedArtifacts = null)
        {
            if (character == null) return null;
            if (teamLoader != null && teamLoader.ContainsCharacter(character))
            {
                return teamLoader.GetCharacterPermanentStats(character);
            }

            CharacterStats stats = ResolveCharacterBaseStats(character);
            return stats;
        }

        public CharacterStats ResolveCharacterPermanentStats(CharacterObject character, Artifact[] equippedArtifacts)
        {
            return ResolveCharacterPermanentStats(character, (ArtifactInstance[])null);
        }

        private static void ApplyArtifactBonuses(CharacterStats stats, CharacterStats baseStats, Artifact[] equippedArtifacts)
        {
            if (stats == null || baseStats == null || equippedArtifacts == null)
            {
                return;
            }

            for (int i = 0; i < equippedArtifacts.Length; i++)
            {
                Artifact artifact = equippedArtifacts[i];
                if (artifact == null || artifact.bonusStats == null)
                {
                    continue;
                }

                stats.ApplyArtifactBonuses(baseStats, artifact.bonusStats);
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
