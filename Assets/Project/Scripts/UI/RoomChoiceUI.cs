using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Project.Core.ZoneState;

namespace Project.UI
{
    /// <summary>
    /// BlazBlue-style modal UI: pick the next room type from offer cards.
    /// Builds the panel at runtime (no authored prefab required).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoiceUI : MonoBehaviour
    {
        private const int CanvasSortingOrder = 8500;

        public static RoomChoiceUI Instance { get; private set; }

        private GameObject canvasRoot;
        private readonly List<Button> choiceButtons = new List<Button>(4);
        private bool hasInputLock;
        private bool choiceCommitted;

        public bool IsShowing => canvasRoot != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            ReleaseInputLock();
        }

        public static RoomChoiceUI EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            RoomChoiceUI existing = FindFirstObjectByType<RoomChoiceUI>();
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject("RoomChoiceUI");
            return go.AddComponent<RoomChoiceUI>();
        }

        public void Show(IReadOnlyList<RoomChoiceOffer> offers)
        {
            Hide();
            choiceCommitted = false;

            if (offers == null || offers.Count == 0)
            {
                Debug.LogWarning("[RoomChoiceUI] No offers to show.", this);
                return;
            }

            EnsureEventSystem();
            AcquireInputLock();

            canvasRoot = new GameObject("RoomChoiceUI_Canvas");
            canvasRoot.transform.SetParent(transform, false);

            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<GraphicRaycaster>();

            GameObject dimmer = CreateRect("Dimmer", canvasRoot.transform);
            StretchToParent(dimmer.GetComponent<RectTransform>());
            Image dimmerImage = dimmer.AddComponent<Image>();
            dimmerImage.color = new Color(0f, 0f, 0f, 0.62f);

            GameObject panel = CreateRect("Panel", dimmer.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1100f, 520f);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.08f, 0.11f, 0.96f);

            VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(36, 36, 28, 28);
            panelLayout.spacing = 22f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = false;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            TextMeshProUGUI title = CreateText("Title", panel.transform, "Choose Next Path", 34f, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;

            TextMeshProUGUI subtitle = CreateText(
                "Subtitle",
                panel.transform,
                "Select a room type to continue",
                18f,
                FontStyles.Normal);
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.color = new Color(0.78f, 0.82f, 0.88f, 1f);

            GameObject row = CreateRect("Choices", panel.transform);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 340f);
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 340f;
            rowLayout.flexibleHeight = 1f;

            HorizontalLayoutGroup rowGroup = row.AddComponent<HorizontalLayoutGroup>();
            rowGroup.spacing = 20f;
            rowGroup.childAlignment = TextAnchor.MiddleCenter;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandWidth = true;
            rowGroup.childForceExpandHeight = true;
            rowGroup.padding = new RectOffset(8, 8, 8, 8);

            choiceButtons.Clear();
            for (int i = 0; i < offers.Count; i++)
            {
                if (!offers[i].IsValid)
                {
                    continue;
                }

                CreateChoiceCard(row.transform, offers[i], i);
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Debug.Log($"[RoomChoiceUI] Showing {choiceButtons.Count} room choices.", this);
        }

        public void Hide()
        {
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                if (choiceButtons[i] != null)
                {
                    choiceButtons[i].onClick.RemoveAllListeners();
                }
            }

            choiceButtons.Clear();

            if (canvasRoot != null)
            {
                Destroy(canvasRoot);
                canvasRoot = null;
            }

            ReleaseInputLock();
        }

        private void CreateChoiceCard(Transform parent, RoomChoiceOffer offer, int index)
        {
            RoomData room = offer.Room;

            GameObject card = CreateRect($"Choice_{index}_{offer.Kind}", parent); 

            HorizontalLayoutGroup containerLayout = parent.GetComponent<HorizontalLayoutGroup>(); 
            if (containerLayout == null) 
            {
                containerLayout = parent.gameObject.AddComponent<HorizontalLayoutGroup>(); 
            }

            containerLayout.childControlWidth = false;
            containerLayout.childControlHeight = true;     
            containerLayout.childForceExpandWidth = false; 
            containerLayout.childForceExpandHeight = false; 
            containerLayout.childAlignment = TextAnchor.MiddleCenter; 
            containerLayout.spacing = 30f; 

            // เพิ่ม LayoutElement เพื่อล็อกขนาดการ์ดไม่ให้ยืดตาม parent
            LayoutElement cardElement = card.AddComponent<LayoutElement>(); 
            cardElement.preferredWidth = 260f;  // ความกว้างการ์ด
            cardElement.preferredHeight = 320f; // ความสูงการ์ด

            Image cardImage = card.AddComponent<Image>(); 
            cardImage.color = new Color(0.12f, 0.14f, 0.18f, 1f); 

            Outline outline = card.AddComponent<Outline>(); 

            Button button = card.AddComponent<Button>(); 
            button.targetGraphic = cardImage; 

            VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(12, 12, 16, 16); 
            cardLayout.spacing = 12f; 
            cardLayout.childAlignment = TextAnchor.MiddleCenter;
            cardLayout.childControlWidth = true; 
            cardLayout.childControlHeight = false; 
            cardLayout.childForceExpandWidth = true; 
            cardLayout.childForceExpandHeight = false; 

            // แยกการสร้าง UI ระหว่าง Shop กับ Combat
            bool isShop = offer.Kind == RoomKind.Shop;

            if (isShop)
            {
                // --- SHOP ---
                TextMeshProUGUI shopLabel = CreateText("ShopLabel", card.transform, "SHOP", 22f, FontStyles.Bold);
                shopLabel.alignment = TextAlignmentOptions.Center;
                shopLabel.color = ResolveKindColor(RoomKind.Shop); 

                outline.effectColor = ResolveKindColor(RoomKind.Shop); 
            }
            else
            {
                // --- COMBAT / ห้องต่อสู้ ---
                string difficultyText = GetDifficultyFromKind(offer.Kind);
                Color difficultyColor = ResolveDifficultyColor(difficultyText);

                TextMeshProUGUI roomTypeLabel = CreateText("RoomTypeLabel", card.transform, offer.Kind.ToString().ToUpper(), 16f, FontStyles.Bold);
                roomTypeLabel.alignment = TextAlignmentOptions.Center;
                roomTypeLabel.color = Color.white;

                TextMeshProUGUI difficultyLabel = CreateText("DifficultyLabel", card.transform, $"[{difficultyText.ToUpper()}]", 14f, FontStyles.Bold);
                difficultyLabel.alignment = TextAlignmentOptions.Center;
                difficultyLabel.color = difficultyColor;

                string rewardText = GetRewardText(offer); 
                TextMeshProUGUI rewardLabel = CreateText("RewardLabel", card.transform, $"Reward: {rewardText}", 13f, FontStyles.Normal);
                rewardLabel.alignment = TextAlignmentOptions.Center;
                rewardLabel.color = new Color(0.9f, 0.9f, 0.9f, 1f);

                outline.effectColor = difficultyColor;
            }

            // สีปุ่ม Hover/Pressed ปรับตามสีของกรอบ Outline
            ColorBlock colors = button.colors; 
            colors.highlightedColor = Color.Lerp(cardImage.color, outline.effectColor, 0.35f);
            colors.pressedColor = Color.Lerp(cardImage.color, outline.effectColor, 0.55f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            // Icon (ถ้ามี)
            if (room.OfferIcon != null) 
            {
                GameObject iconGo = CreateRect("Icon", card.transform); 
                LayoutElement iconLayout = iconGo.AddComponent<LayoutElement>(); 
                iconLayout.preferredHeight = 80f; 
                Image iconImage = iconGo.AddComponent<Image>(); 
                iconImage.sprite = room.OfferIcon; 
                iconImage.preserveAspect = true; 
                iconImage.raycastTarget = false; 
            }

            string roomId = offer.RoomId; 
            string entranceId = room.DefaultEntranceId; 
            button.onClick.AddListener(() => HandleChoiceClicked(roomId, entranceId, button)); 
            choiceButtons.Add(button); 
        }

        private void HandleChoiceClicked(string roomId, string entranceId, Button source)
        {
            if (choiceCommitted || string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            choiceCommitted = true;

            for (int i = 0; i < choiceButtons.Count; i++)
            {
                if (choiceButtons[i] != null)
                {
                    choiceButtons[i].interactable = false;
                }
            }

            if (source != null)
            {
                source.interactable = false;
            }

            Debug.Log($"[RoomChoiceUI] Selected room '{roomId}'.", this);
            Hide();

            if (SceneTransitionManager.Instance == null)
            {
                Debug.LogError("[RoomChoiceUI] SceneTransitionManager.Instance is missing.", this);
                return;
            }

            SceneTransitionManager.Instance.TransitionToRoom(roomId, entranceId);
        }

        private static Color ResolveKindColor(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Elite: return new Color(0.95f, 0.62f, 0.2f, 1f);
                case RoomKind.Shop: return new Color(0.3f, 0.78f, 0.98f, 1f);
                case RoomKind.Reward: return new Color(0.45f, 0.9f, 0.4f, 1f);
                case RoomKind.Boss: return new Color(0.78f, 0.28f, 0.95f, 1f);
                default: return new Color(0.92f, 0.32f, 0.32f, 1f);
            }
        }

        private void AcquireInputLock()
        {
            if (hasInputLock) return;
            InputLockManager.Lock();
            hasInputLock = true;
        }

        private void ReleaseInputLock()
        {
            if (!hasInputLock) return;
            InputLockManager.Unlock();
            hasInputLock = false;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, FontStyles style)
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

        /// <summary>
        /// กำหนดสีตามระดับความยาก: Easy = เขียว, Medium = เหลือง, Hard = ส้ม
        /// </summary>
        private static Color ResolveDifficultyColor(string difficulty)
        {
            if (string.IsNullOrEmpty(difficulty)) return Color.white;

            string diffLower = difficulty.ToLower();

            if (diffLower.Contains("easy"))
            {
                return new Color(0.18f, 0.8f, 0.44f, 1f); // เขียว
            }
            if (diffLower.Contains("medium") || diffLower.Contains("med"))
            {
                return new Color(0.95f, 0.77f, 0.06f, 1f); // เหลือง
            }
            if (diffLower.Contains("hard"))
            {
                return new Color(0.9f, 0.49f, 0.13f, 1f); // ส้ม
            }

            return Color.white;
        }

        /// <summary>
        /// แปลงข้อมูลของรางวัลเป็นข้อความ [ coin | artifact | artifact + coin ]
        /// </summary>
        private static string GetRewardText(RoomChoiceOffer offer)
        {
            // ถ้า offer มี property เก็บ reward ไว้ ให้ดึงมาใช้ตรงนี้
            // เช่น return offer.RewardType.ToString();
            return "coin"; // ค่าเริ่มต้นกรณีไม่มีตัวแปร reward
        }
        private static string GetDifficultyFromKind(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Shop:
                case RoomKind.Reward:
                    return "Easy";

                case RoomKind.Elite:
                case RoomKind.Boss:
                    return "Hard";

                default:
                    // สุ่มระดับความยาก (Easy, Medium, Hard) สำหรับห้อง Combat ปกติ
                    string[] difficulties = { "Easy", "Medium", "Hard" };
                    int randomIndex = Random.Range(0, difficulties.Length);
                    return difficulties[randomIndex];
            }
        }
    }
}
