using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.UI;

namespace Project.TeamBuilder.UI
{
    public enum TeamBuilderMode
    {
        SafeZone, // Interactive (Team Builder)
        CombatZone // Read-Only (Normal Team Data)
    }

    /// <summary>
    /// Main controller for the Team Builder UI.
    /// Manages the Character Grid Inventory panel and Team Slot panel.
    /// Syncs team data to the TeamLoader for gameplay.
    /// </summary>
    public class TeamBuilderUI : MonoBehaviour
    {
        // =========================================================
        // Dependencies
        // =========================================================
        [Header("Dependencies")]
        [Tooltip("Player inventory that stores owned items (CharacterItems live here).")]
        [SerializeField] private PlayerShopInventory playerInventory;

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
        [SerializeField] private TeamSlotUI[] teamSlots = new TeamSlotUI[3];
        [SerializeField] private GameObject playerHolderButton;
        [SerializeField] private bool openGridPanelOnSafeZoneOpen = false;

        [Header("Deploy Confirmation")]
        [SerializeField] private GameObject deployConfirmationPanel;
        [SerializeField] private Button deployConfirmButton;
        [SerializeField] private Button deployCancelButton;
        [SerializeField] private TeamUI owningTeamUI;

        // =========================================================
        // Runtime State
        // =========================================================
        private bool isInitialized = false;
        private int panelOpenedFrame = -1;
        private int stagedActiveIndex = 0;
        private bool hasDraftChanges = false;
        private System.Action pendingCloseAfterDeploy;
        private bool isPanelSessionActive = false;
        private CharacterObject[] cachedTeamData;
        private Artifact[][] cachedArtifactsBySlot;
        private int cachedActiveIndex = 0;
        private readonly Dictionary<CharacterObject, CharacterItem> runtimeCharacterItems = new Dictionary<CharacterObject, CharacterItem>();
        public Canvas RootCanvas => rootCanvas;

        [Header("Mode Settings")]
        [SerializeField] private TeamBuilderMode currentMode = TeamBuilderMode.CombatZone;
        [SerializeField] private InspectScreenUI inspectScreenUI;
        [SerializeField] [Range(0f, 1f)] private float panelAudioDuckVolume = 0.35f;
        [SerializeField] private AudioLowPassFilter panelLowPassFilter;
        [SerializeField] [Range(10f, 22000f)] private float panelLowPassCutoff = 350f;
        private float previousTimeScale = 1f;
        private float previousAudioListenerVolume = 1f;
        private float previousLowPassCutoff = 22000f;
        private bool previousLowPassEnabled;
        public TeamBuilderMode CurrentMode => currentMode;
        public bool HasDraftChanges => hasDraftChanges;

        private void Update()
        {
            if (currentMode != TeamBuilderMode.SafeZone || (inventoryPanel == null || !inventoryPanel.IsCharacterGridOpen))
            {
                return;
            }

            if (Time.frameCount == panelOpenedFrame)
            {
                return;
            }

            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame && IsPointerOutsideCharacterGrid())
            {
                CloseCharacterGridPanel();
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
        // Initialization
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

            EnsureDeployConfirmationPanel();

            // Initialize the grid panels
            if (inventoryPanel != null)
            {
                inventoryPanel.Init(this, rootCanvas);
                inventoryPanel.SubscribeToArtifactClicks(HandleArtifactClicked);
            }

            // Initialize each team slot
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                    teamSlots[i].Init(this, i);
            }
        }

        // =========================================================
        // Public API
        // =========================================================

