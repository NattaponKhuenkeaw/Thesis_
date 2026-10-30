using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatCore
{
    /// <summary>
    /// Central manager for training and debugging visualization.
    /// Controls whether hitboxes, hurtboxes, and stats HUD are visible.
    /// </summary>
    public class TrainingDebugManager : MonoBehaviour
    {
        private static TrainingDebugManager _instance;
        public static TrainingDebugManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindFirstObjectByType<TrainingDebugManager>();
                }
                return _instance;
            }
        }

        [Header("Global Toggle")]
        [Tooltip("Master switch for all training debug visualizations and HUD.")]
        public bool showDebugVisualization = false;

        public static bool IsActive => Instance != null && Instance.showDebugVisualization;
        private bool trainingPresentation;
        private bool trainingStatsVisible;
        public static bool IsTrainingPresentation => Instance != null && Instance.trainingPresentation;
        public static bool CollectStats => Instance != null && (Instance.trainingPresentation || Instance.showDebugVisualization);
        public static bool ShowStatsHud => Instance != null && (Instance.trainingPresentation
            ? Instance.trainingStatsVisible : Instance.showDebugVisualization);
        public static bool DrawColliders => Instance != null && !Instance.trainingPresentation && Instance.showDebugVisualization;

        public void SetTrainingPresentation(bool active)
        {
            trainingPresentation = active;
            trainingStatsVisible = false;
            if (active) ResolveTeamLoader();
        }

        public void ToggleTrainingStats()
        {
            if (trainingPresentation) trainingStatsVisible = !trainingStatsVisible;
        }
        public event Action TeamRosterChanged;

        private TeamLoader _teamLoader;
        private CharacterObject[] _activeTeam = Array.Empty<CharacterObject>();

        public TeamLoader TeamLoader => _teamLoader;
        public IReadOnlyList<CharacterObject> ActiveTeam => _activeTeam;

        public void ToggleDebugMode()
        {
            showDebugVisualization = !showDebugVisualization;
            if (showDebugVisualization)
            {
                ResolveTeamLoader();
            }
        }

        public void SetDebugMode(bool active)
        {
            showDebugVisualization = active;
            if (active)
            {
                ResolveTeamLoader();
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (_instance.gameObject.scene == gameObject.scene)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            _instance = this;
            // Removed DontDestroyOnLoad to keep it scoped to the scene unless needed otherwise,
            // but plan implies a persistent singleton-like hub.
        }

        private void OnEnable()
        {
            if (_instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        private void Start()
        {
            ResolveTeamLoader();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                UnbindTeamLoader();
                _instance = null;
            }
        }

        public CharacterObject ResolveDamageSource(CharacterObject sourceCharacter)
        {
            if (sourceCharacter != null)
            {
                for (int i = 0; i < _activeTeam.Length; i++)
                {
                    if (_activeTeam[i] == sourceCharacter)
                    {
                        return sourceCharacter;
                    }
                }

                return null;
            }

            return _teamLoader != null ? _teamLoader.CurrentCharacter : null;
        }

        public static string GetCharacterDisplayName(CharacterObject character)
        {
            if (character == null)
            {
                return "Unknown";
            }

            return character.metaData != null && !string.IsNullOrWhiteSpace(character.metaData.displayName)
                ? character.metaData.displayName
                : character.name;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveTeamLoader(forceNotify: true);
        }

        private void ResolveTeamLoader(bool forceNotify = false)
        {
            TeamLoader resolved = TeamLoader.Instance;
            if (resolved == null)
            {
                resolved = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();
            }

            if (_teamLoader != resolved)
            {
                UnbindTeamLoader();
                _teamLoader = resolved;
                if (_teamLoader != null)
                {
                    _teamLoader.OnTeamChanged += RefreshActiveTeam;
                }
            }

            RefreshActiveTeam(forceNotify);
        }

        private void UnbindTeamLoader()
        {
            if (_teamLoader != null)
            {
                _teamLoader.OnTeamChanged -= RefreshActiveTeam;
                _teamLoader = null;
            }
        }

        private void RefreshActiveTeam()
        {
            RefreshActiveTeam(forceNotify: false);
        }

        private void RefreshActiveTeam(bool forceNotify)
        {
            CharacterObject[] snapshot = _teamLoader != null
                ? _teamLoader.GetTeamSnapshot()
                : Array.Empty<CharacterObject>();

            int occupiedCount = 0;
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] != null)
                {
                    occupiedCount++;
                }
            }

            CharacterObject[] orderedTeam = new CharacterObject[occupiedCount];
            int destinationIndex = 0;
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] != null)
                {
                    orderedTeam[destinationIndex++] = snapshot[i];
                }
            }

            bool changed = _activeTeam.Length != orderedTeam.Length;
            if (!changed)
            {
                for (int i = 0; i < orderedTeam.Length; i++)
                {
                    if (_activeTeam[i] != orderedTeam[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            _activeTeam = orderedTeam;
            if (changed || forceNotify)
            {
                TeamRosterChanged?.Invoke();
            }
        }
    }
}
