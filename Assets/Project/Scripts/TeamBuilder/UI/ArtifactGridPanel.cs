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
        [SerializeField] private Button favouriteModeButton;
        [SerializeField] private TMP_Text favouriteModeLabel;

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
        private bool favouriteMode;
        private bool inventorySubscribed;
        private TMP_Text runtimeSortLabel;
        private TMP_Text runtimeOrderLabel;
        private TMP_Text runtimeFilterLabel;
        private TMP_Text runtimeLineLabel;
        private TMP_Text runtimeStatLabel;
        private TMP_Text runtimeEquippedLabel;
        private TMP_Text runtimeNewLabel;
        private int runtimeLineFilter;
        private int runtimeStatFilter;
        private string selectedInstanceId;

        public void ClearSelection()
        {
            selectedInstanceId = null;
            if (previewPanel != null) previewPanel.Close();
            foreach (var item in spawnedItems) if (item != null) item.SetSelected(false);
        }

        public event Action<ArtifactInstance> OnArtifactClicked;
        public event Action<ArtifactInstance> OnArtifactDoubleClicked;
        public bool IsFavouriteMode => favouriteMode;

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
            ExitFavouriteMode();
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
            EnsureRuntimeControls();

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
            if (favouriteModeButton != null)
            {
                favouriteModeButton.onClick.RemoveListener(ToggleFavouriteMode);
                favouriteModeButton.onClick.AddListener(ToggleFavouriteMode);
            }
            RefreshFavouriteModeVisual();
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
                    item.gameObject.SetActive(false);
                    pooledItems.Push(item);
                }
            }
            spawnedItems.Clear();
        }

        public void ToggleFavouriteMode()
        {
            favouriteMode = !favouriteMode;
            RefreshFavouriteModeVisual();
            if (!favouriteMode)
            {
                RebuildQueryResults();
            }
        }

        public void ExitFavouriteMode()
        {
            if (!favouriteMode) return;
            favouriteMode = false;
            RefreshFavouriteModeVisual();
            RebuildQueryResults();
        }

        public void SetSortMode(int sortMode)
        {
            query.sortMode = (ArtifactSortMode)Mathf.Clamp(
                sortMode,
                0,
                Enum.GetValues(typeof(ArtifactSortMode)).Length - 1);
            if (runtimeSortLabel != null) runtimeSortLabel.text = $"Sort: {GetSortModeDisplay(query.sortMode)}";
            RebuildQueryResults();
        }

        public void ToggleSortDirection()
        {
            query.descending = !query.descending;
            if (runtimeOrderLabel != null)
            {
                runtimeOrderLabel.text = query.descending ? "▾ Desc" : "▴ Asc";
            }
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
            if (favouriteMode && playerInventory != null && artifact != null)
            {
                bool next = !artifact.isFavourite;
                if (playerInventory.TrySetArtifactFavourite(artifact.instanceId, next))
                {
                    for (int i = 0; i < spawnedItems.Count; i++)
                    {
                        if (spawnedItems[i]?.ArtifactData?.instanceId == artifact.instanceId)
                        {
                            spawnedItems[i].SetFavouriteState(next);
                            break;
                        }
                    }
                }
                return;
            }

            if (artifact != null && artifact.isNew)
            {
                for (int i = 0; i < spawnedItems.Count; i++)
                {
                    if (spawnedItems[i]?.ArtifactData?.instanceId == artifact.instanceId)
                    {
                        spawnedItems[i].SetNewState(false);
                        break;
                    }
                }
            }

            OnArtifactClicked?.Invoke(artifact);
            selectedInstanceId = artifact != null ? artifact.instanceId : null;
            foreach (var item in spawnedItems)
                if (item != null) item.SetSelected(item.ArtifactData?.instanceId == selectedInstanceId);
        }

        private void HandleArtifactDoubleClicked(ArtifactInstance artifact)
        {
            if (favouriteMode || artifact == null) return;
            OnArtifactDoubleClicked?.Invoke(artifact);
        }

        private void RefreshFavouriteModeVisual()
        {
            if (favouriteModeLabel != null)
                favouriteModeLabel.text = favouriteMode ? "Select Artifacts to Favourite" : "Favourite";
        }

        private void CycleSortMode()
        {
            int count = Enum.GetValues(typeof(ArtifactSortMode)).Length;
            int next = ((int)query.sortMode + 1) % count;
            SetSortMode(next);
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
                _ => mode.ToString()
            };
        }

        private void CycleFavouriteFilter()
        {
            query.favourite = (ArtifactOptionalBool)(((int)query.favourite + 1) % 3);
            if (runtimeFilterLabel != null) runtimeFilterLabel.text = $"Fav: {query.favourite}";
            RebuildQueryResults();
        }

        private void CycleLineFilter()
        {
            runtimeLineFilter = (runtimeLineFilter + 1) % 5;
            query.minimumLineCount = runtimeLineFilter == 0 ? 0 : runtimeLineFilter;
            query.maximumLineCount = runtimeLineFilter == 0 ? 4 : runtimeLineFilter;
            if (runtimeLineLabel != null) runtimeLineLabel.text = runtimeLineFilter == 0 ? "Lines: Any" : $"Lines: {runtimeLineFilter}";
            RebuildQueryResults();
        }

        private void CycleStatFilter()
        {
            IReadOnlyList<ArtifactStatRollDefinition> stats = playerInventory != null
                ? playerInventory.ArtifactRollTable.Stats
                : null;
            int count = stats != null ? stats.Count : 0;
            runtimeStatFilter = (runtimeStatFilter + 1) % (count + 1);
            if (runtimeStatFilter == 0)
            {
                query.requiredStatMask = 0UL;
                if (runtimeStatLabel != null) runtimeStatLabel.text = "Stat: Any";
            }
            else
            {
                ArtifactStatKind kind = stats[runtimeStatFilter - 1].statKind;
                query.requiredStatMask = ArtifactInventoryQueryEngine.MaskFor(kind);
                query.matchAllStats = false;
                query.selectedSortStat = kind;
                if (runtimeStatLabel != null) runtimeStatLabel.text = $"Stat: {ArtifactStatCatalog.GetLabel(kind)}";
            }
            RebuildQueryResults();
        }

        private void CycleEquippedFilter()
        {
            query.equipped = (ArtifactOptionalBool)(((int)query.equipped + 1) % 3);
            if (runtimeEquippedLabel != null) runtimeEquippedLabel.text = $"Equipped: {query.equipped}";
            RebuildQueryResults();
        }

        private void CycleNewFilter()
        {
            query.isNew = (ArtifactOptionalBool)(((int)query.isNew + 1) % 3);
            if (runtimeNewLabel != null) runtimeNewLabel.text = $"New: {query.isNew}";
            RebuildQueryResults();
        }

        private void EnsureRuntimeControls()
        {
            if (sortDropdown != null || searchInput != null || favouriteModeButton != null) return;

            GameObject toolbar = new GameObject("ArtifactQueryToolbar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            toolbar.transform.SetParent(transform, false);
            RectTransform toolbarRect = toolbar.GetComponent<RectTransform>();
            toolbarRect.anchorMin = new Vector2(0f, 1f);
            toolbarRect.anchorMax = new Vector2(1f, 1f);
            toolbarRect.pivot = new Vector2(0.5f, 1f);
            toolbarRect.anchoredPosition = new Vector2(0f, -4f);
            toolbarRect.sizeDelta = new Vector2(0f, 40f);
            toolbar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            HorizontalLayoutGroup layout = toolbar.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.spacing = 6f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            favouriteModeButton = CreateButton(toolbar.transform, "Favourite", ToggleFavouriteMode, out TMP_Text favouriteLabel);
            favouriteModeLabel = favouriteLabel;
            Button sortButton = CreateButton(toolbar.transform, $"Sort: {GetSortModeDisplay(query.sortMode)}", CycleSortMode, out runtimeSortLabel);
            Button orderButton = CreateButton(toolbar.transform, query.descending ? "▾ Desc" : "▴ Asc", ToggleSortDirection, out runtimeOrderLabel);
            Button filterButton = CreateButton(toolbar.transform, "Fav: Any", CycleFavouriteFilter, out runtimeFilterLabel);
            Button lineButton = CreateButton(toolbar.transform, "Lines: Any", CycleLineFilter, out runtimeLineLabel);
            Button statButton = CreateButton(toolbar.transform, "Stat: Any", CycleStatFilter, out runtimeStatLabel);
            Button equippedButton = CreateButton(toolbar.transform, "Equipped: Any", CycleEquippedFilter, out runtimeEquippedLabel);
            Button newButton = CreateButton(toolbar.transform, "New: Any", CycleNewFilter, out runtimeNewLabel);

            SetPreferredWidth(favouriteModeButton, 130f);
            SetPreferredWidth(sortButton, 160f);
            SetPreferredWidth(orderButton, 85f);
            SetPreferredWidth(filterButton, 95f);
            SetPreferredWidth(lineButton, 95f);
            SetPreferredWidth(statButton, 120f);
            SetPreferredWidth(equippedButton, 130f);
            SetPreferredWidth(newButton, 90f);

            searchInput = CreateSearchInput(toolbar.transform);
            searchInput.onValueChanged.AddListener(SetSearchText);
        }

        private static Button CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction action, out TMP_Text label)
        {
            GameObject buttonObject = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.95f);
            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(action);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 18f;
            return button;
        }

        private static void SetPreferredWidth(Component component, float width)
        {
            LayoutElement layout = component != null ? component.GetComponent<LayoutElement>() : null;
            if (layout != null) layout.preferredWidth = width;
        }

        private static TMP_InputField CreateSearchInput(Transform parent)
        {
            GameObject inputObject = new GameObject("ArtifactSearch", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            inputObject.transform.SetParent(parent, false);
            inputObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            inputObject.GetComponent<LayoutElement>().preferredWidth = 180f;

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(inputObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 2f);
            textRect.offsetMax = new Vector2(-8f, -2f);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderObject.transform.SetParent(inputObject.transform, false);
            RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(8f, 2f);
            placeholderRect.offsetMax = new Vector2(-8f, -2f);
            TextMeshProUGUI placeholder = placeholderObject.GetComponent<TextMeshProUGUI>();
            placeholder.text = "Search Artifact";
            placeholder.fontSize = 18f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(1f, 1f, 1f, 0.45f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.textViewport = inputObject.GetComponent<RectTransform>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
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