        /// <summary>
        /// Called by TeamUI when the panel finishes opening.
        /// Loads the current team and populates the character grid.
        /// Does NOT manage InputLock or panel activation — TeamUI handles that.
        /// </summary>
        public void OnPanelOpened()
        {
            EnsureInitialized();
            if (isPanelSessionActive)
            {
                RestoreCachedDraft();
            }
            else
            {
                isPanelSessionActive = true;
                LoadRuntimeDraft();
            }

            if (currentMode == TeamBuilderMode.SafeZone && openGridPanelOnSafeZoneOpen)
            {
                if (inventoryPanel != null) inventoryPanel.ToggleCharacterGrid();
                panelOpenedFrame = Time.frameCount;
            }
            RefreshGrid();
            RefreshActiveIndex();

            previousTimeScale = Time.timeScale;
            previousAudioListenerVolume = AudioListener.volume;
            Time.timeScale = 0f;
            ApplyPauseAudioState();

            ApplyModeVisualState();
        }

        /// <summary>
        /// Called by TeamUI just before the panel starts closing.
        /// Closing cancels un-deployed Team Builder edits and leaves runtime authority untouched.
        /// </summary>
        public void OnPanelClosed()
        {
            pendingCloseAfterDeploy = null;
            if (inventoryPanel != null) inventoryPanel.CloseAll();
            isPanelSessionActive = false;
            ClearDraftCache();
            HideDeployConfirmation();
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            RestorePauseAudioState();

            if (inspectScreenUI != null && inspectScreenUI.gameObject.activeSelf)
            {
                inspectScreenUI.Close();
            }

            if (artifactPreviewPanel != null && artifactPreviewPanel.IsOpen)
            {
                artifactPreviewPanel.Close();
            }

            ApplyModeVisualState();
        }

        public void SetMode(TeamBuilderMode mode)
        {
            currentMode = mode;
            if (currentMode == TeamBuilderMode.CombatZone && inventoryPanel != null)
            {
                inventoryPanel.CloseAll();
            }

            ApplyModeVisualState();
        }

        public void OpenCharacterGridPanel()
        {
            if (currentMode != TeamBuilderMode.SafeZone || inventoryPanel == null) return;
            if (!inventoryPanel.IsCharacterGridOpen) inventoryPanel.ToggleCharacterGrid();
            panelOpenedFrame = Time.frameCount;
            RefreshGrid();
            ApplyModeVisualState();
        }

        public void CloseCharacterGridPanel()
        {
            if (inventoryPanel != null && inventoryPanel.IsCharacterGridOpen)
            {
                inventoryPanel.CloseAll();
            }
            ApplyModeVisualState();
        }

        public void ToggleCharacterGridPanel()
        {
            if (currentMode != TeamBuilderMode.SafeZone)
            {
                if (inventoryPanel != null) inventoryPanel.CloseAll();
                ApplyModeVisualState();
                return;
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.ToggleCharacterGrid();
                if (inventoryPanel.IsCharacterGridOpen)
                {
                    panelOpenedFrame = Time.frameCount;
                    RefreshGrid();
                }
            }
            ApplyModeVisualState();
        }

