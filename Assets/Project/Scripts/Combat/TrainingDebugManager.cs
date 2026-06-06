using UnityEngine;

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
                    _instance = Object.FindFirstObjectByType<TrainingDebugManager>();
                }
                return _instance;
            }
        }

        [Header("Global Toggle")]
        [Tooltip("Master switch for all training debug visualizations and HUD.")]
        public bool showDebugVisualization = false;

        public static bool IsActive => Instance != null && Instance.showDebugVisualization;

        public void ToggleDebugMode()
        {
            showDebugVisualization = !showDebugVisualization;
        }

        public void SetDebugMode(bool active)
        {
            showDebugVisualization = active;
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

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
