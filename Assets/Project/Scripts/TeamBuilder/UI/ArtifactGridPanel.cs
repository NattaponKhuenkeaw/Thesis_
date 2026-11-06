using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Project.Core.ZoneState;
using TMPro;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;

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
        [SerializeField] private ArtifactPreviewPanel previewPanel;

        [Header("Dependencies")]
        [SerializeField] private Canvas rootCanvas;

        [Header("Sorting / Filtering")]
        [SerializeField] private TMP_Dropdown sortDropdown;
        [SerializeField] private TMP_InputField searchInput;
        [FormerlySerializedAs("favouriteModeButton")]
        [SerializeField] private Button favouriteFilterButton;
        [FormerlySerializedAs("favouriteModeLabel")]
        [SerializeField] private TMP_Text favouriteFilterLabel;

        // =========================================================
        // Runtime State
        // =========================================================
        private List<ArtifactGridItemUI> spawnedItems = new List<ArtifactGridItemUI>();
        private readonly Stack<ArtifactGridItemUI> pooledItems = new Stack<ArtifactGridItemUI>();
        private readonly List<ArtifactInstance> queryResults = new List<ArtifactInstance>();
        private readonly HashSet<string> equippedIds = new HashSet<string>(StringComparer.Ordinal);
        private ArtifactInventoryQuery query = ArtifactInventoryQuery.Default;
        private IReadOnlyList<ArtifactInstance> sourceArtifacts;
        private TeamBuilderUI parentBuilder;
        private CharacterInventoryState playerInventory;
        private bool inventorySubscribed;
        private ArtifactCopyMenuUI activeMenu;
        private string selectedInstanceId;

        public void ClearSelection()
        {
            selectedInstanceId = null;
            if (previewPanel != null) previewPanel.Close();
            foreach (var item in spawnedItems) if (item != null) item.SetSelected(false);
        }

        public event Action<ArtifactInstance> OnArtifactClicked;
        public event Action<ArtifactInstance> OnArtifactDoubleClicked;

        // =========================================================
        // Unity Lifecycle
        // =========================================================
        private void OnEnable()
        {
            SubscribeInventory();
        }

        private void OnDisable()
        {
            ClearSelection();
            if (activeMenu != null) Destroy(activeMenu.gameObject);
            activeMenu = null;
            UnsubscribeInventory();
        }

        private void OnDestroy()
        {
            UnsubscribeInventory();
        }

        // =========================================================
        // Initialization
        // =========================================================
        public void Init(TeamBuilderUI builder, Canvas canvas, CharacterInventoryState inventory = null)
        {
            parentBuilder = builder;
            rootCanvas = canvas;
            playerInventory = inventory;
            SubscribeInventory();

            if (sortDropdown != null)
            {
                sortDropdown.ClearOptions();
                List<string> displayOptions = new List<string>();
                foreach (ArtifactSortMode mode in (ArtifactSortMode[])Enum.GetValues(typeof(ArtifactSortMode)))
                {
                    displayOptions.Add(GetSortModeDisplay(mode));
                }
                sortDropdown.AddOptions(displayOptions);
                sortDropdown.SetValueWithoutNotify((int)query.sortMode);
                sortDropdown.RefreshShownValue();
                sortDropdown.onValueChanged.RemoveListener(SetSortMode);
                sortDropdown.onValueChanged.AddListener(SetSortMode);
            }
            if (searchInput != null)
            {
                searchInput.onValueChanged.RemoveListener(SetSearchText);
                searchInput.onValueChanged.AddListener(SetSearchText);
            }
            if (favouriteFilterButton != null)
            {
                favouriteFilterButton.onClick.RemoveListener(CycleFavouriteFilter);
                favouriteFilterButton.gameObject.SetActive(false);
            }
            RefreshFavouriteFilterLabel();
        }

        // =========================================================
        // Public Methods
        // =========================================================

        /// <summary>
        /// Populates the grid with the given list of Artifacts.
        /// Artifacts already equipped in the team will be dimmed.
        /// </summary>
        public void PopulateGrid(IReadOnlyList<ArtifactInstance> artifacts)
        {
            sourceArtifacts = artifacts;
            RebuildQueryResults();
        }

        private void RebuildQueryResults()
        {
            ClearGrid();

            if (sourceArtifacts == null || playerInventory == null) return;

            equippedIds.Clear();
            if (parentBuilder != null)
            {
                parentBuilder.GetDraftEquippedArtifactInstanceIds(equippedIds);
            }
            else
            {
                playerInventory.GetEquippedArtifactInstanceIds(equippedIds);
            }

            ArtifactInventoryQueryEngine.Execute(
                sourceArtifacts,
                query,
                playerInventory.ArtifactRollTable,
                playerInventory.ResolveArtifactDefinition,
                equippedIds,
                queryResults);

            if (selectedInstanceId != null && !queryResults.Exists(item => item.instanceId == selectedInstanceId)) ClearSelection();

            foreach (ArtifactInstance artifact in queryResults)
            {
                if (artifact == null) continue;

                ArtifactDefinition definition = playerInventory.ResolveArtifactDefinition(artifact.definitionId);
                if (definition == null) continue;

                ArtifactGridItemUI cellUI = AcquireCell();

                if (cellUI != null)
                {
                    cellUI.transform.SetSiblingIndex(spawnedItems.Count);
                    bool isEquipped = equippedIds.Contains(artifact.instanceId);
                    cellUI.Init(artifact, definition, rootCanvas, playerInventory.ArtifactRollTable, isEquipped);
                    cellUI.SetSelected(artifact.instanceId == selectedInstanceId);
                    cellUI.OnClicked += HandleArtifactClicked;
                    cellUI.OnDoubleClicked += HandleArtifactDoubleClicked;
                    cellUI.OnContextRequested += OpenContextMenu;
                    spawnedItems.Add(cellUI);
                }
            }
        }

        /// <summary>
        /// Refreshes the equipped state of all existing grid items without respawning.
        /// </summary>
        public void RefreshEquippedStates()
        {
            equippedIds.Clear();
            if (parentBuilder != null)
            {
                parentBuilder.GetDraftEquippedArtifactInstanceIds(equippedIds);
            }
            else if (playerInventory != null)
            {
                playerInventory.GetEquippedArtifactInstanceIds(equippedIds);
            }

            foreach (var item in spawnedItems)
            {
                if (item == null || item.ArtifactData == null) continue;
                bool isEquipped = equippedIds.Contains(item.ArtifactData.instanceId);
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
                    item.OnDoubleClicked -= HandleArtifactDoubleClicked;
                    item.OnContextRequested -= OpenContextMenu;
                    item.gameObject.SetActive(false);
                    pooledItems.Push(item);
                }
            }
            spawnedItems.Clear();
        }

        public void SetSortMode(int sortMode)
        {
            query.sortMode = (ArtifactSortMode)Mathf.Clamp(
                sortMode,
                0,
                Enum.GetValues(typeof(ArtifactSortMode)).Length - 1);
            RebuildQueryResults();
        }

        public void ToggleSortDirection()
        {
            query.descending = !query.descending;
            RebuildQueryResults();
        }

        public void SetSearchText(string text)
        {
            query.searchText = text;
            RebuildQueryResults();
        }

        public void SetFavouriteFilter(int value)
        {
            query.favourite = (ArtifactOptionalBool)Mathf.Clamp(value, 0, 2);
            RefreshFavouriteFilterLabel();
            RebuildQueryResults();
        }

        public void SetLineCountFilter(int minimum, int maximum)
        {
            query.minimumLineCount = Mathf.Clamp(minimum, 0, 4);
            query.maximumLineCount = Mathf.Clamp(maximum, query.minimumLineCount, 4);
            RebuildQueryResults();
        }

        public void SetStatFilter(ulong statMask, bool matchAll)
        {
            query.requiredStatMask = statMask;
            query.matchAllStats = matchAll;
            RebuildQueryResults();
        }

        public void SetEquippedFilter(int value)
        {
            query.equipped = (ArtifactOptionalBool)Mathf.Clamp(value, 0, 2);
            RebuildQueryResults();
        }

        public void SetNewFilter(int value)
        {
            query.isNew = (ArtifactOptionalBool)Mathf.Clamp(value, 0, 2);
            RebuildQueryResults();
        }

        private void HandleArtifactClicked(ArtifactInstance artifact)
        {
            if (artifact != null && artifact.isNew)
                playerInventory?.TryMarkArtifactSeen(artifact.instanceId);

            OnArtifactClicked?.Invoke(artifact);
            selectedInstanceId = artifact != null ? artifact.instanceId : null;
            foreach (var item in spawnedItems)
                if (item != null) item.SetSelected(item.ArtifactData?.instanceId == selectedInstanceId);
        }

        private void HandleArtifactDoubleClicked(ArtifactInstance artifact)
        {
            if (artifact == null) return;
            OnArtifactDoubleClicked?.Invoke(artifact);
        }

        private static string GetSortModeDisplay(ArtifactSortMode mode)
        {
            return mode switch
            {
                ArtifactSortMode.Newest => "Newest",
                ArtifactSortMode.Oldest => "Oldest",
                ArtifactSortMode.AverageRollQuality => "Roll Quality",
                ArtifactSortMode.DefinitionName => "Name",
                ArtifactSortMode.StatLineCount => "Stat Lines",
                ArtifactSortMode.SelectedStatTotal => "Stat Total",
                ArtifactSortMode.FavouriteFirst => "Favourites",
                ArtifactSortMode.EquippedFirst => "Equipped",
                ArtifactSortMode.UnequippedFirst => "Unequipped",
                ArtifactSortMode.Rarity => "Rarity",
                _ => mode.ToString()
            };
        }

        private void CycleFavouriteFilter()
        {
            SetFavouriteFilter(((int)query.favourite + 1) % 3);
        }

        private void RefreshFavouriteFilterLabel()
        {
            if (favouriteFilterLabel != null)
                favouriteFilterLabel.text = $"Fav: {query.favourite}";
        }

        private void Update()
        {
            if (Gamepad.current?.buttonNorth.wasPressedThisFrame != true) return;
            ArtifactGridItemUI cell = EventSystem.current?.currentSelectedGameObject?.GetComponent<ArtifactGridItemUI>();
            if (cell == null || !spawnedItems.Contains(cell)) return;
            Camera camera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera : null;
            OpenContextMenu(cell, RectTransformUtility.WorldToScreenPoint(camera, cell.transform.position), camera);
        }

        private void OpenContextMenu(ArtifactGridItemUI cell, Vector2 screenPosition, Camera eventCamera)
        {
            if (activeMenu != null || rootCanvas == null || !CanManageCopies()) return;
            string instanceId = cell.ArtifactData?.instanceId;
            if (!playerInventory.TryGetArtifactInstance(instanceId, out ArtifactInstance instance)) return;
            var prefab = Resources.Load<ArtifactCopyMenuUI>(ArtifactCopyMenuUI.MenuPath);
            if (prefab == null) return;

            TrackMenu(Instantiate(prefab, rootCanvas.transform, false));
            activeMenu.OpenMenu(instance.isFavourite, CanDiscard(instanceId), screenPosition, eventCamera,
                () =>
                {
                    if (!CanManageCopies() || !playerInventory.TryGetArtifactInstance(instanceId, out ArtifactInstance current))
                        return;
                    playerInventory.TrySetArtifactFavourite(instanceId, !current.isFavourite);
                    RebuildQueryResults();
                },
                () => RequestDiscard(instanceId));
        }

        private void RequestDiscard(string instanceId)
        {
            if (!CanDiscard(instanceId)) return;
            if (ArtifactCopyMenuUI.SkipConfirmation)
            {
                Discard(instanceId);
                return;
            }

            if (!playerInventory.TryGetArtifactInstance(instanceId, out ArtifactInstance instance)) return;
            var prefab = Resources.Load<ArtifactCopyMenuUI>(ArtifactCopyMenuUI.DialogPath);
            if (prefab == null || rootCanvas == null) return;
            string displayName = playerInventory.ResolveArtifactDefinition(instance.definitionId)?.itemName ?? "this Artifact";
            TrackMenu(Instantiate(prefab, rootCanvas.transform, false));
            activeMenu.OpenDiscard(displayName, () => Discard(instanceId));
        }

        private void TrackMenu(ArtifactCopyMenuUI menu)
        {
            activeMenu = menu;
            menu.Closed += HandleMenuClosed;
        }

        private void HandleMenuClosed(ArtifactCopyMenuUI menu)
        {
            menu.Closed -= HandleMenuClosed;
            if (activeMenu == menu) activeMenu = null;
        }

        private bool CanManageCopies() => isActiveAndEnabled && playerInventory != null &&
            (parentBuilder == null || parentBuilder.CurrentMode == ZoneMode.SafeZone);

        private bool CanDiscard(string instanceId)
        {
            if (!CanManageCopies() || !playerInventory.TryGetArtifactInstance(instanceId, out _) ||
                playerInventory.IsArtifactEquipped(instanceId)) return false;
            return parentBuilder == null || !parentBuilder.GetDraftEquippedArtifactInstanceIds(equippedIds).Contains(instanceId);
        }

        private bool Discard(string instanceId)
        {
            if (!CanDiscard(instanceId)) return false;
            int formerIndex = spawnedItems.FindIndex(item => item.ArtifactData?.instanceId == instanceId);
            if (!playerInventory.TryRemoveArtifactInstance(instanceId)) return false;
            if (spawnedItems.Count > 0)
                EventSystem.current?.SetSelectedGameObject(spawnedItems[Mathf.Clamp(formerIndex, 0, spawnedItems.Count - 1)].gameObject);
            else
                EventSystem.current?.SetSelectedGameObject(sortDropdown != null ? sortDropdown.gameObject : null);
            return true;
        }

        private ArtifactGridItemUI AcquireCell()
        {
            ArtifactGridItemUI cell = pooledItems.Count > 0 ? pooledItems.Pop() : null;
            if (cell == null)
            {
                GameObject cellObject = Instantiate(gridItemPrefab, gridParent);
                cell = cellObject.GetComponent<ArtifactGridItemUI>();
            }
            else
            {
                cell.transform.SetParent(gridParent, false);
                cell.gameObject.SetActive(true);
            }
            cell.transform.SetAsLastSibling();
            return cell;
        }

        private void SubscribeInventory()
        {
            if (playerInventory == null)
            {
                playerInventory = parentBuilder != null ? parentBuilder.PlayerInventory : null;
                if (playerInventory == null)
                {
                    playerInventory = Project.Training.CharacterStateContext.ResolveInventory();
                }
            }

            if (inventorySubscribed || playerInventory == null) return;
            playerInventory.OnArtifactsChanged += HandleInventoryArtifactsChanged;
            playerInventory.OnArtifactFavouriteChanged += HandleInventoryArtifactFavouriteChanged;
            playerInventory.OnArtifactSeenChanged += HandleInventoryArtifactSeenChanged;
            inventorySubscribed = true;
        }

        private void UnsubscribeInventory()
        {
            if (!inventorySubscribed || playerInventory == null) return;
            playerInventory.OnArtifactsChanged -= HandleInventoryArtifactsChanged;
            playerInventory.OnArtifactFavouriteChanged -= HandleInventoryArtifactFavouriteChanged;
            playerInventory.OnArtifactSeenChanged -= HandleInventoryArtifactSeenChanged;
            inventorySubscribed = false;
        }

        private void HandleInventoryArtifactFavouriteChanged(string instanceId, bool favourite)
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i]?.ArtifactData?.instanceId == instanceId)
                {
                    spawnedItems[i].SetFavouriteState(favourite);
                    break;
                }
            }
        }

        private void HandleInventoryArtifactSeenChanged(string instanceId)
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i]?.ArtifactData?.instanceId == instanceId)
                {
                    spawnedItems[i].SetNewState(false);
                    break;
                }
            }
        }

        private void HandleInventoryArtifactsChanged()
        {
            if (this == null)
            {
                UnsubscribeInventory();
                return;
            }

            if (!isActiveAndEnabled || playerInventory == null) return;
            sourceArtifacts = playerInventory.ArtifactInstances;
            RebuildQueryResults();
        }
    }
}