        public void ToggleArtifactGridPanel()
        {
            if (currentMode != TeamBuilderMode.SafeZone)
            {
                if (inventoryPanel != null) inventoryPanel.CloseAll();
                ApplyModeVisualState();
                return;
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.ToggleArtifactGrid();
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
            ApplyModeVisualState();
        }

        /// <summary>
        /// Checks if a character is currently placed in any team slot.
        /// </summary>
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

        /// <summary>
        /// Finds the TeamSlotUI that currently holds the given character.
        /// Returns null if the character is not in any slot.
        /// </summary>
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

        public bool IsArtifactEquippedOnTeam(Artifact artifact)
        {
            return FindSlotWithArtifact(artifact) != null;
        }

        public TeamSlotUI FindSlotWithArtifact(Artifact artifact)
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

        /// <summary>
        /// Called by TeamSlotUI whenever a slot changes (character placed/swapped/removed).
        /// Refreshes the local draft only. Runtime TeamLoader is updated only by Deploy.
        /// </summary>
        public void OnSlotChanged()
        {
            hasDraftChanges = true;

            // Refresh grid item dim states
            if (inventoryPanel != null)
                inventoryPanel.RefreshInTeamStates();

            // Refresh active index highlighting
            NormalizeStagedActiveIndex();
            RefreshActiveIndex();
            SaveDraftCache();
        }

        public void OnDeployClicked()
        {
            if (currentMode != TeamBuilderMode.SafeZone)
            {
                return;
            }

            EnsureInitialized();
            ShowDeployConfirmation();
        }

        public void OnDeployConfirmed()
        {
            SaveDraftCache();
            HideDeployConfirmation();
            DeployDraftToRuntime();

            System.Action closeAfterDeploy = pendingCloseAfterDeploy;
            pendingCloseAfterDeploy = null;

            if (closeAfterDeploy != null)
            {
                closeAfterDeploy.Invoke();
            }
            else if (owningTeamUI != null)
            {
                owningTeamUI.Close();
            }
        }

        public void OnDeployCancelled()
        {
            System.Action closeAfterCancel = pendingCloseAfterDeploy;
            pendingCloseAfterDeploy = null;
            HideDeployConfirmation();

            if (closeAfterCancel != null)
            {
                closeAfterCancel.Invoke();
            }
            else if (owningTeamUI != null)
            {
                owningTeamUI.Close();
            }
        }

        public void OnCancelClicked()
        {
            pendingCloseAfterDeploy = null;
            HideDeployConfirmation();
            LoadRuntimeDraft();
            RefreshGrid();
            RefreshActiveIndex();
            ApplyModeVisualState();
        }

        public void OpenInspectScreen(CharacterItem character)
        {
            if (inspectScreenUI != null && character != null)
            {
                SaveDraftCache();
                inspectScreenUI.Open(character);
            }
        }

        public void OpenInspectScreen(CharacterItem character, Artifact[] equippedArtifacts)
        {
            if (inspectScreenUI != null && character != null)
            {
                SaveDraftCache();
                inspectScreenUI.Open(character, equippedArtifacts);
            }
        }

        public bool TryResolveArtifactSelection(Artifact currentArtifact, int direction, out Artifact resolvedArtifact)
        {
            resolvedArtifact = currentArtifact;

            List<Artifact> ownedArtifacts = GetOwnedArtifacts();
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

        public List<Artifact> GetOwnedArtifacts()
        {
            List<Artifact> ownedArtifacts = new List<Artifact>();
            if (playerInventory == null || playerInventory.ownedItems == null)
            {
                return ownedArtifacts;
            }

            foreach (BaseItem item in playerInventory.ownedItems)
            {
                if (item is Artifact artifact && artifact != null && artifact.isEquippable && !ownedArtifacts.Contains(artifact))
                {
                    ownedArtifacts.Add(artifact);
                }
            }

            return ownedArtifacts;
        }

        /// <summary>
        /// Sets the active character index in the staged Team Builder draft.
        /// </summary>
        public void SetActiveIndex(int index)
        {
            if (index < 0 || index >= teamSlots.Length)
                return;

            if (teamSlots[index] == null || teamSlots[index].CurrentCharacter == null)
                return;

            if (stagedActiveIndex == index)
                return;

            stagedActiveIndex = index;
            hasDraftChanges = true;
            RefreshActiveIndex();
            SaveDraftCache();
        }

        /// <summary>
        /// Updates the visual highlight for the currently active character index.
        /// </summary>
        public void RefreshActiveIndex()
        {
            NormalizeStagedActiveIndex();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                {
                    teamSlots[i].SetActiveState(i == stagedActiveIndex);
                }
            }
        }

        /// <summary>
        /// Removes a character from its team slot (e.g. when dragged out of the panel area).
        /// </summary>
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
            if (currentMode != TeamBuilderMode.SafeZone)
            {
                return false;
            }

            EnsureInitialized();
            SaveDraftCache();
            pendingCloseAfterDeploy = closeAfterSave;
            ShowDeployConfirmation();
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

        private bool HasConfiguredTeamSlots()
        {
            if (teamSlots == null)
            {
                return false;
            }

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

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

        // =========================================================
        // Private Methods
        // =========================================================

        /// <summary>
        /// Loads the live runtime TeamLoader into the Team Builder draft.
        /// </summary>
        private void LoadRuntimeDraft()
        {
            if (playerInventory == null) return;

            CharacterObject[] existingTeam = teamLoader != null ? teamLoader.team : null;

            if (existingTeam == null) return;

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                    teamSlots[i].ClearSlot();
            }

            stagedActiveIndex = teamLoader != null ? teamLoader.CurrentIndex : 0;

            for (int i = 0; i < teamSlots.Length && i < existingTeam.Length; i++)
            {
                if (teamSlots[i] == null) continue;

                CharacterObject charObj = existingTeam[i];
                if (charObj == null)
                {
                    teamSlots[i].ClearSlot();
                    continue;
                }

                // Find matching character item in inventory
                CharacterItem matchingItem = FindCharacterItem(charObj);
                teamSlots[i].SetCharacter(matchingItem);

                if (teamLoader != null)
                {
                    Artifact[] runtimeArtifacts = teamLoader.GetEquippedArtifactsAt(i);
                    teamSlots[i].SetEquippedArtifacts(CloneArtifactArray(runtimeArtifacts));
                }
            }

            NormalizeStagedActiveIndex();
            hasDraftChanges = false;
            SaveDraftCache();
        }

        private void RestoreCachedDraft()
        {
            if (cachedTeamData == null)
            {
                LoadRuntimeDraft();
                return;
            }

            LoadDraftSnapshot(cachedTeamData, cachedArtifactsBySlot, cachedActiveIndex);
        }

        private void LoadDraftSnapshot(CharacterObject[] teamData, Artifact[][] artifactsBySlot, int activeIndex)
        {
            if (playerInventory == null || teamData == null) return;

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                    teamSlots[i].ClearSlot();
            }

            stagedActiveIndex = activeIndex;

            for (int i = 0; i < teamSlots.Length && i < teamData.Length; i++)
            {
                if (teamSlots[i] == null) continue;

                CharacterObject charObj = teamData[i];
                if (charObj == null)
                {
                    teamSlots[i].ClearSlot();
                    continue;
                }

                CharacterItem matchingItem = FindCharacterItem(charObj);
                teamSlots[i].SetCharacter(matchingItem);

                if (artifactsBySlot != null && i < artifactsBySlot.Length)
                {
                    teamSlots[i].SetEquippedArtifacts(CloneArtifactArray(artifactsBySlot[i]));
                }
            }

            NormalizeStagedActiveIndex();
        }

