using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CombatCore;
using Project.Progression;
using Project.InventorySystem.Items;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Project.Training
{
    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    public sealed class TrainingSession : MonoBehaviour
    {
        public const string SceneName = "Test_Playground";
        private const string TrainingDebugFontPath = "Assets/Project/Fonts/GNF TTF SDF.asset";
        private const string MainCharacterItemId = "char-0-main";
        public static TrainingSession Instance { get; private set; }
        public static bool IsTrainingContext => Application.isPlaying
            && ((Instance != null && Instance.IsActive) || SceneManager.GetActiveScene().name == SceneName);

        [Header("Scene Presentation")]
        [SerializeField] private CinemachineCamera trainingCamera;
        [SerializeField] private TrainingDebugManager debugManager;
        [SerializeField] private UI.Training.TrainingConfigurationPanel configurationPanel;
        [SerializeField] private TMP_FontAsset trainingDebugFont;

        private CameraManager _cameraManager;
        private Project.Core.RootPersistentManager _ownedRoot;
        private bool _startedSuccessfully;
        private readonly HashSet<GameObject> _preexistingManagers = new HashSet<GameObject>();
        public bool IsActive { get; private set; }
        public TrainingInventoryState Inventory { get; private set; }
        public TrainingProgressionState Progression { get; private set; }

        private void OnValidate()
        {
            ResolveTrainingDebugFont();
        }

        private TMP_FontAsset ResolveTrainingDebugFont()
        {
            if (trainingDebugFont != null) return trainingDebugFont;
#if UNITY_EDITOR
            trainingDebugFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TrainingDebugFontPath);
#endif
            return trainingDebugFont;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            var root = FindFirstObjectByType<Project.Core.RootPersistentManager>();
            if (root != null && root.gameObject.scene == gameObject.scene) _ownedRoot = root;
            RememberManager(Project.Core.GameRunManager.Instance);
            RememberManager(Project.Core.ZoneState.SceneTransitionManager.Instance);
            RememberManager(Project.Core.ZoneState.RunProgressionManager.Instance);
            RememberManager(Project.Core.ZoneState.RoomChoiceDirector.Instance);
            RememberManager(GameFreezeManager.Instance);

            Inventory = ScriptableObject.CreateInstance<TrainingInventoryState>();
            Inventory.name = "Training Inventory (Runtime)";
            Inventory.hideFlags = HideFlags.DontSave;
            Progression = ScriptableObject.CreateInstance<TrainingProgressionState>();
            Progression.name = "Training Potentials (Runtime)";
            Progression.hideFlags = HideFlags.DontSave;
            var catalog = Resources.Load<Project.InventorySystem.CodexInventory>("InventorySO/CodexInventory");
            var items = new List<BaseItem>();
            if (catalog != null && catalog.allItems != null) items.AddRange(catalog.allItems);
            items.AddRange(Resources.LoadAll<BaseItem>("ItemSO"));
            Inventory.PopulateCatalog(items);
            Inventory.SetLevel(1, notify: false);
            CharacterObject mainCharacter = Inventory.ownedItems.OfType<CharacterItem>()
                .Where(c => c != null && c.id == MainCharacterItemId && c.characterObjectData != null)
                .Select(c => c.characterObjectData)
                .FirstOrDefault();
            if (mainCharacter == null)
                Debug.LogError($"[Training] Default character '{MainCharacterItemId}' is missing from the practice catalog.", this);
            Inventory.DeployTeam(mainCharacter != null ? new[] { mainCharacter } : Array.Empty<CharacterObject>(), 0);
            IsActive = true;
            _startedSuccessfully = true;
        }

        private IEnumerator Start()
        {
            if (!IsActive) yield break;
            if (trainingCamera == null)
                trainingCamera = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(c => c.gameObject.scene == gameObject.scene && c.name == "CinemachineCamera_Training");
            _cameraManager = CameraManager.instance;
            if (_cameraManager != null && trainingCamera != null) _cameraManager.SetTrainingMode(true, trainingCamera);
            else Debug.LogError("[Training] CameraManager or CinemachineCamera_Training is missing.", this);
            if (debugManager == null) debugManager = TrainingDebugManager.Instance;
            if (debugManager != null) debugManager.SetTrainingPresentation(true);
            else Debug.LogError("[Training] Statistics manager is missing.", this);
            var statsCanvas = GetComponent<UI.Training.TrainingStatsCanvas>();
            if (statsCanvas == null) statsCanvas = gameObject.AddComponent<UI.Training.TrainingStatsCanvas>();
            var debugFont = ResolveTrainingDebugFont();
            if (debugFont != null) statsCanvas.SetFont(debugFont);
            else Debug.LogError($"[Training] Could not load debug font at '{TrainingDebugFontPath}'.", this);
            TrainingDebugStats.Instance?.ClearSample();

            var enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy.gameObject.scene != gameObject.scene) continue;
                while (enemy != null && !enemy.IsTargetable) yield return null;
                if (enemy != null) enemy.SetInCombat(true);
            }
        }

        public TrainingConfiguration CaptureConfiguration()
        {
            var result = new TrainingConfiguration { teamLevel = Inventory.GlobalLevel };
            CharacterObject[] team = Inventory.GetTeamSnapshot();
            var ids = new HashSet<string>();
            foreach (var character in team)
            {
                if (character == null || !ids.Add(character.characterId)) continue;
                result.characters.Add(new TrainingConfiguration.CharacterPotential
                {
                    characterId = character.characterId,
                    skill = Progression.GetPotentialLevel(character, PotentialTrack.Skill),
                    ultimate = Progression.GetPotentialLevel(character, PotentialTrack.Ultimate),
                    passive = Progression.GetPotentialLevel(character, PotentialTrack.Passive),
                    memory = Progression.CaptureMemory(character.characterId)
                });
            }
            return result;
        }

        public static int GetMaxPotentialLevel(CharacterObject character, PotentialTrack track)
        {
            if (character == null || character.kitDefinition == null) return 0;
            int max = 0;
            foreach (var node in character.kitDefinition.EnumerateAllNodes())
                if (node != null && node.track == track) max = Mathf.Max(max, node.level);
            return Mathf.Min(max, CharacterKitDefinition.MaxPotentialLevel);
        }

        public bool TryApply(TrainingConfiguration draft, out string validationError)
        {
            validationError = null;
            if (!IsActive || draft == null || draft.characters == null || draft.teamLevel < 1 || draft.teamLevel > Inventory.GlobalMaxLevel)
                validationError = "Choose a valid team level.";
            if (validationError != null) return false;
            CharacterObject[] roster = Inventory.GetTeamSnapshot();
            var seen = new HashSet<string>();
            foreach (var value in draft.characters)
            {
                CharacterObject character = Array.Find(roster, c => c != null && value != null && c.characterId == value.characterId);
                if (character == null || !seen.Add(value.characterId)
                    || value.skill < 0 || value.skill > GetMaxPotentialLevel(character, PotentialTrack.Skill)
                    || value.ultimate < 0 || value.ultimate > GetMaxPotentialLevel(character, PotentialTrack.Ultimate)
                    || value.passive < 0 || value.passive > GetMaxPotentialLevel(character, PotentialTrack.Passive)
                    || (value.memory != null && value.memory.CharacterId != value.characterId))
                {
                    validationError = "Choose valid upgrades for deployed characters.";
                    return false;
                }
            }
            int before = Inventory.GlobalLevel;
            Inventory.SetLevel(draft.teamLevel, notify: false);
            Progression.SetPotentials(draft.characters, notify: false);
            // Both authorities are staged before listeners rebuild active and off-field records.
            Inventory.NotifyLevelChanged(before);
            Progression.NotifyChanged();
            return true;
        }

        public void ResetDamageSample() => TrainingDebugStats.Instance?.ClearSample();
        public void ToggleStats() { if (debugManager != null) debugManager.ToggleTrainingStats(); }

        public void EndSession()
        {
            if (!IsActive) return;
            StopAllCoroutines();
            configurationPanel?.Close();
            if (debugManager != null) debugManager.SetTrainingPresentation(false);
            if (_cameraManager != null) _cameraManager.SetTrainingMode(false, trainingCamera);
            IsActive = false;
        }

        private void RememberManager(Component manager)
        {
            if (manager != null) _preexistingManagers.Add(manager.gameObject);
        }

        private void DestroyTrainingManager(Component manager)
        {
            if (manager != null && !_preexistingManagers.Contains(manager.gameObject)) Destroy(manager.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            EndSession();
            if (Inventory != null) Destroy(Inventory);
            if (Progression != null) Destroy(Progression);
            // Scene teardown owns manager cleanup. Opening/closing UI, disabling the
            // controller, or a failed startup must never destroy gameplay managers.
            if (_startedSuccessfully)
            {
                if (_ownedRoot != null) Destroy(_ownedRoot.gameObject);
                DestroyTrainingManager(Project.Core.GameRunManager.Instance);
                DestroyTrainingManager(Project.Core.ZoneState.SceneTransitionManager.Instance);
                DestroyTrainingManager(Project.Core.ZoneState.RunProgressionManager.Instance);
                DestroyTrainingManager(Project.Core.ZoneState.RoomChoiceDirector.Instance);
                DestroyTrainingManager(GameFreezeManager.Instance);
            }
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
