using System;
using System.IO;
using System.Linq;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.TeamBuilder.UI.Panels;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project.EditorTools
{
    public static class PotentialScreenAuthoring
    {
        private const string PrefabPath = "Assets/Project/Prefab/UI/TeamCanvas.prefab";
        private const string Art = "Assets/Project/UI/MainMenu/";
        private const string Hero = "Assets/Project/Character/Hero/test_0_Main/";
        private const string Reports = "Docs_JJK/3_Outputs/Reports/PotentialScreen";
        private static readonly Color Muted = new Color(0.58f, 0.61f, 0.72f);
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/GNF TTF SDF.asset");

        [MenuItem("Project/UI/Author Potential Screen")]
        public static void Author()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring UI.");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel = root.GetComponentInChildren<CharacterPotentialPanel>(true);
                var data = new SerializedObject(panel);
                var slots = data.FindProperty("nodeSlots");
                var oldView = panel.transform.Find("PotentialPresentation");
                var scroll = panel.transform.Find("Scroll View").GetComponent<ScrollRect>();
                scroll.gameObject.SetActive(true);
                // Restore the authored canvas rather than fitting all twelve nodes onto one screen.
                var content = scroll.content;
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 0);
                content.pivot = new Vector2(0.5f, 0);
                content.sizeDelta = new Vector2(1920, 1080);
                Place((RectTransform)scroll.transform, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.89f));
                content.anchoredPosition = Vector2.zero;
                scroll.horizontal = true; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 40;
                scroll.viewport.gameObject.SetActive(true);
                var board = content.Find("FieldNodePanel");
                board.gameObject.SetActive(true);
                foreach (var layout in board.GetComponents<LayoutGroup>()) layout.enabled = false;
                string[] groups = { "SkillNodePanel", "UltNodePanel", "PassiveNodePanel" };
                string[] names = { "SKILL", "ULTIMATE", "PASSIVE" };
                float[] y = { -893.00415f, -654.1281f, -440.25208f, -226.37604f };
                for (int track = 0; track < 3; track++)
                {
                    var group = board.Find(groups[track]); group.gameObject.SetActive(true);
                    foreach (var layout in group.GetComponents<LayoutGroup>()) layout.enabled = false;
                    foreach (var fitter in group.GetComponents<ContentSizeFitter>()) fitter.enabled = false;
                    var groupRect = (RectTransform)group;
                    groupRect.anchorMin = groupRect.anchorMax = new Vector2(0, 1);
                    groupRect.pivot = new Vector2(0.5f, 0.5f);
                    groupRect.anchoredPosition = new Vector2(319.8507f + track * 639.7014f, -540.2521f);
                    groupRect.sizeDelta = new Vector2(639.7014f, 1080.5042f);
                    if (group.Find("TrackTitle") != null) Object.DestroyImmediate(group.Find("TrackTitle").gameObject);
                    string titleName = names[track] + "TrackTitle";
                    if (panel.transform.Find(titleName) == null)
                        Text(titleName, panel.transform, new Vector2(track / 3f + 0.05f, 0.895f), new Vector2((track + 1) / 3f - 0.05f, 0.94f), names[track], 28, TextAlignmentOptions.Center);
                    for (int level = 0; level < 4; level++)
                    {
                        var slot = (CharacterPotentialNodeUI)slots.GetArrayElementAtIndex(track * 4 + level).objectReferenceValue;
                        slot.transform.SetParent(group, false);
                        var rect = (RectTransform)slot.transform;
                        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                        rect.pivot = new Vector2(0.5f, 0.5f);
                        rect.anchoredPosition = new Vector2(319.8507f, y[level]);
                        rect.sizeDelta = Vector2.one * (level == 0 ? 175 : 125);
                        slot.GetComponent<Image>().pixelsPerUnitMultiplier = 3;
                        var node = new SerializedObject(slot);
                        var label = (TMP_Text)node.FindProperty("stateLabel").objectReferenceValue;
                        label.transform.SetParent(slot.transform, false);
                        Place(label.rectTransform, new Vector2(-0.4f, -0.4f), new Vector2(1.4f, -0.08f));
                        label.fontSize = label.fontSizeMax = 18; label.fontSizeMin = 16;
                        if (slot.transform.Find("Level") == null)
                            Text("Level", slot.transform, new Vector2(-0.55f, 0.25f), new Vector2(-0.1f, 0.75f), new[] { "I", "II", "III", "IV" }[level], 24, TextAlignmentOptions.Center).color = Muted;
                        if (level < 3 && group.Find("Connector" + level) == null)
                        {
                            var line = Image("Connector" + level, group, new Vector2(0.5f, 1), new Vector2(0.5f, 1), null);
                            var lineRect = line.rectTransform;
                            lineRect.anchoredPosition = new Vector2(0, (y[level] + y[level + 1]) / 2);
                            lineRect.sizeDelta = new Vector2(3, 100);
                            line.color = new Color(0.35f, 0.29f, 0.5f); line.transform.SetAsFirstSibling();
                        }
                        slot.gameObject.SetActive(true);
                    }
                }
                var window = panel.GetComponentInChildren<CharacterPotentialUpgradeWindow>(true);
                window.transform.SetParent(panel.transform, false);
                var windowRect = (RectTransform)window.transform;
                windowRect.anchorMin = windowRect.anchorMax = new Vector2(1, 0.5f);
                windowRect.pivot = new Vector2(1, 0.5f);
                windowRect.anchoredPosition = new Vector2(-55, -4.9556f);
                windowRect.sizeDelta = new Vector2(720.7422f, 832.2194f);
                windowRect.SetAsLastSibling();
                var back = panel.transform.Find("ExitButton (1)").GetComponent<Button>();
                var backImage = back.GetComponent<Image>();
                backImage.sprite = Sprite("UI_BlackBox_YellowBorder.PNG"); backImage.type = UnityEngine.UI.Image.Type.Sliced; backImage.color = Color.white; backImage.pixelsPerUnitMultiplier = 3;
                back.gameObject.SetActive(true);
                foreach (Transform child in back.transform) child.gameObject.SetActive(child.name == "exittext (TMP)");
                if (panel.transform.Find("PotentialTitle") == null)
                    Text("PotentialTitle", panel.transform, new Vector2(0.08f, 0.92f), new Vector2(0.7f, 0.99f), "POTENTIAL", 36, TextAlignmentOptions.MidlineLeft);
                var backText = back.GetComponentInChildren<TMP_Text>();
                if (backText != null) { backText.text = "BACK"; backText.font = Font; backText.fontSize = 22; backText.color = Color.white; }
                Set(data, "exitButton", back); Set(data, "characterNameText", null); Set(data, "elementRoleText", null);
                Set(data, "elementIcon", null); Set(data, "elementRoleUI", null); Set(data, "fragmentOwnedText", null);
                Set(data, "contentScrollRect", scroll); Set(data, "nodeRoot", content);
                data.ApplyModifiedPropertiesWithoutUndo();
                if (oldView != null) Object.DestroyImmediate(oldView.gameObject);
                var backdrop = panel.GetComponent<Image>();
                if (backdrop != null) { backdrop.sprite = null; backdrop.overrideSprite = null; backdrop.color = new Color(0.025f, 0.03f, 0.065f, 1); }
                window.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Potential UI] Restored authored scrollable node layout, level I at bottom; removed profile/composition.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name);

        [MenuItem("Project/UI/Capture Potential Screen")]
        public static void Capture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before preview capture.");
            Directory.CreateDirectory(Reports);
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject source = PrefabUtility.LoadPrefabContents(PrefabPath);
            var progression = ScriptableObject.CreateInstance<CharacterProgressionState>();
            var item = ScriptableObject.CreateInstance<CharacterItem>();
            try
            {
                var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(Hero + "CharacterObject_main.asset");
                item.characterObjectData = character; item.SyncFromCharacterObjectData();
                progression.AddFragments(item, 97);
                progression.SetPotentialLevel(character, PotentialTrack.Skill, 1);
                progression.SetPotentialLevel(character, PotentialTrack.Ultimate, 1);
                var canvasObject = new GameObject("Potential Preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.03f, 0.065f); camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var panel = Object.Instantiate(source.GetComponentInChildren<CharacterPotentialPanel>(true), canvas.transform);
                Place((RectTransform)panel.transform, Vector2.zero, Vector2.one);
                var data = new SerializedObject(panel); Set(data, "metaProgression", progression); Set(data, "inspectScreenUI", null); data.ApplyModifiedPropertiesWithoutUndo();
                panel.gameObject.SetActive(true); panel.PopulateData(item, null);
                Render(camera, 1600, 900, "potential-screen.png");
                Render(camera, 1200, 900, "potential-screen-4x3.png");
                panel.SelectNode(progression.GetNextPotentialNode(character, PotentialTrack.Passive));
                Render(camera, 1600, 900, "potential-upgrade.png");
                Render(camera, 1200, 900, "potential-upgrade-4x3.png");
                Debug.Log("[Potential UI] Captured screen and preview at 16:9 and 4:3 using transient progression.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source); EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(item); Object.DestroyImmediate(progression);
            }
        }

        private static void Render(Camera camera, int width, int height, string filename)
        {
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                foreach (var scroll in camera.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ScrollRect>()).Where(scroll => scroll.name == "Scroll View"))
                {
                    scroll.horizontalNormalizedPosition = 0.5f;
                    scroll.verticalNormalizedPosition = 0;
                }
                Canvas.ForceUpdateCanvases();
                foreach (var text in camera.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>())) text.ForceMeshUpdate();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Reports, filename), image.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target); }
        }
        private static void Set(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; Place(rect, min, max); return rect;
        }
        private static void Place(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
        }
        private static Image Image(string name, Transform parent, Vector2 min, Vector2 max, Sprite sprite)
        {
            var image = Rect(name, parent, min, max).gameObject.AddComponent<Image>(); image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false; return image;
        }
        private static TMP_Text Text(string name, Transform parent, Vector2 min, Vector2 max, string value, float size, TextAlignmentOptions alignment)
        {
            var text = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.text = value; text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMin = size * 0.65f; text.fontSizeMax = size;
            text.alignment = alignment; text.color = Color.white; text.raycastTarget = false; return text;
        }
    }
}
