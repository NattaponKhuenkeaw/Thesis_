using UnityEngine;
using Project.InventorySystem.Items;
using Project.TeamBuilder.UI;
using Project.Core.ZoneState;

namespace Project.UI
{
    /// <summary>
    /// Central entry point for opening and closing all UI panels.
    /// Wire ALL buttons to methods on this singleton instead of talking to
    /// individual panel scripts directly.
    ///
    /// Lives on an always-active root GameObject so Awake always runs.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // =========================================================
        // Singleton
        // =========================================================
        public static UIManager Instance { get; private set; }

        // =========================================================
        // Panel References — assign in Inspector
        // =========================================================
        [Header("Panel References")]
        [SerializeField] private TeamBuilderUI teamBuilderUI;
        [SerializeField] private InspectScreenUI inspectScreenUI;
        [SerializeField] private TeamUI teamUI;

        [Header("Zone State")]
        [SerializeField] private ZoneStateProvider zoneStateProvider;

        // =========================================================
        // Unity Lifecycle
        // =========================================================
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Resolve loose references if not set in the Inspector.
            if (teamBuilderUI == null)
                teamBuilderUI = FindFirstObjectByType<TeamBuilderUI>(FindObjectsInactive.Include);
            if (inspectScreenUI == null)
                inspectScreenUI = FindFirstObjectByType<InspectScreenUI>(FindObjectsInactive.Include);
            if (teamUI == null)
                teamUI = FindFirstObjectByType<TeamUI>(FindObjectsInactive.Include);
            if (zoneStateProvider == null)
                zoneStateProvider = Resources.Load<ZoneStateProvider>("CombatSO/ZoneStateProvider");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // =========================================================
        // TeamBuilder
        // =========================================================

        /// <summary>Open the TeamBuilder panel. Wire to button onClick.</summary>
        public void OpenTeamBuilder()
        {
            if (teamBuilderUI == null)
            {
                Debug.LogError("[UIManager] TeamBuilderUI reference is missing!");
                return;
            }
            teamBuilderUI.OpenPanel();
        }

        /// <summary>Close the TeamBuilder panel.</summary>
        public void CloseTeamBuilder()
        {
            if (teamBuilderUI == null) return;
            teamBuilderUI.ClosePanel();
        }

        /// <summary>Toggle TeamBuilder open/closed. Wire to a TAB-style button.</summary>
        public void ToggleTeamBuilder()
        {
            if (teamBuilderUI == null)
            {
                Debug.LogError("[UIManager] TeamBuilderUI reference is missing!");
                return;
            }
            teamBuilderUI.TogglePanel();
        }

        // =========================================================
        // Inspector UI
        // =========================================================

        /// <summary>
        /// Open the Inspector showing the first available character.
        /// Wire to the Inspector open button.
        /// </summary>
        public void OpenInspector()
        {
            if (inspectScreenUI == null)
            {
                Debug.LogError("[UIManager] InspectScreenUI reference is missing!");
                return;
            }

            ZoneMode mode = Project.Training.TrainingAccessPolicy.LoadoutMode(
                zoneStateProvider != null ? zoneStateProvider.CurrentMode : ZoneMode.SafeZone);
            if (mode != ZoneMode.SafeZone)
            {
                Debug.Log("[UIManager] OpenInspector blocked: not in SafeZone.");
                return;
            }

            inspectScreenUI.OpenInspector();
        }

        /// <summary>
        /// Open the Inspector focused on a specific character by name/id.
        /// Called from TeamSlotUI (RMB hold) and any fragment item that links to a character.
        /// </summary>
        public void OpenInspectorWithCharacter(string characterName)
        {
            if (inspectScreenUI == null)
            {
                Debug.LogError("[UIManager] InspectScreenUI reference is missing!");
                return;
            }

            ZoneMode mode = Project.Training.TrainingAccessPolicy.LoadoutMode(
                zoneStateProvider != null ? zoneStateProvider.CurrentMode : ZoneMode.SafeZone);
            if (mode != ZoneMode.SafeZone)
            {
                Debug.Log("[UIManager] OpenInspectorWithCharacter blocked: not in SafeZone.");
                return;
            }

            inspectScreenUI.OpenInspectorWithCharacter(characterName);
        }

        /// <summary>
        /// Open the Inspector directly on a CharacterItem (for code callers that already
        /// have the item reference, e.g. CharacterGridItemUI clicks).
        /// </summary>
        public void OpenInspectorWithItem(CharacterItem character)
        {
            if (inspectScreenUI == null)
            {
                Debug.LogError("[UIManager] InspectScreenUI reference is missing!");
                return;
            }

            ZoneMode mode = Project.Training.TrainingAccessPolicy.LoadoutMode(
                zoneStateProvider != null ? zoneStateProvider.CurrentMode : ZoneMode.SafeZone);
            if (mode != ZoneMode.SafeZone)
            {
                Debug.Log("[UIManager] OpenInspectorWithItem blocked: not in SafeZone.");
                return;
            }

            inspectScreenUI.Open(character);
        }

        /// <summary>Close the Inspector.</summary>
        public void CloseInspector()
        {
            if (inspectScreenUI == null) return;
            inspectScreenUI.Close();
        }

        // =========================================================
        // Inspector — Page Navigation
        // =========================================================

        /// <summary>
        /// Open the Inspector (if not already open) and jump directly to the Potential tab.
        /// Wire to fragment items or shortcuts that link to character potential.
        /// </summary>
        public void OpenInspectorOnPotential()
        {
            if (inspectScreenUI == null)
            {
                Debug.LogError("[UIManager] InspectScreenUI reference is missing!");
                return;
            }

            ZoneMode mode = Project.Training.TrainingAccessPolicy.LoadoutMode(
                zoneStateProvider != null ? zoneStateProvider.CurrentMode : ZoneMode.SafeZone);
            if (mode != ZoneMode.SafeZone) return;

            // Open with the first available character (or keep current if already open).
            inspectScreenUI.OpenInspectorOnPage(InspectPage.Potential);
        }

        /// <summary>
        /// Open the Inspector on a specific character and jump to the Potential tab.
        /// Wire to character fragment items that link directly to potential.
        /// </summary>
        public void OpenInspectorOnPotentialWithCharacter(string characterName)
        {
            if (inspectScreenUI == null)
            {
                Debug.LogError("[UIManager] InspectScreenUI reference is missing!");
                return;
            }

            ZoneMode mode = Project.Training.TrainingAccessPolicy.LoadoutMode(
                zoneStateProvider != null ? zoneStateProvider.CurrentMode : ZoneMode.SafeZone);
            if (mode != ZoneMode.SafeZone) return;

            inspectScreenUI.OpenInspectorWithCharacterOnPage(characterName, InspectPage.Potential);
        }

        // =========================================================
        // Global Close
        // =========================================================

        /// <summary>Close all open panels immediately.</summary>
        public void CloseAll()
        {
            CloseInspector();
            CloseTeamBuilder();
        }
    }
}