        private void SaveDraftCache()
        {
            cachedTeamData = BuildDraftTeamData();
            cachedArtifactsBySlot = BuildDraftArtifactData();
            cachedActiveIndex = stagedActiveIndex;
        }

        private void ClearDraftCache()
        {
            cachedTeamData = null;
            cachedArtifactsBySlot = null;
            cachedActiveIndex = 0;
            hasDraftChanges = false;
        }

        /// <summary>
        /// Helper to find a CharacterItem in the inventory by its CharacterObject reference.
        /// </summary>
        private CharacterItem FindCharacterItem(CharacterObject charObj)
        {
            if (charObj == null) return null;

            // First, try to find a matching CharacterItem in the player inventory
            if (playerInventory != null)
            {
                foreach (var item in playerInventory.ownedItems)
                {
                    if (item is CharacterItem charItem && charItem.characterObjectData == charObj)
                    {
                        return charItem;
                    }
                }
            }

            // No matching item in inventory — create a runtime wrapper
            // so the TeamBuilder UI can still display this character
            if (runtimeCharacterItems.TryGetValue(charObj, out CharacterItem cachedItem) && cachedItem != null)
                return cachedItem;

            CharacterItem runtimeItem = ScriptableObject.CreateInstance<CharacterItem>();
            runtimeItem.characterObjectData = charObj;
            runtimeItem.SyncFromCharacterObjectData();
            runtimeCharacterItems[charObj] = runtimeItem;
            return runtimeItem;
        }

