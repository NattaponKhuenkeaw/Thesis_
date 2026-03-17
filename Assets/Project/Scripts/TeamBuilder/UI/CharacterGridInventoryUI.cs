using System.Collections.Generic;
using UnityEngine;
using Project.InventorySystem.Items;
using Project.ShopSystem;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Grid panel that displays all owned characters from the player's inventory.
    /// Spawns CharacterGridItemUI cells and dims characters already assigned to team slots.
    /// </summary>
    public class CharacterGridInventoryUI : MonoBehaviour
    {
        // =========================================================
        // References
        // =========================================================
        [Header("Grid Setup")]
        [SerializeField] private Transform gridParent;
        [SerializeField] private GameObject gridItemPrefab;

        [Header("Dependencies")]
        [SerializeField] private Canvas rootCanvas;

        // =========================================================
        // Runtime State
        // =========================================================
        private List<CharacterGridItemUI> spawnedItems = new List<CharacterGridItemUI>();
        private TeamBuilderUI parentBuilder;

        // =========================================================
        // Initialization
        // =========================================================
        public void Init(TeamBuilderUI builder, Canvas canvas)
        {
            parentBuilder = builder;
            rootCanvas = canvas;
        }

        // =========================================================
        // Public Methods
        // =========================================================

        /// <summary>
        /// Populates the grid with the given list of CharacterItems.
        /// Characters already in the team will be dimmed.
        /// </summary>
        public void PopulateGrid(List<CharacterItem> characters)
        {
            ClearGrid();

            if (characters == null) return;

            foreach (var character in characters)
            {
                if (character == null) continue;

                GameObject cellObj = Instantiate(gridItemPrefab, gridParent);
                CharacterGridItemUI cellUI = cellObj.GetComponent<CharacterGridItemUI>();

                if (cellUI != null)
                {
                    bool isInTeam = (parentBuilder != null) && parentBuilder.IsCharacterInTeam(character);
                    cellUI.Init(character, rootCanvas, isInTeam);
                    spawnedItems.Add(cellUI);
                }
            }
        }

        /// <summary>
        /// Refreshes the in-team state of all existing grid items without respawning.
        /// </summary>
        public void RefreshInTeamStates()
        {
            foreach (var item in spawnedItems)
            {
                if (item == null) continue;
                bool isInTeam = (parentBuilder != null) && parentBuilder.IsCharacterInTeam(item.CharacterData);
                item.SetInTeamState(isInTeam);
            }
        }

        /// <summary>
        /// Clears all spawned grid items.
        /// </summary>
        public void ClearGrid()
        {
            foreach (var item in spawnedItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            spawnedItems.Clear();
        }
    }
}
