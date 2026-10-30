using System;
using System.Collections;
using System.Collections.Generic;
using CombatCore;
using Project.Progression;
using Project.ShopSystem;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Training
{
    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    public sealed class TrainingSession : MonoBehaviour
    {
        public const string SceneName = "Test_Playground";
        public static TrainingSession Instance { get; private set; }
        public static bool IsTrainingContext => Application.isPlaying
            && ((Instance != null && Instance.IsActive) || SceneManager.GetActiveScene().name == SceneName);

        [Header("Session Data")]
        [SerializeField] private PlayerRunInventory runInventory;
        [SerializeField] private PlayerMetaProgression metaProgression;
        [Header("Scene Presentation")]
        [SerializeField] private CinemachineCamera trainingCamera;
        [SerializeField] private TrainingDebugManager debugManager;
        [SerializeField] private UI.Training.TrainingConfigurationPanel configurationPanel;

        private readonly object _owner = new object();
        private bool _ending;
        private CameraManager _cameraManager;
        private Project.Core.RootPersistentManager _ownedRoot;
        private readonly HashSet<GameObject> _preexistingManagers = new HashSet<GameObject>();
        public bool IsActive { get; private set; }
        public PlayerRunInventory Inventory => runInventory;
        public PlayerMetaProgression Progression => metaProgression;

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
            var team = FindFirstObjectByType<TeamLoader>();
            if (runInventory == null) runInventory = team != null ? team.RunInventory : null;
            if (metaProgression == null) metaProgression = team != null ? team.MetaProgression : null;
            if (runInventory == null) runInventory = Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
            if (metaProgression == null) metaProgression = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
            if (runInventory == null || metaProgression == null || trainingCamera == null)
            {
                Debug.LogError("[Training] Inventory, progression and Training camera must be assigned.", this);
                enabled = false;
                return;
            }
            IsActive = true;
            try
            {
                runInventory.BeginTrainingSession(_owner);
                metaProgression.BeginTrainingSession(_owner);
            }
            catch
            {
                EndSession();
                throw;
            }
        }

        private IEnumerator Start()
        {
            if (!IsActive) yield break;
            _cameraManager = CameraManager.instance;
            if (_cameraManager != null) _cameraManager.SetTrainingMode(true, trainingCamera);
            else Debug.LogError("[Training] CameraManager is missing.", this);
            if (debugManager == null) debugManager = TrainingDebugManager.Instance;
            if (debugManager != null) debugManager.SetTrainingPresentation(true);
            else Debug.LogError("[Training] Statistics manager is missing.", this);
            TrainingDebugStats.Instance?.ClearSample();

            // Passive targets still need camera membership; detection is deliberately disabled.
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
            var result = new TrainingConfiguration { teamLevel = runInventory.GlobalLevel };
            CharacterObject[] team = runInventory.GetTeamSnapshot();
            var ids = new HashSet<string>();
            foreach (var character in team)
            {
                if (character == null || !ids.Add(character.characterId)) continue;
                result.characters.Add(new TrainingConfiguration.CharacterPotential
                {
                    characterId = character.characterId,
                    skill = metaProgression.GetPotentialLevel(character, PotentialTrack.Skill),
                    ultimate = metaProgression.GetPotentialLevel(character, PotentialTrack.Ultimate),
                    passive = metaProgression.GetPotentialLevel(character, PotentialTrack.Passive)
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
            if (!IsActive || draft == null || draft.characters == null || draft.teamLevel < 1 || draft.teamLevel > runInventory.GlobalMaxLevel)
                validationError = "Choose a valid team level.";
            if (validationError != null) return false;
            CharacterObject[] roster = runInventory.GetTeamSnapshot();
            var seen = new HashSet<string>();
            foreach (var value in draft.characters)
            {
                CharacterObject character = Array.Find(roster, c => c != null && value != null && c.characterId == value.characterId);
                if (character == null || !seen.Add(value.characterId)
                    || value.skill < 0 || value.skill > GetMaxPotentialLevel(character, PotentialTrack.Skill)
                    || value.ultimate < 0 || value.ultimate > GetMaxPotentialLevel(character, PotentialTrack.Ultimate)
                    || value.passive < 0 || value.passive > GetMaxPotentialLevel(character, PotentialTrack.Passive))
                {
                    validationError = "Choose valid potential tiers for deployed characters.";
                    return false;
                }
            }
            int before = runInventory.GlobalLevel;
            runInventory.SetTrainingLevelOverride(_owner, draft.teamLevel, notify: false);
            metaProgression.SetTrainingPotentialOverrides(_owner, draft.characters, notify: false);
            // Both authorities are staged before listeners rebuild active and off-field records.
            runInventory.NotifyTrainingLevelChanged(before);
            metaProgression.NotifyTrainingPotentialsChanged();
            ResetDamageSample();
            return true;
        }

        public void ResetDamageSample() => TrainingDebugStats.Instance?.ClearSample();
        public void ToggleStats() { if (debugManager != null) debugManager.ToggleTrainingStats(); }

        public void EndSession()
        {
            if (!IsActive || _ending) return;
            _ending = true;
            StopAllCoroutines();
            configurationPanel?.Close();
            if (debugManager != null) debugManager.SetTrainingPresentation(false);
            if (_cameraManager != null) _cameraManager.SetTrainingMode(false, trainingCamera);
            try { if (metaProgression != null) metaProgression.EndTrainingSession(_owner); }
            finally
            {
                if (runInventory != null) runInventory.EndTrainingSession(_owner);
                IsActive = false;
                _ending = false;
                // A direct scene load must clean up Training-created persistent managers too.
                if (_ownedRoot != null) Destroy(_ownedRoot.gameObject);
                DestroyTrainingManager(Project.Core.GameRunManager.Instance);
                DestroyTrainingManager(Project.Core.ZoneState.SceneTransitionManager.Instance);
                DestroyTrainingManager(Project.Core.ZoneState.RunProgressionManager.Instance);
                DestroyTrainingManager(Project.Core.ZoneState.RoomChoiceDirector.Instance);
                DestroyTrainingManager(GameFreezeManager.Instance);
            }
        }

        private void RememberManager(Component manager)
        {
            if (manager != null) _preexistingManagers.Add(manager.gameObject);
        }

        private void DestroyTrainingManager(Component manager)
        {
            if (manager != null && !_preexistingManagers.Contains(manager.gameObject)) Destroy(manager.gameObject);
        }

        private void OnDisable() { if (Instance == this) EndSession(); }
        private void OnDestroy()
        {
            if (Instance != this) return;
            EndSession();
            Instance = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
