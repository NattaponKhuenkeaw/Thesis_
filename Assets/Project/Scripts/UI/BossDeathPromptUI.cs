using TMPro;
using UnityEngine;

namespace Project.UI
{
    /// <summary>
    /// Prefab-authored completion notice shown after the boss death presentation.
    /// Does not lock movement — only displays text until left-click continues.
    /// </summary>
    public sealed class BossDeathPromptUI : MonoBehaviour
    {
        private const string PrefabResourcePath = "UI/BossDeathPromptUI";
        public static BossDeathPromptUI Instance { get; private set; }

        [SerializeField] private Canvas canvas;
        [SerializeField] private TextMeshProUGUI promptLabel;
        [SerializeField] private string defaultPrompt = "Left-click to continue";

        private bool isShowing;

        public bool IsShowing => isShowing;

        public static BossDeathPromptUI EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            BossDeathPromptUI existing = FindFirstObjectByType<BossDeathPromptUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var prefab = Resources.Load<BossDeathPromptUI>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[BossDeathPromptUI] Missing Resources/UI/BossDeathPromptUI.prefab. No runtime UI will be generated.");
                return null;
            }
            Instance = Instantiate(prefab);
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(string message = null)
        {
            if (canvas == null || promptLabel == null)
            {
                Debug.LogError("[BossDeathPromptUI] The saved prefab has missing presentation references.", this);
                return;
            }
            if (promptLabel != null)
                promptLabel.text = string.IsNullOrWhiteSpace(message) ? defaultPrompt : message;

            if (canvas != null)
                canvas.enabled = true;

            isShowing = true;
        }

        public void Hide()
        {
            if (canvas != null)
                canvas.enabled = false;

            isShowing = false;
        }

    }
}
