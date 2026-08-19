using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.UI;
using Project.Core.ZoneState;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Main controller for the Team Builder UI.
    /// Manages the Character Grid Inventory panel and Team Slot panel.
    /// Syncs team data to the TeamLoader for gameplay.
    /// </summary>
    public class TeamBuilderUI : MonoBehaviour, IOrchestratedPanel
    {
        // =========================================================
        // Dependencies
        // =========================================================
        [Header("Dependencies")]
        [Tooltip("Player inventory that stores owned items (CharacterItems live here).")]
        [SerializeField] private PlayerRunInventory playerInventory;

        [Tooltip("The TeamLoader that manages the active team for gameplay.")]
        [SerializeField] private TeamLoader teamLoader;

        [Header("UI References")]
        [Tooltip("Root canvas for drag ghost parenting.")]
        [SerializeField] private Canvas rootCanvas;

        // =========================================================
        // Panels
        // =========================================================
        [Header("Panels")]
        [SerializeField] private InventoryBuilderPanel inventoryPanel;
        [SerializeField] private ArtifactPreviewPanel artifactPreviewPanel;
        [SerializeField] private TeamSlotUI[] teamSlots = new TeamSlotUI[2];
        [SerializeField] private GameObject playerHolderButton;
        [SerializeField] private bool openGridPanelOnSafeZoneOpen = false;

        [Header("Deploy Confirmation")]
        [SerializeField] private GameObject deployConfirmationPanel;
        [SerializeField] private Button deployConfirmButton;
        [SerializeField] private Button deployCancelButton;
        [SerializeField] private TeamUI owningTeamUI;

        [Header("Mode Settings")]
        [SerializeField] private ZoneStateProvider zoneStateProvider;
        private ZoneMode currentMode = ZoneMode.CombatZone;
        [SerializeField] private InspectScreenUI inspectScreenUI;

        [Header("Audio Settings")]
        [SerializeField] [Range(0f, 1f)] private float panelAudioDuckVolume = 0.35f;
        [SerializeField] private AudioLowPassFilter panelLowPassFilter;
        [SerializeField] [Range(10f, 22000f)] private float panelLowPassCutoff = 350f;

        // =========================================================
        // Controllers (Extracted Responsibilities)
        // =========================================================
        private TeamDraftController draftController;
        private DeployConfirmationController deployController;
        private PanelAudioController audioController;

        // =========================================================
        // Runtime State
        // =========================================================
        private IZoneStateListener[] modeHandlers;
        private bool isInitialized = false;
        private int panelOpenedFrame = -1;
        private bool isPanelSessionActive = false;
        private float previousTimeScale = 1f;
        private System.Action pendingCloseAfterDeploy;
        private bool isInventoryPanelOpen = false;
        private int selectedCharacterSwitchSlotIndex = -1;
        private ArtifactSlotUI selectedArtifactSwitchSlot;

        // =========================================================
        // Properties
        // =========================================================
        public Canvas RootCanvas => rootCanvas;
        public ZoneMode CurrentMode => currentMode;
        public bool HasDraftChanges => draftController != null ? draftController.hasDraftChanges : false;
        public int StagedActiveIndex => draftController != null ? draftController.stagedActiveIndex : 0;
        public InspectScreenUI InspectScreenUI => inspectScreenUI;
        public PlayerRunInventory RunInventory => playerInventory;
        public PlayerRunInventory PlayerInventory => playerInventory;

        private void OnEnable()
        {
            if (zoneStateProvider != null)
            {
                zoneStateProvider.OnZoneModeChanged += HandleZoneChanged;
                HandleZoneChanged(zoneStateProvider.CurrentMode);
            }
        }

        private void OnDisable()
        {
            ClearArtifactSwitchSelection();
            if (zoneStateProvider != null)
            {
                zoneStateProvider.OnZoneModeChanged -= HandleZoneChanged;
            }
        }

        private void HandleZoneChanged(ZoneMode newMode)
        {
            SetMode(newMode);
        }

        private void Update()
        {
            if (currentMode != ZoneMode.SafeZone || (inventoryPanel == null || !isInventoryPanelOpen))
            {
                return;
            }

            if (Time.frameCount == panelOpenedFrame)
            {
                return;
            }

            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame && IsPointerOutsideInventoryPanels())
            {
                CloseInventoryPanel();
            }
        }

        private void Start()
        {
            if (!HasConfiguredTeamSlots())
            {
                enabled = false;
                return;
            }

            EnsureInitialized();
            ApplyModeVisualState();


        }

        // =========================================================
        // Initialization & Setup
        // =========================================================
        private void EnsureInitialized()
        {
            if (isInitialized) return;
            isInitialized = true;

            if (!HasConfiguredTeamSlots())
            {
                return;
            }

            // Fallback for rootCanvas if not assigned in inspector
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            }

            if (teamLoader == null)
            {
                teamLoader = FindFirstObjectByType<TeamLoader>();
            }


            if (owningTeamUI == null)
            {
                owningTeamUI = GetComponentInParent<TeamUI>();
            }

            if (playerInventory == null)
            {
                playerInventory = teamLoader != null && teamLoader.RunInventory != null
                    ? teamLoader.RunInventory
                    : Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
            }

            // Instantiate extracted sub-controllers
            draftController = new TeamDraftController(playerInventory, teamLoader, teamSlots);
            deployController = new DeployConfirmationController(
                rootCanvas,
                deployConfirmationPanel,
                deployConfirmButton,
                deployCancelButton,
                OnDeployConfirmed,
                OnDeployCancelled
            );
            audioController = new PanelAudioController(panelAudioDuckVolume, panelLowPassFilter, panelLowPassCutoff);

            deployController.Setup();

            modeHandlers = GetComponentsInChildren<IZoneStateListener>(true);


            // Initialize the grid panels
            if (inventoryPanel != null)
            {
                inventoryPanel.Init(this, rootCanvas);
                inventoryPanel.SetVisible(false);

                inventoryPanel.SubscribeToArtifactClicks(HandleArtifactClicked);
                inventoryPanel.SubscribeToArtifactDoubleClicks(HandleArtifactDoubleClicked);
            }

            // Initialize each team slot
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                    teamSlots[i].Init(this, i);
            }
        }

        // =========================================================
        // Public Panel Lifecycle APIs
        // =========================================================

        public void OnPanelOpened()
        {
            EnsureInitialized();
            ClearArtifactSwitchSelection();
            selectedCharacterSwitchSlotIndex = -1;
            if (isPanelSessionActive)
            {
                draftController.RestoreCachedDraft();
            }
            else
            {
                isPanelSessionActive = true;
                draftController.LoadRuntimeDraft();
            }

            if (currentMode == ZoneMode.SafeZone && openGridPanelOnSafeZoneOpen)
            {
                if (inventoryPanel != null)
                {
                    isInventoryPanelOpen = true;
                    inventoryPanel.ToggleCharacterGrid();
                }
                panelOpenedFrame = Time.frameCount;
            }
            else
            {
                isInventoryPanelOpen = false;
                if (inventoryPanel != null)
                {
                    inventoryPanel.CloseAll();
                    inventoryPanel.SetVisible(false);
                }
            }

            RefreshGrid();
            RefreshActiveIndex();

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            audioController.ApplyPauseAudioState();

            ApplyModeVisualState();
        }

        public void OnPanelClosed()
        {
            pendingCloseAfterDeploy = null;
            ClearArtifactSwitchSelection();
            selectedCharacterSwitchSlotIndex = -1;
            if (inventoryPanel != null)
            {
                inventoryPanel.CloseAll();
                inventoryPanel.SetVisible(false);
            }
            isInventoryPanelOpen = false;
            isPanelSessionActive = false;
            draftController.ClearDraftCache();
            deployController.Hide();
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            audioController.RestorePauseAudioState();

            if (inspectScreenUI != null && inspectScreenUI.gameObject.activeSelf)
            {
                inspectScreenUI.Close();
            }

            if (artifactPreviewPanel != null && artifactPreviewPanel.IsOpen)
            {
                artifactPreviewPanel.Close();
            }

            if (teamLoader != null)
            {
                teamLoader.RefreshRuntimeStatsAndResources();
            }

            ApplyModeVisualState();
            owningTeamUI?.UnregisterActivePanel(this);
            gameObject.SetActive(false);
        }

        public bool TryRequestClose(System.Action closeAction)
        {
            return TryRequestSafeZoneClose(closeAction);
        }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            EnsureInitialized();
            owningTeamUI?.RegisterActivePanel(this);
            OnPanelOpened();
        }

        public void ClosePanel()
        {
            OnPanelClosed();
        }

        public void TogglePanel()
        {
            if (gameObject.activeSelf)
            {
                ClosePanel();
            }
            else
            {
                OpenPanel();
            }
        }

        public void SetMode(ZoneMode mode)
        {
            currentMode = mode;
            if (currentMode == ZoneMode.CombatZone)
            {
                inventoryPanel?.CloseAll();
                selectedCharacterSwitchSlotIndex = -1;
                ClearArtifactSwitchSelection();
            }

            ApplyModeVisualState();
        }

        // =========================================================
        // Character & Artifact Grid Panels
        // =========================================================

        public void CloseInventoryPanel()
        {
            if (inventoryPanel != null)
            {
                inventoryPanel.CloseAll();
                inventoryPanel.SetVisible(false);
            }
            isInventoryPanelOpen = false;
            selectedCharacterSwitchSlotIndex = -1;
            ClearArtifactSwitchSelection();
            ApplyModeVisualState();
        }

        public void ToggleInventoryPanel()
        {
            if (currentMode != ZoneMode.SafeZone)
            {
                CloseInventoryPanel();
                return;
            }

            if (inventoryPanel != null)
            {
                isInventoryPanelOpen = !isInventoryPanelOpen;
                if (isInventoryPanelOpen)
                {
                    inventoryPanel.SetVisible(true);
                    if (!inventoryPanel.IsAnyPanelOpen)
                    {
                        inventoryPanel.ToggleCharacterGrid();
                    }
                    panelOpenedFrame = Time.frameCount;
                    RefreshGrid();
                }
                else
                {
                    inventoryPanel.CloseAll();
                    inventoryPanel.SetVisible(false);
                    selectedCharacterSwitchSlotIndex = -1;
                    ClearArtifactSwitchSelection();
                }
            }
            ApplyModeVisualState();
        }

        public void CloseCharacterGridPanel()
        {
            if (inventoryPanel != null && inventoryPanel.IsCharacterGridOpen)
            {
                inventoryPanel.CloseAll();
            }
            isInventoryPanelOpen = inventoryPanel != null && inventoryPanel.IsAnyPanelOpen;
            if (!isInventoryPanelOpen)
            {
                selectedCharacterSwitchSlotIndex = -1;
            }
            ApplyModeVisualState();
        }

        public void ToggleCharacterGridPanel()
        {
            if (currentMode != ZoneMode.SafeZone)
            {
                CloseInventoryPanel();
                return;
            }

            ClearArtifactSwitchSelection();
            if (inventoryPanel != null)
            {
                inventoryPanel.SetVisible(true);
                inventoryPanel.ToggleCharacterGrid();
                isInventoryPanelOpen = inventoryPanel.IsAnyPanelOpen;
                if (inventoryPanel.IsCharacterGridOpen)
                {
                    panelOpenedFrame = Time.frameCount;
                    RefreshGrid();
                }
            }
            ApplyModeVisualState();
        }

        public void BeginCharacterSwitch(int slotIndex)
        {
            if (currentMode != ZoneMode.SafeZone)
            {
                CloseInventoryPanel();
                return;
            }

            if (slotIndex < 0 || slotIndex >= teamSlots.Length || teamSlots[slotIndex] == null)
            {
                return;
            }

            ClearArtifactSwitchSelection();
            selectedCharacterSwitchSlotIndex = slotIndex;
            isInventoryPanelOpen = true;

            if (inventoryPanel != null)
            {
                inventoryPanel.SetVisible(true);
                if (!inventoryPanel.IsCharacterGridOpen)
                {
                    inventoryPanel.ToggleCharacterGrid();
                }

                panelOpenedFrame = Time.frameCount;
                RefreshGrid();
            }

            ApplyModeVisualState();
        }

        public void TryAssignCharacterToSelectedSlot(CharacterItem incomingChar)
        {
            if (currentMode != ZoneMode.SafeZone || incomingChar == null)
            {
                return;
            }

            if (selectedCharacterSwitchSlotIndex < 0 || selectedCharacterSwitchSlotIndex >= teamSlots.Length)
            {
                return;
            }

            TeamSlotUI targetSlot = teamSlots[selectedCharacterSwitchSlotIndex];
            if (targetSlot == null)
            {
                return;
            }

            TeamSlotUI existingSlot = FindSlotWithCharacter(incomingChar);
            if (existingSlot != null && existingSlot != targetSlot)
            {
                existingSlot.SetCharacter(targetSlot.CurrentCharacter);
                targetSlot.SetCharacter(incomingChar);
            }
            else if (existingSlot == targetSlot)
            {
                return;
            }
            else
            {
                targetSlot.SetCharacter(incomingChar);
            }

            OnSlotChanged();
            CloseInventoryPanel();
        }

        public void ToggleArtifactGridPanel()
        {
            if (currentMode != ZoneMode.SafeZone)
            {
                CloseInventoryPanel();
                return;
            }

            ClearArtifactSwitchSelection();
            selectedCharacterSwitchSlotIndex = -1;
            if (inventoryPanel != null)
            {
                inventoryPanel.SetVisible(true);
                inventoryPanel.ToggleArtifactGrid();
                isInventoryPanelOpen = inventoryPanel.IsAnyPanelOpen;
                if (inventoryPanel.IsArtifactGridOpen)
                {
                    panelOpenedFrame = Time.frameCount;
                    RefreshGrid();
                }
            }
            ApplyModeVisualState();
        }

        public void CloseArtifactGridPanel()
        {
            if (inventoryPanel != null && inventoryPanel.IsArtifactGridOpen)
            {
                inventoryPanel.CloseAll();
            }
            isInventoryPanelOpen = inventoryPanel != null && inventoryPanel.IsAnyPanelOpen;
            ClearArtifactSwitchSelection();
            ApplyModeVisualState();
        }

        public void BeginArtifactSwitch(ArtifactSlotUI destinationSlot)
        {
            if (currentMode != ZoneMode.SafeZone)
            {
                CloseInventoryPanel();
                return;
            }

            if (destinationSlot == null
                || destinationSlot.ReadOnly
                || destinationSlot.ParentSlot == null
                || destinationSlot.ParentSlot.CurrentCharacter == null)
            {
                return;
            }

            ClearArtifactSwitchSelection();
            selectedCharacterSwitchSlotIndex = -1;
            if (artifactPreviewPanel != null)
            {
                if (destinationSlot.CurrentArtifact != null && playerInventory != null)
                {
                    artifactPreviewPanel.Open(
                        destinationSlot.CurrentArtifact,
                        playerInventory.ResolveArtifactDefinition(destinationSlot.CurrentArtifact.definitionId),
                        playerInventory.ArtifactRollTable);
                }
                else
                {
                    artifactPreviewPanel.ShowEmpty();
                }
            }
            selectedArtifactSwitchSlot = destinationSlot;
            selectedArtifactSwitchSlot.SetSelectionDestination(true);
            isInventoryPanelOpen = true;

            if (inventoryPanel != null)
            {
                inventoryPanel.SetVisible(true);
                if (!inventoryPanel.IsArtifactGridOpen)
                {
                    inventoryPanel.ToggleArtifactGrid();
                }
                panelOpenedFrame = Time.frameCount;
                RefreshGrid();
            }

            ApplyModeVisualState();
        }

        public HashSet<string> GetDraftEquippedArtifactInstanceIds(HashSet<string> destination = null)
        {
            destination ??= new HashSet<string>(StringComparer.Ordinal);
            destination.Clear();

            if (teamSlots != null)
            {
                for (int i = 0; i < teamSlots.Length; i++)
                {
                    if (teamSlots[i] == null) continue;
                    ArtifactInstance[] equipped = teamSlots[i].GetEquippedArtifacts();
                    if (equipped == null) continue;
                    for (int j = 0; j < equipped.Length; j++)
                    {
                        if (equipped[j] != null && !string.IsNullOrWhiteSpace(equipped[j].instanceId))
                        {
                            destination.Add(equipped[j].instanceId);
                        }
                    }
                }
            }
            return destination;
        }

        public bool IsArtifactEquippedInDraft(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || teamSlots == null) return false;
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] == null) continue;
                ArtifactInstance[] equipped = teamSlots[i].GetEquippedArtifacts();
                if (equipped == null) continue;
                for (int j = 0; j < equipped.Length; j++)
                {
                    if (equipped[j] != null && string.Equals(equipped[j].instanceId, instanceId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public TeamSlotUI GetActiveOrFirstTeamSlot()
        {
            if (teamSlots == null || teamSlots.Length == 0) return null;
            int activeIdx = StagedActiveIndex >= 0 && StagedActiveIndex < teamSlots.Length ? StagedActiveIndex : 0;
            if (teamSlots[activeIdx] != null && teamSlots[activeIdx].CurrentCharacter != null)
                return teamSlots[activeIdx];

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null && teamSlots[i].CurrentCharacter != null)
                    return teamSlots[i];
            }
            return teamSlots[0];
        }

        public bool TryAssignArtifactToSelectedSlot(ArtifactInstance incomingArtifact)
        {
            return TryEquipArtifact(incomingArtifact);
        }

        public bool TryEquipArtifact(ArtifactInstance artifact)
        {
            if (currentMode != ZoneMode.SafeZone || artifact == null)
            {
                return false;
            }

            ArtifactSlotUI targetSlot = selectedArtifactSwitchSlot;
            if (targetSlot == null)
            {
                TeamSlotUI activeSlot = GetActiveOrFirstTeamSlot();
                if (activeSlot != null)
                {
                    targetSlot = activeSlot.GetFirstEmptyOrFirstArtifactSlot();
                }
            }

            if (targetSlot == null
                || targetSlot.ReadOnly
                || targetSlot.ParentSlot == null
                || targetSlot.ParentSlot.CurrentCharacter == null)
            {
                return false;
            }

            // If already equipped on this exact target slot, unequip it
            if (targetSlot.GetArtifact() != null && string.Equals(targetSlot.GetArtifact().instanceId, artifact.instanceId, StringComparison.Ordinal))
            {
                targetSlot.ClearArtifact();
                targetSlot.ParentSlot.RecalculateStats();
                OnSlotChanged();
                return true;
            }

            // Equip to target slot (HandleArtifactDrop automatically un-equips from any other slot on team)
            targetSlot.ParentSlot.HandleArtifactDrop(targetSlot, artifact);
            ClearArtifactSwitchSelection();
            return true;
        }

        public void CancelArtifactSwitch()
        {
            ClearArtifactSwitchSelection();
        }

        private void ClearArtifactSwitchSelection()
        {
            if (selectedArtifactSwitchSlot != null)
            {
                selectedArtifactSwitchSlot.SetSelectionDestination(false);
                selectedArtifactSwitchSlot = null;
            }
        }

        // =========================================================
        // Queries & Slot Lookups
        // =========================================================

        public bool IsCharacterInTeam(CharacterItem character)
        {
            if (character == null) return false;

            foreach (var slot in teamSlots)
            {
                if (slot != null && slot.CurrentCharacter == character)
                    return true;
            }
            return false;
        }

        public TeamSlotUI FindSlotWithCharacter(CharacterItem character)
        {
            if (character == null) return null;

            foreach (var slot in teamSlots)
            {
                if (slot != null && slot.CurrentCharacter == character)
                    return slot;
            }
            return null;
        }

        public bool IsArtifactEquippedOnTeam(ArtifactInstance artifact)
        {
            return FindSlotWithArtifact(artifact) != null;
        }

        public CharacterUpgrade ResolveCharacterUpgrade(CharacterObject character)
        {
            // Compatibility API for the retained CharacterLevelPanel only.
            if (character == null)
            {
                return null;
            }

            PlayerRunInventory inventory = ResolveRunInventoryReference();
            RunCharacterState state = inventory != null ? inventory.GetRunCharacter(character) : null;
            return state != null && state.upgrade != null
                ? state.upgrade
                : character.upgrade;
        }

        public int ResolveRunGlobalLevel()
        {
            PlayerRunInventory inventory = ResolveRunInventoryReference();
            return inventory != null ? inventory.GlobalLevel : 1;
        }

        public int ResolveRunGlobalMaxLevel()
        {
            PlayerRunInventory inventory = ResolveRunInventoryReference();
            return inventory != null ? inventory.GlobalMaxLevel : 30;
        }

        public CharacterStats ResolveCharacterStats(CharacterObject character)
        {
            if (character == null)
            {
                return new CharacterStats();
            }

            PlayerRunInventory inventory = ResolveRunInventoryReference();
            return inventory != null
                ? inventory.GetCharacterStats(character)
                : character.GetLeveledStats();
        }

        public CharacterStats ResolveCharacterBaseStats(CharacterObject character)
        {
            if (character == null)
            {
                return new CharacterStats();
            }

            PlayerRunInventory inventory = ResolveRunInventoryReference();
            return inventory != null
                ? inventory.GetCharacterBaseStats(character)
                : character.GetLeveledStats();
        }

        public CharacterStats ResolveCharacterPermanentStats(CharacterObject character)
        {
            if (character == null)
            {
                return new CharacterStats();
            }

            TeamLoader loader = ResolveTeamLoaderReference();
            if (loader != null && loader.ContainsCharacter(character))
            {
                return loader.GetCharacterPermanentStats(character);
            }

            return ResolveCharacterBaseStats(character);
        }

        public CharacterStats ResolveCharacterRuntimeStats(CharacterObject character, ArtifactInstance[] equippedArtifacts = null)
        {
            if (character == null)
            {
                return new CharacterStats();
            }

            TeamLoader loader = ResolveTeamLoaderReference();
            if (equippedArtifacts == null && loader != null && loader.ContainsCharacter(character))
            {
                return loader.GetCharacterRuntimeStats(character);
            }

            CharacterStats stats = ResolveCharacterPermanentStats(character);
            CharacterStats baseStats = ResolveCharacterBaseStats(character);
            ApplyArtifactBonuses(stats, baseStats, equippedArtifacts);
            ResolveRunInventoryReference()?.ApplyRuntimeEffectsToStats(character, stats);
            return stats;
        }

        public TeamSlotUI FindSlotWithArtifact(ArtifactInstance artifact)
        {
            if (artifact == null) return null;

            foreach (var slot in teamSlots)
            {
                if (slot != null && slot.FindArtifactSlotWith(artifact) != null)
                {
                    return slot;
                }
            }
            return null;
        }

        private void RefreshOpenInspectorArtifacts()
        {
            if (inspectScreenUI == null || !inspectScreenUI.gameObject.activeSelf || inspectScreenUI.CurrentCharacter == null)
            {
                return;
            }

            TeamSlotUI slot = FindSlotWithCharacter(inspectScreenUI.CurrentCharacter);
            if (slot != null)
            {
                inspectScreenUI.RefreshCurrentArtifactInstances(slot.GetEquippedArtifacts(), playerInventory);
            }
        }

        private PlayerRunInventory ResolveRunInventoryReference()
        {
            if (playerInventory != null)
            {
                return playerInventory;
            }

            if (teamLoader == null)
            {
                teamLoader = FindFirstObjectByType<TeamLoader>(FindObjectsInactive.Include);
            }

            if (teamLoader != null && teamLoader.RunInventory != null)
            {
                playerInventory = teamLoader.RunInventory;
            }

            if (playerInventory == null)
            {
                playerInventory = Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
            }

            return playerInventory;
        }

        private TeamLoader ResolveTeamLoaderReference()
        {
            if (teamLoader == null)
            {
                teamLoader = FindFirstObjectByType<TeamLoader>(FindObjectsInactive.Include);
            }

            return teamLoader;
        }

        private static void ApplyArtifactBonuses(CharacterStats stats, CharacterStats baseStats, ArtifactInstance[] equippedArtifacts)
        {
            ArtifactStatAggregator.Apply(stats, baseStats, equippedArtifacts);
        }

        // =========================================================
        // Team Slot Actions & Callbacks
        // =========================================================

        public void OnSlotChanged()
        {
            if (draftController == null) return;

            draftController.hasDraftChanges = true;

            if (inventoryPanel != null)
                inventoryPanel.RefreshInTeamStates();

            deployController.Hide();
            RefreshActiveIndex();
            RefreshOpenInspectorArtifacts();
            ApplyModeVisualState();
        }

        public void OnDeployClicked()
        {
            if (currentMode != ZoneMode.SafeZone)
            {
                return;
            }

            EnsureInitialized();
            deployController.Show();
        }

        public void OnDeployConfirmed()
        {
            draftController.SaveDraftCache();
            deployController.Hide();
            DeployDraftToRuntime();

            System.Action closeAfterDeploy = pendingCloseAfterDeploy;
            pendingCloseAfterDeploy = null;

            if (closeAfterDeploy != null)
            {
                closeAfterDeploy.Invoke();
            }
        }

        public void OnDeployCancelled()
        {
            System.Action closeAfterCancel = pendingCloseAfterDeploy;
            pendingCloseAfterDeploy = null;
            deployController.Hide();

            if (closeAfterCancel != null)
            {
                closeAfterCancel.Invoke();
            }
        }

        public void OnCancelClicked()
        {
            pendingCloseAfterDeploy = null;
            deployController.Hide();
            draftController.LoadRuntimeDraft();
            RefreshGrid();
            RefreshActiveIndex();
            ApplyModeVisualState();
        }

        public bool TryResolveArtifactSelection(ArtifactInstance currentArtifact, int direction, out ArtifactInstance resolvedArtifact)
        {
            resolvedArtifact = currentArtifact;

            List<ArtifactInstance> ownedArtifacts = GetOwnedArtifacts();
            if (ownedArtifacts.Count == 0)
            {
                resolvedArtifact = null;
                return false;
            }

            int optionCount = ownedArtifacts.Count + 1;
            int currentIndex = 0;

            if (currentArtifact != null)
            {
                int ownedIndex = ownedArtifacts.IndexOf(currentArtifact);
                currentIndex = ownedIndex >= 0 ? ownedIndex + 1 : 0;
            }

            int nextIndex = currentIndex + (direction >= 0 ? 1 : -1);
            if (nextIndex < 0)
            {
                nextIndex = optionCount - 1;
            }
            else if (nextIndex >= optionCount)
            {
                nextIndex = 0;
            }

            resolvedArtifact = nextIndex == 0 ? null : ownedArtifacts[nextIndex - 1];
            return true;
        }

        public List<ArtifactInstance> GetOwnedArtifacts()
        {
            List<ArtifactInstance> ownedArtifacts = new List<ArtifactInstance>();
            if (playerInventory == null)
            {
                return ownedArtifacts;
            }

            IReadOnlyList<ArtifactInstance> instances = playerInventory.ArtifactInstances;
            for (int i = 0; i < instances.Count; i++)
            {
                ArtifactInstance instance = instances[i];
                ArtifactDefinition definition = instance != null
                    ? playerInventory.ResolveArtifactDefinition(instance.definitionId)
                    : null;
                if (instance != null && definition != null && definition.isEquippable)
                    ownedArtifacts.Add(instance);
            }

            return ownedArtifacts;
        }

        // =========================================================
        // Staged Active Team Index
        // =========================================================

        public void SetActiveIndex(int index)
        {
            if (draftController == null || index < 0 || index >= teamSlots.Length)
                return;

            if (teamSlots[index] == null || teamSlots[index].CurrentCharacter == null)
                return;

            if (draftController.stagedActiveIndex == index)
                return;

            draftController.stagedActiveIndex = index;
            draftController.hasDraftChanges = true;
            RefreshActiveIndex();
            draftController.SaveDraftCache();
        }

        public void RefreshActiveIndex()
        {
            if (draftController == null) return;

            draftController.NormalizeStagedActiveIndex();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                {
                    teamSlots[i].SetActiveState(i == draftController.stagedActiveIndex);
                }
            }
        }

        // =========================================================
        // Character Slot Removal (Drag & Drop)
        // =========================================================

        public void RemoveCharacterFromSlot(int slotIndex)
        {
            TryRemoveCharacterFromSlot(slotIndex);
        }

        public bool CanRemoveCharacterFromSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= teamSlots.Length)
                return false;

            if (teamSlots[slotIndex] == null || teamSlots[slotIndex].CurrentCharacter == null)
                return false;

            int occupiedCount = 0;
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null && teamSlots[i].CurrentCharacter != null)
                    occupiedCount++;
            }

            return occupiedCount > 1;
        }

        public bool TryRemoveCharacterFromSlot(int slotIndex)
        {
            if (!CanRemoveCharacterFromSlot(slotIndex))
                return false;

            if (teamSlots[slotIndex] != null)
                teamSlots[slotIndex].ClearSlot();

            OnSlotChanged();
            return true;
        }

        public bool TryRequestSafeZoneClose(System.Action closeAfterSave)
        {
            if (currentMode != ZoneMode.SafeZone)
            {
                return false;
            }

            EnsureInitialized();
            draftController.SaveDraftCache();
            pendingCloseAfterDeploy = closeAfterSave;
            deployController.Show();
            return true;
        }

        public bool IsSlotRemovalDropTarget(PointerEventData eventData)
        {
            GameObject dropTarget = eventData != null ? eventData.pointerEnter : null;
            if (dropTarget != null)
            {
                if (dropTarget.GetComponentInParent<TeamSlotUI>() != null
                    || dropTarget.GetComponentInParent<ArtifactSlotUI>() != null)
                {
                    return false;
                }

                if (dropTarget.GetComponentInParent<InventoryBuilderPanel>() != null)
                {
                    return true;
                }
            }

            RectTransform builderRect = transform as RectTransform;
            return !IsScreenPointInsideRect(builderRect, eventData != null ? eventData.position : Vector2.zero);
        }

        // =========================================================
        // Helper Methods
        // =========================================================

        public bool TryGetCharacterHealth(CharacterObject character, out float currentHp, out float maxHp)
        {
            currentHp = 0f;
            maxHp = 0f;

            if (teamLoader == null || character == null)
                return false;

            maxHp = teamLoader.GetCharacterMaxHP(character);
            currentHp = teamLoader.GetCharacterCurrentHP(character);
            return maxHp > 0f;
        }

        private void DeployDraftToRuntime()
        {
            draftController.NormalizeStagedActiveIndex();
            CharacterObject[] teamData = draftController.BuildDraftTeamData();
            ArtifactInstanceRef[][] artifactData = draftController.BuildDraftArtifactData();

            if (teamLoader != null)
            {
                teamLoader.DeployLoadout(teamData, draftController.stagedActiveIndex, artifactData);
            }

            draftController.hasDraftChanges = false;
            RefreshGrid();
            RefreshActiveIndex();
        }

        private void RefreshGrid()
        {
            if (inventoryPanel == null || playerInventory == null || draftController == null) return;

            // Filter only CharacterItems from the player's inventory
            List<CharacterItem> ownedCharacters = new List<CharacterItem>();
            foreach (var item in playerInventory.ownedItems)
            {
                if (item is CharacterItem charItem)
                {
                    ownedCharacters.Add(charItem);
                }
            }

            foreach (CharacterItem runtimeItem in draftController.GetRuntimeCharacterItems())
            {
                if (runtimeItem == null || runtimeItem.characterObjectData == null)
                    continue;

                bool alreadyOwned = false;
                for (int i = 0; i < ownedCharacters.Count; i++)
                {
                    if (ownedCharacters[i] != null && ownedCharacters[i].characterObjectData == runtimeItem.characterObjectData)
                    {
                        alreadyOwned = true;
                        break;
                    }
                }

                if (!alreadyOwned)
                    ownedCharacters.Add(runtimeItem);
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.Populate(ownedCharacters, GetOwnedArtifacts());
            }

            ApplyModeVisualState();
        }

        private void HandleArtifactClicked(ArtifactInstance artifact)
        {
            if (artifactPreviewPanel != null)
            {
                if (artifact != null)
                {
                    artifactPreviewPanel.Open(artifact, playerInventory != null
                        ? playerInventory.ResolveArtifactDefinition(artifact.definitionId)
                        : null,
                        playerInventory != null ? playerInventory.ArtifactRollTable : null);
                    playerInventory?.TryMarkArtifactSeen(artifact.instanceId);

                }
                else
                {
                    artifactPreviewPanel.ShowEmpty();
                }
            }
        }

        private void HandleArtifactDoubleClicked(ArtifactInstance artifact)
        {
            TryEquipArtifact(artifact);
        }


        private void ApplyModeVisualState()
        {
            if (!HasConfiguredTeamSlots())
            {
                return;
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.SetVisible(currentMode == ZoneMode.SafeZone && isInventoryPanelOpen);
            }

            if (playerHolderButton != null)
            {
                playerHolderButton.SetActive(currentMode == ZoneMode.SafeZone);
            }

            bool isLocked = currentMode == ZoneMode.CombatZone;
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                {
                    teamSlots[i].SetLockedVisualState(isLocked);
                }
            }

            if (modeHandlers != null)
            {
                foreach (var handler in modeHandlers)
                {
                    handler.OnZoneChanged(currentMode);
                }
            }
        }

        private bool HasConfiguredTeamSlots()
        {
            if (teamSlots == null) return false;

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null) return true;
            }

            return false;
        }

        private bool IsPointerOutsideInventoryPanels()
        {
            if (inventoryPanel == null) return true;

            RectTransform gridRect = inventoryPanel.GetComponent<RectTransform>();
            if (IsPointerInsideRect(gridRect)) return false;

            if (playerHolderButton != null)
            {
                RectTransform holderRect = playerHolderButton.GetComponent<RectTransform>();
                if (IsPointerInsideRect(holderRect)) return false;

                foreach (Transform child in playerHolderButton.transform)
                {
                    if (child.gameObject.activeSelf)
                    {
                        RectTransform childRect = child.GetComponent<RectTransform>();
                        if (IsPointerInsideRect(childRect)) return false;
                    }
                }
            }

            return true;
        }

        private bool IsPointerInsideRect(RectTransform targetRect)
        {
            Vector2 pointerPosition = Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
            return IsScreenPointInsideRect(targetRect, pointerPosition);
        }

        private bool IsScreenPointInsideRect(RectTransform targetRect, Vector2 screenPoint)
        {
            if (targetRect == null) return false;

            Camera eventCamera = null;
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = rootCanvas.worldCamera;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(targetRect, screenPoint, eventCamera);
        }
    }
}
