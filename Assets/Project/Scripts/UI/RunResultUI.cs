using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Project.Core.RunEnd;

namespace Project.UI
{
    [DisallowMultipleComponent]
    public class RunResultUI : MonoBehaviour
    {
        private const int CanvasSortingOrder = 9000;

        private GameObject canvasRoot;
        private Button returnButton;
        private Action returnCallback;

        public bool IsShowing => canvasRoot != null;

        public void Show(RunResultSnapshot snapshot, Action onReturnClicked)
        {
            Hide();

            returnCallback = onReturnClicked;
            EnsureEventSystem();

            canvasRoot = new GameObject("RunResultUI");
            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasRoot.AddComponent<GraphicRaycaster>();

            GameObject dimmer = CreateRect("Dimmer", canvasRoot.transform);
            RectTransform dimmerRect = dimmer.GetComponent<RectTransform>();
            StretchToParent(dimmerRect);
            Image dimmerImage = dimmer.AddComponent<Image>();
            dimmerImage.color = new Color(0f, 0f, 0f, 0.72f);

            GameObject panel = CreateRect("Panel", dimmer.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(760f, 560f);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 34, 34);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI title = CreateText("Title", panel.transform, "Run Result", 36f, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;

            TextMeshProUGUI body = CreateText("Summary", panel.transform, BuildSummary(snapshot), 22f, FontStyles.Normal);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.color = new Color(0.88f, 0.9f, 0.94f, 1f);
            LayoutElement bodyLayout = body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.preferredHeight = 330f;
            bodyLayout.flexibleHeight = 1f;

            GameObject buttonObject = CreateRect("ReturnButton", panel.transform);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(280f, 58f);
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.95f, 0.76f, 0.28f, 1f);

            returnButton = buttonObject.AddComponent<Button>();
            returnButton.targetGraphic = buttonImage;
            returnButton.onClick.AddListener(HandleReturnClicked);

            TextMeshProUGUI buttonText = CreateText("Label", buttonObject.transform, "Return to Lobby", 22f, FontStyles.Bold);
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = new Color(0.08f, 0.07f, 0.05f, 1f);
            StretchToParent(buttonText.rectTransform);

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Hide()
        {
            if (returnButton != null)
            {
                returnButton.onClick.RemoveListener(HandleReturnClicked);
                returnButton = null;
            }

            if (canvasRoot != null)
            {
                Destroy(canvasRoot);
                canvasRoot = null;
            }

            returnCallback = null;
        }

        private void HandleReturnClicked()
        {
            if (returnButton != null)
            {
                returnButton.interactable = false;
            }

            Action callback = returnCallback;
            Hide();
            callback?.Invoke();
        }

        private static string BuildSummary(RunResultSnapshot snapshot)
        {
            snapshot ??= new RunResultSnapshot();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Time: {snapshot.FormatElapsedTime()}");
            builder.AppendLine($"Rooms cleared: {snapshot.roomsCleared}");
            builder.AppendLine();

            AppendLines(builder, "Permanent rewards", snapshot.permanentRewards, "No new permanent rewards.");
            AppendLines(builder, "Unlocked roster", snapshot.unlockedCharacters, "No new roster unlocks.");
            AppendLines(builder, "Lost temporary items", snapshot.lostItems, "No temporary items lost.");
            AppendLines(builder, "Lost run artifacts", snapshot.lostArtifacts, "No run artifacts lost.");

            if (snapshot.lostCurrency > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"Lost run currency: {snapshot.lostCurrency}");
            }

            return builder.ToString();
        }

        private static void AppendLines(
            StringBuilder builder,
            string heading,
            IReadOnlyList<RunResultItemLine> lines,
            string emptyText)
        {
            builder.AppendLine($"{heading}:");
            if (lines == null || lines.Count == 0)
            {
                builder.AppendLine($"- {emptyText}");
                builder.AppendLine();
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                RunResultItemLine line = lines[i];
                if (line == null)
                {
                    continue;
                }

                string quantity = line.quantity > 1 ? $" x{line.quantity}" : string.Empty;
                builder.AppendLine($"- {line.displayName}{quantity}");
            }

            builder.AppendLine();
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            FontStyles style)
        {
            GameObject textObject = CreateRect(name, parent);
            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }
}
