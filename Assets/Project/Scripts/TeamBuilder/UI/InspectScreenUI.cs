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
    /// <summary>
    /// Defines which tab is active in the Inspector panel.
    /// Public so UIManager can request a specific page when opening.
    /// </summary>
    public enum InspectPage
    {
        Info,
        Skill,
        Equipment,
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

        private CharacterItem currentCharacter;
        private Artifact[] currentArtifacts = new Artifact[0];
        private readonly List<CharacterItem> availableCharacters = new List<CharacterItem>();
        private readonly List<CharacterGridItemUI> sliderItems = new List<CharacterGridItemUI>();
        private RectTransform panelRectTransform;
        private CanvasGroup panelCanvasGroup;
        private CanvasGroup rootCanvasGroup;
        private Coroutine fadeCoroutine;
        private Coroutine slideCoroutine;
        private Vector2 openAnchoredPosition;
        private int selectedCharacterIndex = -1;
        private UIFeedbackDriver cachedDriver;
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

            // Gather all sub-panels
            subPanels = targetPanel.GetComponentsInChildren<IInspectSubPanel>(true);
            infoPanel = targetPanel.GetComponentInChildren<CharacterInfoPanel>(true);
            skillPanel = targetPanel.GetComponentInChildren<CharacterSkillPanel>(true);
            equipmentPanel = targetPanel.GetComponentInChildren<EquipmentPanel>(true);
            levelPanel = targetPanel.GetComponentInChildren<CharacterLevelPanel>(true);
            potentialPanel = targetPanel.GetComponentInChildren<CharacterPotentialPanel>(true);
            visualPanel = targetPanel.GetComponentInChildren<CharacterVisualPanel>(true);

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
            return false;
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

        public void Open(CharacterItem character, Artifact[] equippedArtifacts = null)
        {
            ZoneMode liveMode = zoneStateProvider != null ? zoneStateProvider.CurrentMode : ZoneMode.SafeZone;
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
            Open(character, null);
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

        public CharacterUpgrade ResolveCharacterUpgrade(CharacterObject character)
        {
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

        public CharacterStats ResolveCharacterRuntimeStats(CharacterObject character, Artifact[] equippedArtifacts = null)
        {
            if (character == null) return null;
            if (teamLoader != null && teamLoader.ContainsCharacter(character))
            {
                return teamLoader.GetCharacterRuntimeStats(character);
            }

            CharacterStats stats = ResolveCharacterBaseStats(character);
            if (stats == null)
            {
                return null;
            }

            ApplyArtifactBonuses(stats, ResolveCharacterBaseStats(character), equippedArtifacts);
            ResolveRunInventory()?.ApplyRuntimeEffectsToStats(character, stats);
            return stats;
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

                stats.maxHP += baseStats.maxHP * (artifact.bonusStats.maxHP / 100f);
                stats.attack += baseStats.attack * (artifact.bonusStats.attack / 100f);
                stats.defense += baseStats.defense * (artifact.bonusStats.defense / 100f);
                stats.speed += artifact.bonusStats.speed;
                stats.elementalMastery += artifact.bonusStats.elementalMastery;
                stats.critRate += artifact.bonusStats.critRate;
                stats.critDamage += artifact.bonusStats.critDamage;
                stats.cooldownRate += artifact.bonusStats.cooldownRate;
                stats.impact += artifact.bonusStats.impact;
                stats.damageReduction += artifact.bonusStats.damageReduction;
                stats.aeRecharge += artifact.bonusStats.aeRecharge;
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
