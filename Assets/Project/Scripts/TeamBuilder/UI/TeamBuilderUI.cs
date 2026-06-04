using System.Collections.Generic;
using UnityEngine;
using Project.InventorySystem.Items;
using Project.ShopSystem;

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
        [SerializeField] private CharacterGridInventoryUI gridPanel;
        [SerializeField] private TeamSlotUI[] teamSlots = new TeamSlotUI[3];

        // =========================================================
        // Runtime State
        // =========================================================
        private bool isInitialized = false;
        private readonly Dictionary<CharacterObject, CharacterItem> runtimeCharacterItems = new Dictionary<CharacterObject, CharacterItem>();
        public Canvas RootCanvas => rootCanvas;

        [Header("Mode Settings")]
        public TeamBuilderMode currentMode = TeamBuilderMode.SafeZone;
        [SerializeField] private InspectScreenUI inspectScreenUI;
        private float previousTimeScale = 1f;

        // =========================================================
        // Initialization
        // =========================================================
        private void EnsureInitialized()
        {
            if (isInitialized) return;
            isInitialized = true;

            // Fallback for rootCanvas if not assigned in inspector
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            }

            // Initialize the grid panel
            if (gridPanel != null)
                gridPanel.Init(this, rootCanvas);

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
            LoadExistingTeam();
            RefreshGrid();
            RefreshActiveIndex();

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            if (currentMode == TeamBuilderMode.CombatZone)
            {
                if (gridPanel != null) gridPanel.gameObject.SetActive(false);
            }
            else
            {
                if (gridPanel != null) gridPanel.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Called by TeamUI just before the panel starts closing.
        /// Syncs the current slot data back to the TeamLoader.
        /// </summary>
        public void OnPanelClosed()
        {
            SyncTeamToLoader();
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

            if (inspectScreenUI != null && inspectScreenUI.gameObject.activeSelf)
            {
                inspectScreenUI.Close();
            }
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

        /// <summary>
        /// Called by TeamSlotUI whenever a slot changes (character placed/swapped/removed).
        /// Refreshes grid to update dimming and syncs to TeamLoader.
        /// </summary>
        public void OnSlotChanged()
        {
            // Refresh grid item dim states
            if (gridPanel != null)
                gridPanel.RefreshInTeamStates();

            // Refresh active index highlighting
            RefreshActiveIndex();

            // Live-sync team data to TeamLoader
            SyncTeamToLoader();
        }

        public void OpenInspectScreen(CharacterItem character)
        {
            if (inspectScreenUI != null && character != null)
            {
                inspectScreenUI.Open(character);
            }
        }

        /// <summary>
        /// Sets the active character index in the TeamLoader and refreshes the UI.
        /// </summary>
        public void SetActiveIndex(int index)
        {
            if (teamLoader == null) return;

            if (teamLoader.SwitchToIndex(index))
                RefreshActiveIndex();
        }

        /// <summary>
        /// Updates the visual highlight for the currently active character index.
        /// </summary>
        public void RefreshActiveIndex()
        {
            if (teamLoader == null) return;

            int activeIdx = teamLoader.CurrentIndex;
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                {
                    teamSlots[i].SetActiveState(i == activeIdx);
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
        /// Loads the current team from TeamLoader and populates the UI slots.
        /// Maps CharacterObject back to CharacterItem from player inventory.
        /// </summary>
        private void LoadExistingTeam()
        {
            if (teamLoader == null || playerInventory == null) return;

            CharacterObject[] existingTeam = teamLoader.team;
            if (existingTeam == null) return;

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                    teamSlots[i].ClearSlot();
            }

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
            }
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
            if (gridPanel == null || playerInventory == null) return;

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

            gridPanel.PopulateGrid(ownedCharacters);
        }

        /// <summary>
        /// Pushes the current slot data into the TeamLoader's team array.
        /// </summary>
        private void SyncTeamToLoader()
        {
            if (teamLoader == null) return;

            CharacterObject[] teamData = new CharacterObject[teamSlots.Length];
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null && teamSlots[i].CurrentCharacter != null)
                    teamData[i] = teamSlots[i].CurrentCharacter.characterObjectData;
                else
                    teamData[i] = null;
            }

            teamLoader.SyncWithTeam(teamData);
        }
    }
}
