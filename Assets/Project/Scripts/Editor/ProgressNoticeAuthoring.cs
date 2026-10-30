#if UNITY_EDITOR
using Project.Core.Lobby;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ProgressNoticeAuthoring
{
    public const string LobbyPath = "Assets/Project/Resources/UI/LobbyLevelBanner.prefab";
    public const string BossPath = "Assets/Project/Resources/UI/BossDeathPromptUI.prefab";
    private const string Art = "Assets/Project/UI/MainMenu/";
    private static readonly Color Yellow = new Color(1f, .9f, .22f);

    public static void Build()
    {
        BuildLobby();
        BuildBoss();
        AssetDatabase.SaveAssets();
        Debug.Log("[ProgressNotices] Saved editable lobby level and boss completion prefabs.");
    }

    private static void BuildLobby()
    {
        var root = CanvasRoot("LobbyLevelBanner", 8000);
        try
        {
            var panel = Panel(root.transform, new Vector2(520f, 156f), .78f);
            Label(panel, "EnteringLabel", "ENTERING", 20f, new Color(.65f, .67f, .73f), new Vector2(440f, 30f), 44f);
            var title = Label(panel, "LevelLabel", "LEVEL 1", 48f, Color.white, new Vector2(440f, 66f), -5f);
            Line(panel, 340f, -50f);
            var view = root.AddComponent<LobbyLevelBanner>();
            using (var data = new SerializedObject(view))
            {
                data.FindProperty("presentation").objectReferenceValue = root.GetComponent<CanvasGroup>();
                data.FindProperty("levelLabel").objectReferenceValue = title;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, LobbyPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void BuildBoss()
    {
        var root = CanvasRoot("BossDeathPromptUI", 520);
        try
        {
            var panel = Panel(root.transform, new Vector2(760f, 180f), .72f);
            Label(panel, "TitleLabel", "BOSS DEFEATED", 40f, Yellow, new Vector2(680f, 56f), 43f);
            Line(panel, 600f, 0f);
            var prompt = Label(panel, "PromptLabel", "Left-click to continue", 28f, Color.white, new Vector2(680f, 48f), -43f);
            var view = root.AddComponent<BossDeathPromptUI>();
            using (var data = new SerializedObject(view))
            {
                data.FindProperty("canvas").objectReferenceValue = root.GetComponent<Canvas>();
                data.FindProperty("promptLabel").objectReferenceValue = prompt;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static GameObject CanvasRoot(string name, int sortingOrder)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        root.layer = LayerMask.NameToLayer("UI");
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        var group = root.GetComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        return root;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = LayerMask.NameToLayer("UI");
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Panel(Transform parent, Vector2 size, float height)
    {
        var panel = Rect("NoticePanel", parent, size);
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, height);
        var frame = panel.gameObject.AddComponent<Image>();
        frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "UI_BlackBox_YellowBorder.PNG");
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.5f;
        frame.raycastTarget = false;
        return panel;
    }

    private static TMP_Text Label(Transform parent, string name, string value, float size, Color color, Vector2 dimensions, float y)
    {
        var rect = Rect(name, parent, dimensions);
        rect.anchoredPosition = Vector2.up * y;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/GNF TTF SDF.asset");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * .65f;
        text.fontSizeMax = size;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Line(Transform parent, float width, float y)
    {
        var rect = Rect("Separator", parent, new Vector2(width, 2f));
        rect.anchoredPosition = Vector2.up * y;
        var line = rect.gameObject.AddComponent<Image>();
        line.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "UI_GreyLine.PNG");
        line.raycastTarget = false;
    }
}
#endif