        /// <summary>
        /// Reads owned characters from the player inventory and populates the grid.
        /// </summary>
        private void RefreshGrid()
        {
            if (inventoryPanel == null || playerInventory == null) return;

            // Filter only CharacterItems from the player's inventory
            List<CharacterItem> ownedCharacters = new List<CharacterItem>();
            foreach (var item in playerInventory.ownedItems)
            {
                if (item is CharacterItem charItem)
                {
                    ownedCharacters.Add(charItem);
                }
            }

            foreach (CharacterItem runtimeItem in runtimeCharacterItems.Values)
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

        private void HandleArtifactClicked(Artifact artifact)
        {
            if (artifactPreviewPanel != null && artifact != null)
            {
                artifactPreviewPanel.Open(artifact);
            }
        }

        private void ApplyModeVisualState()
        {
            if (!HasConfiguredTeamSlots())
            {
                return;
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.SetVisible(currentMode == TeamBuilderMode.SafeZone);
            }

            if (playerHolderButton != null)
            {
                playerHolderButton.SetActive(currentMode == TeamBuilderMode.SafeZone);
            }

            bool isLocked = currentMode == TeamBuilderMode.CombatZone;
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                {
                    teamSlots[i].SetLockedVisualState(isLocked);
                }
            }
        }

        private void DeployDraftToRuntime()
        {
            CharacterObject[] teamData = BuildDraftTeamData();
            Artifact[][] artifactData = BuildDraftArtifactData();
            NormalizeStagedActiveIndex();

            if (teamLoader != null)
            {
                teamLoader.DeployLoadout(teamData, stagedActiveIndex, artifactData);
            }

            hasDraftChanges = false;
            RefreshGrid();
            RefreshActiveIndex();
        }

