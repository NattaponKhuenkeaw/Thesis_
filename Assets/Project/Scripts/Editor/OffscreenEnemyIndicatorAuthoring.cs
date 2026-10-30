#if UNITY_EDITOR
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OffscreenEnemyIndicatorAuthoring
{
    public const string MarkerPath = "Assets/Project/Prefab/UI/EnemyDirectionIndicator.prefab";
    public const string HudPath = "Assets/Project/Prefab/UI/OffscreenEnemyIndicators.prefab";
    public const string PlayerHudPath = "Assets/Project/Prefab/CombatUI/Player_UI.prefab";
    private static readonly Color Yellow = new Color(1f, .9f, .22f);

    public static void Build()
    {
        var marker = Rect("EnemyDirectionIndicator", null, new Vector2(40f, 40f));
        try
        {
            var plate = marker.gameObject.AddComponent<Image>();
            plate.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/UI/MainMenu/UI_BlackBox_YellowBorder_NoOutLine.PNG");
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 4f;
            plate.raycastTarget = false;
            var arrow = Text("Pointer", marker, ">", 34f, Yellow, new Vector2(36f, 36f));
            var counter = Rect("Count", marker, new Vector2(26f, 20f));
            counter.anchoredPosition = new Vector2(19f, -17f);
            var background = counter.gameObject.AddComponent<Image>();
            background.color = new Color(.045f, .055f, .095f, 1f);
            background.raycastTarget = false;
            var count = Text("Number", counter, "2", 20f, Color.white, new Vector2(26f, 20f));
            counter.gameObject.SetActive(false);
            var view = marker.gameObject.AddComponent<EnemyDirectionIndicator>();
            var data = new SerializedObject(view);
            data.FindProperty("pointer").objectReferenceValue = arrow.rectTransform;
            data.FindProperty("countRoot").objectReferenceValue = counter.gameObject;
            data.FindProperty("countText").objectReferenceValue = count;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(marker.gameObject, MarkerPath);
        }
        finally { Object.DestroyImmediate(marker.gameObject); }

        var hud = Rect("OffscreenEnemyIndicators", null, Vector2.zero);
        try
        {
            Stretch(hud);
            var group = hud.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var controller = hud.gameObject.AddComponent<OffscreenEnemyIndicators>();
            var data = new SerializedObject(controller);
            data.FindProperty("markersRoot").objectReferenceValue = hud;
            data.FindProperty("markerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(MarkerPath).GetComponent<EnemyDirectionIndicator>();
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, HudPath);
        }
        finally { Object.DestroyImmediate(hud.gameObject); }

        foreach (var path in new[] { PlayerHudPath })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas == null) throw new System.InvalidOperationException("HUD requires a canvas: " + path);
                if (root.GetComponentInChildren<OffscreenEnemyIndicators>(true) == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath), canvas.transform);
                    Stretch((RectTransform)instance.transform);
                    instance.transform.SetAsFirstSibling();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[EnemyDirections] Saved authored indicator prefabs and linked player HUDs.");
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, Vector2 dimensions)
    {
        var rect = Rect(name, parent, dimensions);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/GNF TTF SDF.asset");
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }
}
#endif
