using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;

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
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text contextText;
        [SerializeField] private Button characterTab;
        [SerializeField] private Button artifactTab;
        [SerializeField] private Button closeButton;
        [SerializeField] private Sprite activeTabSprite;
        [SerializeField] private Sprite inactiveTabSprite;
        [SerializeField] private TeamBuilderInventoryMotion visibilityMotion;

        private TeamBuilderUI parentTeamBuilder;
        private Canvas rootCanvas;

        public bool IsCharacterGridOpen => characterGridPanel != null && characterGridPanel.gameObject.activeSelf;
        public bool IsArtifactGridOpen => artifactGridPanel != null && artifactGridPanel.gameObject.activeSelf;
        public bool IsAnyPanelOpen => IsCharacterGridOpen || IsArtifactGridOpen;

        public void Init(TeamBuilderUI teamBuilder, Canvas canvas)
        {
            parentTeamBuilder = teamBuilder;
            rootCanvas = canvas;
            if (characterTab != null)
            {
                characterTab.onClick.RemoveListener(OpenCharacterCategory);
                characterTab.onClick.AddListener(OpenCharacterCategory);
            }
            if (artifactTab != null)
            {
                artifactTab.onClick.RemoveListener(OpenArtifactCategory);
                artifactTab.onClick.AddListener(OpenArtifactCategory);
            }
            if (closeButton != null && teamBuilder != null)
            {
                closeButton.onClick.RemoveListener(teamBuilder.CloseInventoryPanel);
                closeButton.onClick.AddListener(teamBuilder.CloseInventoryPanel);
            }

            if (characterGridPanel != null)
            {
                characterGridPanel.Init(teamBuilder, canvas);
            }

            if (artifactGridPanel != null)
            {
                artifactGridPanel.Init(teamBuilder, canvas, teamBuilder != null ? teamBuilder.PlayerInventory : null);
            }

            CloseAll();
        }

        public void ClearSelection()
        {
            characterGridPanel?.ClearSelection();
            artifactGridPanel?.ClearSelection();
        }

        public void SelectArtifact(ArtifactInstance artifact)
        {
            artifactGridPanel?.SelectArtifact(artifact);
        }

        public void SetContext(string context)
        {
            if (contextText != null) contextText.text = context;
            if (titleText != null) titleText.text = IsCharacterGridOpen ? "SELECT CHARACTER" : "SELECT ARTIFACT";
            SetTabColor(characterTab, IsCharacterGridOpen);
            SetTabColor(artifactTab, IsArtifactGridOpen);
        }

        private void SetTabColor(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            image.sprite = selected ? activeTabSprite : inactiveTabSprite;
            image.color = Color.white;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = selected ? new Color(.04f, .05f, .08f) : Color.white;
        }

        private void OpenCharacterCategory()
        {
            var slot = parentTeamBuilder.GetActiveOrFirstTeamSlot();
            if (!IsCharacterGridOpen && slot != null) parentTeamBuilder.BeginCharacterSwitch(slot.SlotIndex);
        }

        private void OpenArtifactCategory()
        {
            if (!IsArtifactGridOpen) parentTeamBuilder.ToggleArtifactGridPanel();
        }

        public void Populate(List<CharacterItem> characters, IReadOnlyList<ArtifactInstance> artifacts)
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

        public void SubscribeToArtifactClicks(Action<ArtifactInstance> callback)
        {
            if (artifactGridPanel != null)
            {
                artifactGridPanel.OnArtifactClicked -= callback;
                artifactGridPanel.OnArtifactClicked += callback;
            }
        }

        public void SubscribeToArtifactDoubleClicks(Action<ArtifactInstance> callback)
        {
            if (artifactGridPanel != null)
            {
                artifactGridPanel.OnArtifactDoubleClicked -= callback;
                artifactGridPanel.OnArtifactDoubleClicked += callback;
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
            ClearSelection();
            if (characterGridPanel != null) characterGridPanel.gameObject.SetActive(false);
            if (artifactGridPanel != null) artifactGridPanel.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible, bool animated = true)
        {
            if (!visible) ClearSelection();
            if (visibilityMotion != null)
            {
                visibilityMotion.SetVisible(visible, CloseAll, animated);
                return;
            }
            if (!visible) CloseAll();
            gameObject.SetActive(visible);
        }
    }
}