        private CharacterObject[] BuildDraftTeamData()
        {
            CharacterObject[] teamData = new CharacterObject[teamSlots.Length];
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null && teamSlots[i].CurrentCharacter != null)
                    teamData[i] = teamSlots[i].CurrentCharacter.characterObjectData;
                else
                    teamData[i] = null;
            }

            return teamData;
        }

        private Artifact[][] BuildDraftArtifactData()
        {
            Artifact[][] artifactsBySlot = new Artifact[teamSlots.Length][];
            for (int i = 0; i < teamSlots.Length; i++)
            {
                artifactsBySlot[i] = teamSlots[i] != null
                    ? teamSlots[i].GetEquippedArtifactsSnapshot()
                    : null;
            }

            return artifactsBySlot;
        }

        private static Artifact[] CloneArtifactArray(Artifact[] source)
        {
            if (source == null)
            {
                return null;
            }

            Artifact[] clone = new Artifact[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = source[i];
            }

            return clone;
        }

        private void NormalizeStagedActiveIndex()
        {
            if (teamSlots == null || teamSlots.Length == 0)
            {
                stagedActiveIndex = 0;
                return;
            }

            if (stagedActiveIndex >= 0
                && stagedActiveIndex < teamSlots.Length
                && teamSlots[stagedActiveIndex] != null
                && teamSlots[stagedActiveIndex].CurrentCharacter != null)
            {
                return;
            }

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null && teamSlots[i].CurrentCharacter != null)
                {
                    stagedActiveIndex = i;
                    return;
                }
            }

            stagedActiveIndex = 0;
        }

        private void EnsureDeployConfirmationPanel()
        {
            if (deployConfirmationPanel == null)
            {
                deployConfirmationPanel = CreateDeployConfirmationPanel();
            }

            if (deployConfirmButton != null)
            {
                deployConfirmButton.onClick.RemoveListener(OnDeployConfirmed);
                deployConfirmButton.onClick.AddListener(OnDeployConfirmed);
            }

            if (deployCancelButton != null)
            {
                deployCancelButton.onClick.RemoveListener(OnDeployCancelled);
                deployCancelButton.onClick.AddListener(OnDeployCancelled);
            }

            HideDeployConfirmation();
        }

        private GameObject CreateDeployConfirmationPanel()
        {
            Transform parent = rootCanvas != null ? rootCanvas.transform : transform;

            GameObject overlay = new GameObject("Deploy Confirmation Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            overlay.transform.SetParent(parent, false);

            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.62f);

            GameObject card = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(overlay.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(460f, 220f);
            cardRect.anchoredPosition = Vector2.zero;

            Image cardImage = card.GetComponent<Image>();
            cardImage.color = new Color(0.07f, 0.08f, 0.1f, 0.96f);

            CreateDialogText(card.transform, "Title", "Save Loadout?", 26f, FontStyles.Bold, new Vector2(0f, 62f));
            CreateDialogText(
                card.transform,
                "Message",
                "This saves the Team Builder profile, refreshes TeamLoader runtime when available, then closes the Team UI.",
                16f,
                FontStyles.Normal,
                new Vector2(0f, 10f));

            deployConfirmButton = CreateDialogButton(card.transform, "Confirm Button", "Save", new Vector2(-90f, -68f), new Color(0.2f, 0.75f, 0.48f, 1f));
            deployCancelButton = CreateDialogButton(card.transform, "Cancel Button", "Cancel", new Vector2(90f, -68f), new Color(0.28f, 0.3f, 0.34f, 1f));

            overlay.SetActive(false);
            return overlay;
        }

        private static TextMeshProUGUI CreateDialogText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles fontStyle,
            Vector2 anchoredPosition)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(380f, fontSize > 20f ? 38f : 72f);
            rectTransform.anchoredPosition = anchoredPosition;

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static Button CreateDialogButton(
            Transform parent,
            string name,
            string text,
            Vector2 anchoredPosition,
            Color color)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(136f, 44f);
            buttonRect.anchoredPosition = anchoredPosition;

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = color;

            Button button = buttonObject.GetComponent<Button>();
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 17f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            return button;
        }

        private void ShowDeployConfirmation()
        {
            if (deployConfirmationPanel != null)
            {
                deployConfirmationPanel.SetActive(true);
            }
        }

        private void HideDeployConfirmation()
        {
            if (deployConfirmationPanel != null)
            {
                deployConfirmationPanel.SetActive(false);
            }
        }

        private bool IsPointerOutsideCharacterGrid()
        {
            if (inventoryPanel == null)
            {
                return true;
            }

            RectTransform gridRect = inventoryPanel.GetComponent<RectTransform>();
            if (IsPointerInsideRect(gridRect))
            {
                return false;
            }

            if (playerHolderButton != null)
            {
                RectTransform holderRect = playerHolderButton.GetComponent<RectTransform>();
                if (IsPointerInsideRect(holderRect))
                {
                    return false;
                }

                // Check all active children since button components might be offset outside the holder's bounds
                foreach (Transform child in playerHolderButton.transform)
                {
                    if (child.gameObject.activeSelf)
                    {
                        RectTransform childRect = child.GetComponent<RectTransform>();
                        if (IsPointerInsideRect(childRect))
                        {
                            return false;
                        }
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
            if (targetRect == null)
            {
                return false;
            }

            Camera eventCamera = null;
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = rootCanvas.worldCamera;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(targetRect, screenPoint, eventCamera);
        }

        private void ApplyPauseAudioState()
        {
            if (panelLowPassFilter != null)
            {
                previousLowPassEnabled = panelLowPassFilter.enabled;
                previousLowPassCutoff = panelLowPassFilter.cutoffFrequency;
                panelLowPassFilter.enabled = true;
                panelLowPassFilter.cutoffFrequency = panelLowPassCutoff;
                return;
            }

            AudioListener.volume = Mathf.Clamp01(previousAudioListenerVolume * panelAudioDuckVolume);
        }

        private void RestorePauseAudioState()
        {
            if (panelLowPassFilter != null)
            {
                panelLowPassFilter.cutoffFrequency = previousLowPassCutoff;
                panelLowPassFilter.enabled = previousLowPassEnabled;
            }

            AudioListener.volume = previousAudioListenerVolume;
        }
    }
}
