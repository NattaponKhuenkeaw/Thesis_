using System.Collections.Generic;
using UnityEngine;
using Project.InventorySystem.Items;
using Project.TeamBuilder.UI;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// A unified container for various inventory grids (Characters, Artifacts, etc.)
    /// within the TeamBuilder UI. Handles visibility toggling to ensure only one
    /// inventory category is active at a time.
    /// </summary>
    public class InventoryBuilderPanel : MonoBehaviour
    {
        [Header("Sub-Panels")]
        [SerializeField] private CharacterGridInventoryUI characterGridPanel;
        [SerializeField] private ArtifactGridPanel artifactGridPanel;

        private TeamBuilderUI parentTeamBuilder;
        private Canvas rootCanvas;

        public bool IsCharacterGridOpen => characterGridPanel != null && characterGridPanel.gameObject.activeSelf;
        public bool IsArtifactGridOpen => artifactGridPanel != null && artifactGridPanel.gameObject.activeSelf;
        public bool IsAnyPanelOpen => IsCharacterGridOpen || IsArtifactGridOpen;


        public void Init(TeamBuilderUI teamBuilder, Canvas canvas)
        {
            parentTeamBuilder = teamBuilder;
            rootCanvas = canvas;

            if (characterGridPanel != null)
            {
                characterGridPanel.Init(teamBuilder, canvas);
            }

            if (artifactGridPanel != null)
            {
                artifactGridPanel.Init(teamBuilder, canvas);
            }

            CloseAll();
        }

        public void Populate(List<CharacterItem> characters, List<Artifact> artifacts)
        {
            if (characterGridPanel != null)
            {
                characterGridPanel.PopulateGrid(characters);
            }

            if (artifactGridPanel != null)
            {
                artifactGridPanel.PopulateGrid(artifacts);
            }
        }

        public void RefreshInTeamStates()
        {
            if (characterGridPanel != null)
            {
                characterGridPanel.RefreshInTeamStates();
            }

            if (artifactGridPanel != null)
            {
                artifactGridPanel.RefreshEquippedStates();
            }
        }

        public void SubscribeToArtifactClicks(System.Action<Artifact> callback)
        {
            if (artifactGridPanel != null)
            {
                artifactGridPanel.OnArtifactClicked -= callback;
                artifactGridPanel.OnArtifactClicked += callback;
            }
        }

        public void ToggleCharacterGrid()
        {
            if (characterGridPanel == null) return;

            bool wasOpen = characterGridPanel.gameObject.activeSelf;
            CloseAll();

            if (!wasOpen)
            {
                characterGridPanel.gameObject.SetActive(true);
            }
        }

        public void ToggleArtifactGrid()
        {
            if (artifactGridPanel == null) return;

            bool wasOpen = artifactGridPanel.gameObject.activeSelf;
            CloseAll();

            if (!wasOpen)
            {
                artifactGridPanel.gameObject.SetActive(true);
            }
        }

        public void CloseAll()
        {
            if (characterGridPanel != null) characterGridPanel.gameObject.SetActive(false);
            if (artifactGridPanel != null) artifactGridPanel.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            // If we are forcing visibility off, close all sub-panels
            if (!visible)
            {
                CloseAll();
            }
            gameObject.SetActive(visible);
        }
    }
}
