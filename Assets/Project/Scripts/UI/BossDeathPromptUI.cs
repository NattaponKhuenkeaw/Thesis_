using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// Lightweight full-screen prompt shown after boss reward absorb.
    /// Does not lock movement — only displays text until left-click continues.
    /// </summary>
    public sealed class BossDeathPromptUI : MonoBehaviour
    {
        public static BossDeathPromptUI Instance { get; private set; }

        [SerializeField] private Canvas canvas;
        [SerializeField] private TextMeshProUGUI promptLabel;
        [SerializeField] private string defaultPrompt = "กดคลิกซ้ายเพื่อไปด่านต่อไป";

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

            GameObject go = new GameObject("BossDeathPromptUI");
            return go.AddComponent<BossDeathPromptUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureUi();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(string message = null)
        {
            EnsureUi();
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

        private void EnsureUi()
        {
            if (canvas != null && promptLabel != null)
                return;

            GameObject canvasGo = new GameObject("BossDeathPrompt_Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject labelGo = new GameObject("PromptLabel");
            labelGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rect = labelGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.12f);
            rect.anchorMax = new Vector2(0.5f, 0.12f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 80f);

            promptLabel = labelGo.AddComponent<TextMeshProUGUI>();
            promptLabel.text = defaultPrompt;
            promptLabel.fontSize = 36f;
            promptLabel.alignment = TextAlignmentOptions.Center;
            promptLabel.fontStyle = FontStyles.Bold;
            promptLabel.color = new Color(1f, 0.95f, 0.85f, 0.95f);
            promptLabel.raycastTarget = false;
        }
    }
}
