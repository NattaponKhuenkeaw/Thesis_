#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Project.Core.ZoneState;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project.EditorTools
{
    public static class RoomRouteProgressAuthoring
    {
        public const string ProgressPath = "Assets/Project/Prefab/UI/RoomRouteProgress.prefab";
        public const string NodePath = "Assets/Project/Prefab/UI/RoomRouteProgressNode.prefab";
        private const string ChoicePath = "Assets/Project/Prefab/UI/RoomChoiceUI.prefab";
        private const string ArtFolder = "Assets/Project/UI/RoomProgress";
        private static readonly Color Yellow = new Color(1f, .91f, .05f);

        public static void Apply()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePath);
            var font = source.transform.Find("Panel/Title").GetComponent<TMP_Text>().font;
            BuildSprites();
            BuildNode(font);
            BuildProgress(font);
            var root = PrefabUtility.LoadPrefabContents(ChoicePath);
            try
            {
                Transform panel = root.transform.Find("Panel");
                var title = panel.Find("Title").GetComponent<TMP_Text>();
                title.fontSize = 54f;
                title.rectTransform.anchoredPosition = new Vector2(0f, 448f);
                title.rectTransform.sizeDelta = new Vector2(1500f, 76f);
                var subtitle = panel.Find("Subtitle").GetComponent<TMP_Text>();
                subtitle.fontSize = 28f;
                subtitle.rectTransform.anchoredPosition = new Vector2(0f, 390f);
                subtitle.rectTransform.sizeDelta = new Vector2(1400f, 40f);
                var cards = (RectTransform)panel.Find("Cards");
                cards.anchoredPosition = new Vector2(0f, -165f);
                cards.sizeDelta = new Vector2(1520f, 600f);
                cards.GetComponent<HorizontalLayoutGroup>().childControlHeight = true;
                cards.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
                foreach (var card in root.GetComponentsInChildren<RoomChoiceCardView>(true)) CompactCard(card);
                var progress = panel.GetComponentInChildren<RoomRouteProgressUI>(true);
                if (progress == null)
                    progress = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ProgressPath), panel))
                        .GetComponent<RoomRouteProgressUI>();
                progress.name = "RoomRouteProgress";
                progress.transform.SetSiblingIndex(cards.GetSiblingIndex());
                progress.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 242f);
                Bind(root.GetComponent<RoomChoiceUI>(), "routeProgress", progress);
                PrefabUtility.SaveAsPrefabAsset(root, ChoicePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            RuntimeUiPrefabLinkEditor.EnsureLink("RoomChoiceUI");
            AssetDatabase.SaveAssets();
        }

        private static void CompactCard(RoomChoiceCardView card)
        {
            var rect = (RectTransform)card.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 600f); Record(rect);
            var marker = (RectTransform)card.transform.Find("SelectionMarker");
            marker.anchorMin = marker.anchorMax = new Vector2(.5f, 1f);
            marker.anchoredPosition = Vector2.zero;
            marker.sizeDelta = new Vector2(24f, 24f); Record(marker);
            Transform content = card.transform.Find("Content");
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding.top = layout.padding.bottom = 28;
            Record(layout);
            SetHeight(content.Find("IconArea"), 146f);
            var icon = content.Find("IconArea/Icon").GetComponent<RectTransform>();
            icon.sizeDelta = new Vector2(146f, 146f); Record(icon);
            SetHeight(content.Find("Kind"), 68f);
            var kind = content.Find("Kind").GetComponent<TMP_Text>(); kind.fontSize = 50f; Record(kind);
            SetHeight(content.Find("Tag"), 56f);
            SetHeight(content.Find("Description"), 74f);
            var description = content.Find("Description").GetComponent<TMP_Text>(); description.fontSize = 28f; Record(description);
            SetHeight(content.Find("Reward"), 56f);
        }

        private static void SetHeight(Transform target, float height)
        {
            var layout = target.GetComponent<LayoutElement>(); layout.preferredHeight = height; Record(layout);
        }
        private static void Record(Object target) => PrefabUtility.RecordPrefabInstancePropertyModifications(target);

        private static void BuildNode(TMP_FontAsset font)
        {
            var root = Rect("RoomRouteProgressNode", null, new Vector2(110f, 150f), Vector2.zero);
            try
            {
                var node = root.gameObject.AddComponent<RoomRouteProgressNode>();
                var background = Picture("Background", root, Art("PixelOctagonFill"), new Vector2(80f, 80f), Vector2.zero);
                background.color = new Color(.045f, .055f, .09f);
                var border = Picture("Border", root, Art("PixelOctagonOutline"), new Vector2(80f, 80f), Vector2.zero);
                border.color = new Color(.58f, .61f, .68f);
                var icon = Picture("Icon", root, null, new Vector2(38f, 38f), Vector2.zero);
                icon.preserveAspect = true;
                var number = Label("Number", root, font, "1", 24f, new Vector2(100f, 28f), new Vector2(0f, -57f));
                var boss = Label("BossLabel", root, font, "BOSS", 14f, new Vector2(70f, 22f), new Vector2(0f, -23f));
                var next = Rect("NextMarker", root, new Vector2(110f, 100f), Vector2.zero);
                var bubble = Picture("Bubble", next, Art("PixelOctagonFill"), new Vector2(100f, 30f), new Vector2(0f, 76f));
                bubble.type = Image.Type.Sliced; bubble.color = new Color(.045f, .055f, .09f);
                var bubbleBorder = Picture("Border", bubble.transform, Art("PixelOctagonOutline"), new Vector2(100f, 30f), Vector2.zero);
                bubbleBorder.type = Image.Type.Sliced; bubbleBorder.color = Yellow;
                var nextLabel = Label("Text", bubble.transform, font, "NEXT", 19f, new Vector2(90f, 26f), Vector2.zero);
                nextLabel.color = Yellow;
                Picture("Pointer", next, Art("PixelDownPointer"), new Vector2(14f, 9f), new Vector2(0f, 51f)).color = Yellow;
                Bind(node, "background", background); Bind(node, "border", border); Bind(node, "icon", icon);
                Bind(node, "number", number); Bind(node, "bossLabel", boss.gameObject); Bind(node, "nextMarker", next.gameObject);
                Bind(node, "checkSprite", Art("PixelCheck"));
                boss.gameObject.SetActive(false); next.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, NodePath);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        private static void BuildProgress(TMP_FontAsset font)
        {
            var root = Rect("RoomRouteProgress", null, new Vector2(1520f, 230f), Vector2.zero);
            try
            {
                var view = root.gameObject.AddComponent<RoomRouteProgressUI>();
                var level = Label("LevelLabel", root, font, "LEVEL 1", 28f, new Vector2(300f, 36f), new Vector2(-610f, 96f));
                level.alignment = TextAlignmentOptions.MidlineLeft;
                var position = Label("PositionLabel", root, font, "NEXT ROOM 1 / 8", 28f, new Vector2(650f, 36f), new Vector2(435f, 96f));
                position.alignment = TextAlignmentOptions.MidlineRight;
                var lineArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/UI/MainMenu/UI_GreyLine.PNG");
                Picture("TopDivider", root, lineArt, new Vector2(1520f, 2f), new Vector2(0f, 124f));
                Picture("BottomDivider", root, lineArt, new Vector2(1520f, 2f), new Vector2(0f, -94f));
                var track = Rect("Track", root, new Vector2(1520f, 170f), new Vector2(0f, -16f));
                var remaining = Picture("RemainingLine", track, null, new Vector2(1330f, 4f), Vector2.zero);
                remaining.color = new Color(.55f, .57f, .64f);
                var completed = Picture("CompletedLine", track, null, new Vector2(0f, 4f), Vector2.zero);
                completed.color = new Color(.27f, .76f, .6f);
                var nodePrefab = AssetDatabase.LoadAssetAtPath<RoomRouteProgressNode>(NodePath);
                int count = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Core/Manager.prefab")
                    .GetComponentInChildren<RoomChoiceDirector>(true).BiomeTemplate.Count;
                var nodes = new RoomRouteProgressNode[count];
                for (int i = 0; i < count; i++)
                {
                    nodes[i] = ((GameObject)PrefabUtility.InstantiatePrefab(nodePrefab.gameObject, track)).GetComponent<RoomRouteProgressNode>();
                    nodes[i].name = "Room_" + (i + 1).ToString("00");
                }
                Bind(view, "levelLabel", level); Bind(view, "positionLabel", position); Bind(view, "track", track);
                Bind(view, "remainingLine", remaining.rectTransform); Bind(view, "completedLine", completed.rectTransform);
                Bind(view, "nodePrefab", nodePrefab);
                using (var data = new SerializedObject(view))
                {
                    var array = data.FindProperty("authoredNodes"); array.arraySize = nodes.Length;
                    for (int i = 0; i < nodes.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
                    var card = AssetDatabase.LoadAssetAtPath<RoomChoiceCardView>("Assets/Project/Prefab/UI/RoomChoiceCard.prefab");
                    using var source = new SerializedObject(card);
                    var src = source.FindProperty("kindIcons"); var dst = data.FindProperty("kindIcons"); dst.arraySize = src.arraySize;
                    for (int i = 0; i < src.arraySize; i++)
                    {
                        dst.GetArrayElementAtIndex(i).FindPropertyRelative("kind").intValue = src.GetArrayElementAtIndex(i).FindPropertyRelative("kind").intValue;
                        dst.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue = src.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue;
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Core/Manager.prefab")
                    .GetComponentInChildren<RoomChoiceDirector>(true).BiomeTemplate;
                view.Bind(template, 0, 1, false);
                foreach (var node in nodes)
                {
                    foreach (var item in node.GetComponentsInChildren<Transform>(true)) Record(item.gameObject);
                    foreach (var component in node.GetComponentsInChildren<Component>(true))
                        if (component != null) Record(component);
                }
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, ProgressPath);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static Image Picture(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position)
        {
            var image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false; return image;
        }
        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, float size, Vector2 dimensions, Vector2 position)
        {
            var label = Rect(name, parent, dimensions, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.characterSpacing = 0f;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; return label;
        }
        private static void Bind(Object component, string field, Object value)
        {
            using var data = new SerializedObject(component);
            data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{name}.png");

        private static void BuildSprites()
        {
            Directory.CreateDirectory(ArtFolder);
            foreach (string name in new[] { "PixelOctagonFill", "PixelOctagonOutline", "PixelCheck", "PixelDownPointer" })
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                var pixels = new Color32[32 * 32];
                bool Inside(int x, int y, int inset) => x >= inset && y >= inset && x < 32 - inset && y < 32 - inset
                    && x + y >= 8 + inset && 31 - x + y >= 8 + inset && x + 31 - y >= 8 + inset && 62 - x - y >= 8 + inset;
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        bool opaque = name == "PixelOctagonFill" ? Inside(x, y, 0)
                            : name == "PixelOctagonOutline" ? Inside(x, y, 0) && !Inside(x, y, 2)
                            : name == "PixelDownPointer" && Mathf.Abs(x - 15.5f) <= y * .5f;
                        if (opaque) pixels[y * 32 + x] = new Color32(255, 255, 255, 255);
                    }
                if (name == "PixelCheck")
                {
                    void Stroke(Vector2 a, Vector2 b)
                    {
                        for (int step = 0; step <= 40; step++)
                        {
                            Vector2 point = Vector2.Lerp(a, b, step / 40f);
                            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                                pixels[(Mathf.RoundToInt(point.y) + dy) * 32 + Mathf.RoundToInt(point.x) + dx] = new Color32(255, 255, 255, 255);
                        }
                    }
                    Stroke(new Vector2(5, 15), new Vector2(12, 8)); Stroke(new Vector2(12, 8), new Vector2(26, 24));
                }
                texture.SetPixels32(pixels); texture.Apply();
                string path = $"{ArtFolder}/{name}.png";
                File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                if (name.StartsWith("PixelOctagon")) importer.spriteBorder = Vector4.one * 10f;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
