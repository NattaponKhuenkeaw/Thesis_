#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.UI;

namespace Project.EditorTools
{
    /// <summary>
    /// Scaffolds an authored Room Choice UI prefab you can restyle in the Editor.
    /// </summary>
    public static class RoomChoiceUIFactory
    {
        private const string PrefabPath = "Assets/Project/Prefab/UI/RoomChoiceUI.prefab";
        private const string ResourcesPrefabPath = "Assets/Project/Resources/UI/RoomChoiceUI.prefab";

        [MenuItem("Project/UI/Create Room Choice UI Prefab")]
        public static void CreatePrefab()
        {
            EnsureFolder("Assets/Project/Prefab");
            EnsureFolder("Assets/Project/Prefab/UI");
            EnsureFolder("Assets/Project/Resources");
            EnsureFolder("Assets/Project/Resources/UI");
            BuildAndSaveBoth();
        }

        [MenuItem("Project/UI/Sync Room Choice Prefab → Resources")]
        public static void SyncPrefabToResources()
        {
            EnsureFolder("Assets/Project/Resources");
            EnsureFolder("Assets/Project/Resources/UI");

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (source == null)
            {
                EditorUtility.DisplayDialog(
                    "Room Choice UI",
                    $"Missing source prefab:\n{PrefabPath}\n\nRun Create Room Choice UI Prefab first, or save your UI there.",
                    "OK");
                return;
            }

            RuntimeUiPrefabLinkEditor.EnsureLink("RoomChoiceUI");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Object resourcesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesPrefabPath);
            Selection.activeObject = resourcesPrefab;
            EditorGUIUtility.PingObject(resourcesPrefab);
            Debug.Log($"[RoomChoiceUIFactory] Synced {PrefabPath} → {ResourcesPrefabPath}");
        }

        private static void BuildAndSaveBoth()
        {
            GameObject root = new GameObject("RoomChoiceUI", typeof(RectTransform));
            RoomChoiceUI ui = root.AddComponent<RoomChoiceUI>();

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8500;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            GameObject panel = CreateUiObject("Panel", root.transform);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.08f, 0.11f, 0.92f);
            RectTransform panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0f);
            panelRt.anchorMax = new Vector2(0.5f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.anchoredPosition = new Vector2(0f, 48f);
            panelRt.sizeDelta = new Vector2(1100f, 420f);

            VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(28, 28, 20, 20);
            panelLayout.spacing = 14f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = false;
            panelLayout.childForceExpandWidth = true;

            CreateLabel(panel.transform, "Title", "Choose Next Path", 30f, FontStyles.Bold);
            CreateLabel(panel.transform, "Subtitle", "Walk away to cancel · Select a room to continue", 16f, FontStyles.Normal);

            GameObject row = CreateUiObject("Cards", panel.transform);
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 280f;
            rowLayout.flexibleHeight = 1f;
            HorizontalLayoutGroup rowGroup = row.AddComponent<HorizontalLayoutGroup>();
            rowGroup.spacing = 20f;
            rowGroup.childAlignment = TextAnchor.MiddleCenter;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandWidth = true;
            rowGroup.childForceExpandHeight = true;
            rowGroup.padding = new RectOffset(8, 8, 8, 8);

            RoomChoiceCardView[] cards = new RoomChoiceCardView[3];
            for (int i = 0; i < 3; i++)
            {
                cards[i] = CreateCard(row.transform, i);
            }

            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("panelRoot").objectReferenceValue = panel;
            SerializedProperty cardsProp = so.FindProperty("cards");
            cardsProp.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);

            SavePrefab(root, PrefabPath);
            Object.DestroyImmediate(root);
            RuntimeUiPrefabLinkEditor.EnsureLink("RoomChoiceUI");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject resourcesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesPrefabPath);
            Selection.activeObject = resourcesPrefab;
            EditorGUIUtility.PingObject(resourcesPrefab);
            Debug.Log(
                $"[RoomChoiceUIFactory] Created authored prefabs:\n- {PrefabPath}\n- {ResourcesPrefabPath}\n"
                + "Dress the Panel/Cards in the Inspector. Runtime loads Resources/UI/RoomChoiceUI.");
        }

        private static RoomChoiceCardView CreateCard(Transform parent, int index)
        {
            GameObject card = CreateUiObject($"Card_{index}", parent);
            Image cardImage = card.AddComponent<Image>();
            cardImage.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(0.92f, 0.32f, 0.32f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = cardImage;

            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 22, 22);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            GameObject badge = CreateUiObject("KindBadge", card.transform);
            LayoutElement badgeLayout = badge.AddComponent<LayoutElement>();
            badgeLayout.preferredHeight = 42f;
            Image badgeImage = badge.AddComponent<Image>();
            badgeImage.color = new Color(0.92f, 0.32f, 0.32f, 1f);
            TMP_Text kindLabel = CreateLabel(badge.transform, "Kind", "KIND", 20f, FontStyles.Bold);
            kindLabel.color = new Color(0.08f, 0.08f, 0.1f, 1f);
            Stretch(kindLabel.rectTransform);

            GameObject iconGo = CreateUiObject("Icon", card.transform);
            LayoutElement iconLayout = iconGo.AddComponent<LayoutElement>();
            iconLayout.preferredHeight = 72f;
            Image iconImage = iconGo.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.enabled = false;

            TMP_Text nameLabel = CreateLabel(card.transform, "Name", "Room Name", 22f, FontStyles.Bold);
            TMP_Text difficultyLabel = CreateLabel(card.transform, "Difficulty", "EASY", 18f, FontStyles.Bold);
            TMP_Text rewardLabel = CreateLabel(card.transform, "Reward", "Reward / Zone", 16f, FontStyles.Normal);

            RoomChoiceCardView view = card.AddComponent<RoomChoiceCardView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("kindLabel").objectReferenceValue = kindLabel;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("difficultyLabel").objectReferenceValue = difficultyLabel;
            so.FindProperty("rewardOrZoneLabel").objectReferenceValue = rewardLabel;
            so.FindProperty("kindBadge").objectReferenceValue = badgeImage;
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("accentOutline").objectReferenceValue = outline;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, string text, float size, FontStyles style)
        {
            GameObject go = CreateUiObject(name, parent);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = size + 10f;
            return label;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
