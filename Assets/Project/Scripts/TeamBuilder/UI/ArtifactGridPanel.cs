using System.Collections.Generic;
using UnityEngine;
using Project.InventorySystem.Items;
using System;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Grid panel that displays all owned artifacts from the player's inventory.
    /// Spawns ArtifactGridItemUI cells and dims artifacts already assigned to team slots.
    /// </summary>
    public class ArtifactGridPanel : MonoBehaviour
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
        private List<ArtifactGridItemUI> spawnedItems = new List<ArtifactGridItemUI>();
        private TeamBuilderUI parentBuilder;

        public event Action<Artifact> OnArtifactClicked;

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
        /// Populates the grid with the given list of Artifacts.
        /// Artifacts already equipped in the team will be dimmed.
        /// </summary>
        public void PopulateGrid(List<Artifact> artifacts)
        {
            ClearGrid();

            if (artifacts == null) return;

            foreach (var artifact in artifacts)
            {
                if (artifact == null) continue;

                GameObject cellObj = Instantiate(gridItemPrefab, gridParent);
                ArtifactGridItemUI cellUI = cellObj.GetComponent<ArtifactGridItemUI>();

                if (cellUI != null)
                {
                    bool isEquipped = (parentBuilder != null) && parentBuilder.IsArtifactEquippedOnTeam(artifact);
                    cellUI.Init(artifact, rootCanvas, isEquipped);
                    cellUI.OnClicked += HandleArtifactClicked;
                    spawnedItems.Add(cellUI);
                }
            }
        }

        /// <summary>
        /// Refreshes the equipped state of all existing grid items without respawning.
        /// </summary>
        public void RefreshEquippedStates()
        {
            foreach (var item in spawnedItems)
            {
                if (item == null || item.ArtifactData == null) continue;
                bool isEquipped = (parentBuilder != null) && parentBuilder.IsArtifactEquippedOnTeam(item.ArtifactData);
                item.SetEquippedState(isEquipped);
            }
        }

        /// <summary>
        /// Clears all spawned grid items.
        /// </summary>
        public void ClearGrid()
        {
            foreach (var item in spawnedItems)
            {
                if (item != null)
                {
                    item.OnClicked -= HandleArtifactClicked;
                    Destroy(item.gameObject);
                }
            }
            spawnedItems.Clear();
        }

        private void HandleArtifactClicked(Artifact artifact)
        {
            OnArtifactClicked?.Invoke(artifact);
        }
    }
}
