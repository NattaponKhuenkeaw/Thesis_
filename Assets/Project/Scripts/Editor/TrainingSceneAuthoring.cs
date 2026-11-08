using System;
using System.Linq;
using CombatCore;
using Project.Progression;
using Project.ShopSystem;
using Project.TeamBuilder.UI.Panels;
using Project.Training;
using Project.UI;
using Project.UI.Training;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TrainingSceneAuthoring
{
    private const string Playground = "Assets/Project/Scenes/Test_Playground.unity";
    private const string Menu = "Assets/Project/Scenes/MainMenu.unity";
    private const string Dummy = "Assets/Project/Character/enemy/enemy/EnemyCore/Enemy_Dummy.prefab";
    private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/GNF TTF SDF.asset");

    [MenuItem("Tools/Project/Training/Author Training Scene")]
    public static void Author()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring Training.");
        EditorSceneManager.SaveOpenScenes();
        string previous = SceneManager.GetActiveScene().path;
        AuthorDummy();
        AuthorMenu();
        AuthorPlayground();
        if (!string.IsNullOrEmpty(previous) && previous != Playground) EditorSceneManager.OpenScene(previous);
        AssetDatabase.SaveAssets();
        Debug.Log("[Training Authoring] MainMenu, passive dummy and Training scene authored. Build settings unchanged.");
    }

    [MenuItem("Tools/Project/Training/Refresh Training UI Layers")]
    public static void AuthorTrainingLoadout()
    {
        if (EditorApplication.isPlaying) return;
        EditorSceneManager.SaveOpenScenes();
        string previous = SceneManager.GetActiveScene().path;
        AuthorPlayground();
        if (!string.IsNullOrEmpty(previous) && previous != Playground) EditorSceneManager.OpenScene(previous);
        Debug.Log("[Training Authoring] Training loadout buttons wired.");
    }

    private static void AuthorDummy()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Dummy);
        try
        {
            var brain = root.GetComponent<EnemyBehaviorBrain>();
            if (brain == null) throw new InvalidOperationException("Dummy is missing its inherited EnemyBehaviorBrain.");
            var so = new SerializedObject(brain);
            so.FindProperty("enableAgentWhenNoBossDriver").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            var agent = root.GetComponent<Unity.Behavior.BehaviorGraphAgent>();
            if (agent == null) throw new InvalidOperationException("Restore the dummy's required graph component before authoring.");
            agent.enabled = false;
            PrefabUtility.SaveAsPrefabAsset(root, Dummy);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void AuthorMenu()
    {
        var scene = EditorSceneManager.OpenScene(Menu);
        var menu = Object.FindFirstObjectByType<MainMenuController>();
        var so = new SerializedObject(menu);
        var play = (Button)so.FindProperty("playButton").objectReferenceValue;
        var settings = (Button)so.FindProperty("settingsButton").objectReferenceValue;
        var quit = (Button)so.FindProperty("quitButton").objectReferenceValue;
        var training = (Button)so.FindProperty("trainingButton").objectReferenceValue;
        if (training == null)
        {
            var go = Object.Instantiate(play.gameObject, play.transform.parent);
            go.name = "TrainingButton";
            go.transform.SetSiblingIndex(play.transform.GetSiblingIndex() + 1);
            training = go.GetComponent<Button>();
            training.onClick = new Button.ButtonClickedEvent();
            var trainingLabel = training.GetComponentInChildren<TMP_Text>(true);
            if (trainingLabel == null)
            {
                var image = training.GetComponent<Image>();
                image.sprite = null;
                image.color = Color.clear;
                trainingLabel = Label(training.transform, "TRAINING", Vector2.zero,
                    ((RectTransform)training.transform).sizeDelta, 64);
                training.targetGraphic = trainingLabel;
            }
            trainingLabel.text = "TRAINING";
            var playRect = (RectTransform)play.transform;
            var trainingRect = (RectTransform)training.transform;
            trainingRect.anchoredPosition = playRect.anchoredPosition + new Vector2(0, -180);
            trainingRect.sizeDelta = new Vector2(500, 90);
            trainingLabel.rectTransform.sizeDelta = trainingRect.sizeDelta;
            trainingLabel.alignment = TextAlignmentOptions.MidlineLeft;
            so.FindProperty("trainingButton").objectReferenceValue = training;
            so.ApplyModifiedPropertiesWithoutUndo();
            var presentation = menu.GetComponent<MainMenuPresentation>();
            if (presentation == null) presentation = Object.FindFirstObjectByType<MainMenuPresentation>();
            if (presentation != null)
            {
                var ps = new SerializedObject(presentation);
                var groups = ps.FindProperty("buttonGroups");
                var old = new CanvasGroup[groups.arraySize];
                for (int i = 0; i < old.Length; i++) old[i] = groups.GetArrayElementAtIndex(i).objectReferenceValue as CanvasGroup;
                var group = training.GetComponent<CanvasGroup>();
                if (group == null) group = training.gameObject.AddComponent<CanvasGroup>();
                groups.arraySize = old.Length + 1;
                for (int i = 0, src = 0; i < groups.arraySize; i++)
                    groups.GetArrayElementAtIndex(i).objectReferenceValue = i == 1 ? group : old[src++];
                ps.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        var buttons = new[] { play, training, settings, quit };
        // Remove only unbound copies left by a previously interrupted authoring run.
        foreach (var duplicate in play.transform.parent.GetComponentsInChildren<Button>(true))
            if (duplicate.name == "TrainingButton" && duplicate != training) Object.DestroyImmediate(duplicate.gameObject);
        for (int i = 0; i < buttons.Length; i++)
        {
            var navigation = buttons[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length];
            navigation.selectOnDown = buttons[(i + 1) % buttons.Length];
            buttons[i].navigation = navigation;
        }
        EditorSceneManager.SaveScene(scene);
    }

    private static void AuthorPlayground()
    {
        var scene = EditorSceneManager.OpenScene(Playground);
        var cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var camera = cameras.FirstOrDefault(c => c.name == "CinemachineCamera_Training" && !PrefabUtility.IsPartOfPrefabInstance(c));
        if (camera == null) camera = cameras.FirstOrDefault(c => c.name == "CinemachineCamera_Training");
        if (camera == null) throw new InvalidOperationException("Scene's authored Training camera was not found.");
        var session = Object.FindFirstObjectByType<TrainingSession>();
        if (session == null) session = new GameObject("TrainingSession").AddComponent<TrainingSession>();
        var manager = Object.FindFirstObjectByType<TrainingDebugManager>();
        if (manager == null) manager = new GameObject("TrainingStatistics").AddComponent<TrainingDebugManager>();
        if (Object.FindFirstObjectByType<TrainingDebugStats>() == null) manager.gameObject.AddComponent<TrainingDebugStats>();
        if (Object.FindFirstObjectByType<TrainingDebugHUD>() == null) manager.gameObject.AddComponent<TrainingDebugHUD>();
        Bind(session, "trainingCamera", camera);
        Bind(session, "debugManager", manager);
        Bind(session, "trainingDebugFont", Font);
        var framing = camera.GetComponent<CinemachineGroupFraming>();
        float size = camera.Lens.OrthographicSize;
        if (framing != null) framing.OrthoSizeRange = new Vector2(size, size);
        camera.Priority = 0;

        var panel = Object.FindFirstObjectByType<TrainingConfigurationPanel>(FindObjectsInactive.Include);
        if (panel == null) panel = BuildPanel();
        LayoutPanel(panel);
        Bind(panel, "session", session);
        Bind(session, "configurationPanel", panel);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static TrainingConfigurationPanel BuildPanel()
    {
        var root = new GameObject("TrainingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;
        root.AddComponent<UIBlockPlayerClick>();
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var controller = root.AddComponent<TrainingConfigurationPanel>();
        var open = Button(root.transform, "Training Configuration", new Vector2(440, -87), new Vector2(300, 52));
        var openRect = (RectTransform)open.transform;
        openRect.anchorMin = openRect.anchorMax = new Vector2(0, 1);
        var reset = Button(root.transform, "Reset Damage", new Vector2(770, -87), new Vector2(300, 48));
        var resetRect = (RectTransform)reset.transform;
        resetRect.anchorMin = resetRect.anchorMax = new Vector2(0, 1);

        var shade = Image(root.transform, "Configuration Overlay", new Color(0, 0, 0, .72f), Vector2.zero, Vector2.zero);
        var shadeRect = (RectTransform)shade.transform;
        shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
        shadeRect.sizeDelta = Vector2.zero;
        var box = Image(shade.transform, "Training Panel", new Color(.055f, .065f, .09f, 1), Vector2.zero, new Vector2(680, 590));
        Label(box.transform, "TRAINING CONFIGURATION", new Vector2(0, 250), new Vector2(620, 40), 32);
        Label(box.transform, "Practice settings only - your save is unchanged", new Vector2(0, 208), new Vector2(620, 30), 21);
        var level = SliderRow(box.transform, "Team Level", 145, out var levelValue);
        var previous = Button(box.transform, "<", new Vector2(-268, 72), new Vector2(56, 42));
        var next = Button(box.transform, ">", new Vector2(268, 72), new Vector2(56, 42));
        var character = Label(box.transform, "Character", new Vector2(0, 72), new Vector2(450, 44), 25);
        var skill = SliderRow(box.transform, "Skill Potential", 10, out var skillValue);
        var ultimate = SliderRow(box.transform, "Ultimate Potential", -55, out var ultimateValue);
        var passive = SliderRow(box.transform, "Passive Potential", -120, out var passiveValue);
        var error = Label(box.transform, "", new Vector2(0, -178), new Vector2(620, 32), 20);
        error.color = new Color(1, .65f, .45f);
        var apply = Button(box.transform, "Apply", new Vector2(-155, -240), new Vector2(260, 52));
        var cancel = Button(box.transform, "Cancel", new Vector2(155, -240), new Vector2(260, 52));
        Bind(controller, "content", shade.gameObject);
        Bind(controller, "openButton", open); Bind(controller, "resetDamageButton", reset);
        Bind(controller, "applyButton", apply); Bind(controller, "cancelButton", cancel);
        Bind(controller, "previousCharacterButton", previous); Bind(controller, "nextCharacterButton", next);
        Bind(controller, "characterLabel", character); Bind(controller, "errorText", error);
        Bind(controller, "teamLevel", level); Bind(controller, "skill", skill);
        Bind(controller, "ultimate", ultimate); Bind(controller, "passive", passive);
        Bind(controller, "teamLevelValue", levelValue); Bind(controller, "skillValue", skillValue);
        Bind(controller, "ultimateValue", ultimateValue); Bind(controller, "passiveValue", passiveValue);
        shade.gameObject.SetActive(false);
        return controller;
    }

    private static Slider SliderRow(Transform parent, string name, float y, out TMP_Text value)
    {
        var caption = Label(parent, name, new Vector2(-216, y), new Vector2(215, 36), 22);
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        var host = new GameObject(name + " Slider", typeof(RectTransform), typeof(Slider));
        host.transform.SetParent(parent, false);
        var rect = (RectTransform)host.transform; rect.sizeDelta = new Vector2(270, 36); rect.anchoredPosition = new Vector2(45, y);
        var track = Image(host.transform, "Track", new Color(.2f, .24f, .3f), Vector2.zero, new Vector2(254, 6));
        var handleArea = new GameObject("Handle Area", typeof(RectTransform)); handleArea.transform.SetParent(host.transform, false);
        var area = (RectTransform)handleArea.transform; area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.sizeDelta = new Vector2(-18, 0);
        var handle = Image(area, "Handle", new Color(.55f, .82f, 1), Vector2.zero, new Vector2(18, -8));
        var slider = host.GetComponent<Slider>(); slider.direction = Slider.Direction.LeftToRight;
        slider.handleRect = (RectTransform)handle.transform; slider.targetGraphic = handle; slider.wholeNumbers = true;
        value = Label(parent, "0 / 0", new Vector2(261, y), new Vector2(100, 36), 22);
        return slider;
    }

    private static Image Image(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
        var image = go.GetComponent<Image>(); image.color = color; return image;
    }

    private static TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = Font; label.fontSize = fontSize;
        label.text = text; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; return label;
    }

    private static Button Button(Transform parent, string text, Vector2 position, Vector2 size)
    {
        var image = Image(parent, text + " Button", new Color(.13f, .19f, .27f), position, size);
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(.65f, .85f, 1); colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        Label(image.transform, text, Vector2.zero, size - new Vector2(10, 0), 24);
        return button;
    }

    private static void Bind(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + field + " was not found.");
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void LayoutPanel(TrainingConfigurationPanel panel)
    {
        var teamCanvas = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/UI/TeamCanvas.prefab");
        if (teamCanvas != null) Bind(panel, "memoryWindowTemplate", teamCanvas.GetComponentInChildren<CharacterPotentialPanel>(true));
        panel.GetComponent<Canvas>().sortingOrder = 6000;
        if (panel.GetComponent<UIBlockPlayerClick>() == null) panel.gameObject.AddComponent<UIBlockPlayerClick>();
        var so = new SerializedObject(panel);
        var stats = (Button)so.FindProperty("statsButton").objectReferenceValue;
        if (stats == null)
        {
            stats = Button(panel.transform, "Stats", new Vector2(1040, -87), new Vector2(180, 48));
            stats.transform.SetSiblingIndex(2);
            Bind(panel, "statsButton", stats);
        }
        var statsRect = (RectTransform)stats.transform;
        statsRect.anchorMin = statsRect.anchorMax = new Vector2(0, 1);
        statsRect.anchoredPosition = new Vector2(970, -87);
        statsRect.sizeDelta = new Vector2(160, 48);
        stats.GetComponentInChildren<TMP_Text>(true).rectTransform.sizeDelta = statsRect.sizeDelta;
        // Only remove the redundant controls created on TrainingCanvas. Player_UI owns loadout navigation.
        foreach (var button in panel.GetComponentsInChildren<Button>(true))
            if (button.transform.parent == panel.transform
                && (button.name == "TeamBuilder Button" || button.name == "Inspector Button"))
                Object.DestroyImmediate(button.gameObject);

        var drawer = panel.GetComponent<TrainingToolsDrawer>();
        if (drawer == null) drawer = panel.gameObject.AddComponent<TrainingToolsDrawer>();
        var toggle = panel.transform.Find("Config Button")?.GetComponent<Button>();
        if (toggle == null) toggle = Button(panel.transform, "Config", Vector2.zero, new Vector2(160, 48));
        var toggleRect = (RectTransform)toggle.transform;
        toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = new Vector2(0, 1);
        toggleRect.anchoredPosition = new Vector2(24, -120);
        toggleRect.sizeDelta = new Vector2(160, 48);
        toggle.GetComponentInChildren<TMP_Text>(true).rectTransform.sizeDelta = toggleRect.sizeDelta;
        var panelTransform = panel.transform.Find("Training Tools");
        var tools = panelTransform != null ? panelTransform.GetComponent<Image>()
            : Image(panel.transform, "Training Tools", new Color(.055f, .065f, .09f, .98f), Vector2.zero, new Vector2(300, 216));
        var toolsRect = (RectTransform)tools.transform;
        toolsRect.anchorMin = toolsRect.anchorMax = toolsRect.pivot = new Vector2(0, 1);
        toolsRect.anchoredPosition = new Vector2(24, -182);
        toolsRect.sizeDelta = new Vector2(300, 216);
        var group = tools.GetComponent<CanvasGroup>();
        if (group == null) group = tools.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
        group.interactable = group.blocksRaycasts = false;
        string[] fields = { "openButton", "resetDamageButton", "statsButton" };
        string[] labels = { "Training Config", "Reset DMG", "Stats" };
        var children = new Button[3];
        for (int i = 0; i < fields.Length; i++)
        {
            var button = (Button)new SerializedObject(panel).FindProperty(fields[i]).objectReferenceValue;
            children[i] = button;
            button.transform.SetParent(tools.transform, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, -40 - i * 68);
            rect.sizeDelta = new Vector2(268, 48);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = labels[i];
            label.rectTransform.sizeDelta = rect.sizeDelta - new Vector2(10, 0);
        }
        for (int i = 0; i < children.Length; i++)
        {
            var navigation = children[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = i == 0 ? toggle : children[i - 1];
            navigation.selectOnDown = i == children.Length - 1 ? toggle : children[i + 1];
            navigation.selectOnLeft = toggle;
            children[i].navigation = navigation;
        }
        var toggleNavigation = toggle.navigation;
        toggleNavigation.mode = Navigation.Mode.Automatic;
        toggle.navigation = toggleNavigation;
        Bind(drawer, "configButton", toggle);
        Bind(drawer, "toolsPanel", toolsRect);
        Bind(drawer, "toolsGroup", group);
        Bind(drawer, "configuration", panel);
        Bind(drawer, "firstButton", children[0]);
        Bind(panel, "toolsDrawer", drawer);
        // Modal must remain above both the drawer and its persistent Config button.
        var modal = (GameObject)new SerializedObject(panel).FindProperty("content").objectReferenceValue;
        if (modal != null)
        {
            modal.transform.SetAsLastSibling();
            var modalCanvasRoot = panel.gameObject.scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == "Training Modal Canvas");
            if (modalCanvasRoot == null)
                modalCanvasRoot = new GameObject("Training Modal Canvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = modalCanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6100;
            var canvasRect = (RectTransform)modalCanvasRoot.transform;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.sizeDelta = Vector2.zero;
            canvasRect.localScale = Vector3.one;
            var scaler = modalCanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            modalCanvasRoot.SetActive(true);
            var nestedCanvas = modal.GetComponent<Canvas>();
            if (nestedCanvas != null) Object.DestroyImmediate(nestedCanvas);
            var nestedRaycaster = modal.GetComponent<GraphicRaycaster>();
            if (nestedRaycaster != null) Object.DestroyImmediate(nestedRaycaster);
            modal.transform.SetParent(modalCanvasRoot.transform, false);
            var modalRect = (RectTransform)modal.transform;
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.sizeDelta = Vector2.zero;
        }
        tools.gameObject.SetActive(false);
        foreach (var slider in panel.GetComponentsInChildren<Slider>(true))
            if (slider.handleRect != null) slider.handleRect.sizeDelta = new Vector2(18, -8);
    }
}
