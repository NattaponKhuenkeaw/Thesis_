using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Manages the Deploy Confirmation modal dialog.
    /// Handles procedural dialog creation if the reference is null, and controls visibility/callbacks.
    /// </summary>
    public class DeployConfirmationController
    {
        private Canvas rootCanvas;
        private GameObject deployConfirmationPanel;
        private Button deployConfirmButton;
        private Button deployCancelButton;

        private Action onConfirm;
        private Action onCancel;

        public DeployConfirmationController(
            Canvas canvas, 
            GameObject existingPanel, 
            Button confirmBtn, 
            Button cancelBtn, 
            Action confirmCallback, 
            Action cancelCallback)
        {
            rootCanvas = canvas;
            deployConfirmationPanel = existingPanel;
            deployConfirmButton = confirmBtn;
            deployCancelButton = cancelBtn;
            onConfirm = confirmCallback;
            onCancel = cancelCallback;
        }

        public void Setup()
        {
            EnsureDeployConfirmationPanel();
        }

        public void Show()
        {
            if (deployConfirmationPanel != null)
            {
                deployConfirmationPanel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (deployConfirmationPanel != null)
            {
                deployConfirmationPanel.SetActive(false);
            }
        }

        private void EnsureDeployConfirmationPanel()
        {
            if (deployConfirmationPanel == null)
            {
                deployConfirmationPanel = CreateDeployConfirmationPanel();
            }

            if (deployConfirmButton != null)
            {
                deployConfirmButton.onClick.RemoveListener(OnDeployConfirmed);
                deployConfirmButton.onClick.AddListener(OnDeployConfirmed);
            }

            if (deployCancelButton != null)
            {
                deployCancelButton.onClick.RemoveListener(OnDeployCancelled);
                deployCancelButton.onClick.AddListener(OnDeployCancelled);
            }

            Hide();
        }

        private void OnDeployConfirmed()
        {
            onConfirm?.Invoke();
        }

        private void OnDeployCancelled()
        {
            onCancel?.Invoke();
        }

        private GameObject CreateDeployConfirmationPanel()
        {
            Transform parent = rootCanvas != null ? rootCanvas.transform : null;

            GameObject overlay = new GameObject("Deploy Confirmation Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            if (parent != null)
            {
                overlay.transform.SetParent(parent, false);
            }

            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.62f);

            GameObject card = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(overlay.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(460f, 220f);
            cardRect.anchoredPosition = Vector2.zero;

            Image cardImage = card.GetComponent<Image>();
            cardImage.color = new Color(0.07f, 0.08f, 0.1f, 0.96f);

            CreateDialogText(card.transform, "Title", "Save Loadout?", 26f, FontStyles.Bold, new Vector2(0f, 62f));
            CreateDialogText(
                card.transform,
                "Message",
                "This saves the Team Builder profile, refreshes TeamLoader runtime when available, then closes the Team UI.",
                16f,
                FontStyles.Normal,
                new Vector2(0f, 10f));

            deployConfirmButton = CreateDialogButton(card.transform, "Confirm Button", "Save", new Vector2(-90f, -68f), new Color(0.2f, 0.75f, 0.48f, 1f));
            deployCancelButton = CreateDialogButton(card.transform, "Cancel Button", "Cancel", new Vector2(90f, -68f), new Color(0.28f, 0.3f, 0.34f, 1f));

            overlay.SetActive(false);
            return overlay;
        }

        private static TextMeshProUGUI CreateDialogText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles fontStyle,
            Vector2 anchoredPosition)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(380f, fontSize > 20f ? 38f : 72f);
            rectTransform.anchoredPosition = anchoredPosition;

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static Button CreateDialogButton(
            Transform parent,
            string name,
            string text,
            Vector2 anchoredPosition,
            Color color)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
            buttonObject.transform.SetParent(parent, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(136f, 44f);
            buttonRect.anchoredPosition = anchoredPosition;

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = color;

            Button button = buttonObject.GetComponent<Button>();
            
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 17f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            return button;
        }
    }
}
