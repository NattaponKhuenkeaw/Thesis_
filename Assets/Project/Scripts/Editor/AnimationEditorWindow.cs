using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using CombatEditor.UI;

/// <summary>
/// Premium SaaS-style Combat Animation Editor built on UI Toolkit.
/// 5-panel layout: Header, Sidebar, Preview, Timeline, Property Cards.
/// </summary>
public class AnimationEditorWindow : EditorWindow
{
    // ── State ──
    private CharacterObject selectedCharacter;
    private int scrubberFrame = 0;
    private bool isPlaying = false;
    private float lastTime = 0f;
    private GameObject previewInstance;

    // ── UI Components ──
    private ActionTreeController treeController;
    private PropertyCardBuilder cardBuilder;
    private TimelineElement timelineElement;

    // ── Element References ──
    private Label breadcrumbLabel;
    private Label frameCounterLabel;
    private Label previewStatusLabel;
    private Label previewClipLabel;
    private VisualElement sidebarScrollContent;
    private VisualElement propertyScrollContent;
    private VisualElement headerCharIcon;
    private Label headerCharName;
    private Slider transportSlider;
    private Label transportFrameLabel;
    private ObjectField clipField;
    private ObjectField charField;

    // =========================================================
    // MENU & ENTRY POINTS
    // =========================================================
    [MenuItem("Window/Animation Editor")]
    public static void ShowWindow()
    {
        var w = GetWindow<AnimationEditorWindow>("⚔ Combat Animation Editor");
        w.minSize = new Vector2(960, 600);
    }

    public static void OpenWindowFromInspector(CharacterObject character)
    {
        var w = GetWindow<AnimationEditorWindow>("⚔ Combat Animation Editor");
        w.minSize = new Vector2(960, 600);
        w.selectedCharacter = character;
        w.Show();
        w.RebuildAll();
    }

    // =========================================================
    // LIFECYCLE
    // =========================================================
    void OnEnable()
    {
        EditorApplication.update += EditorUpdate;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
        CleanupPreview();
    }

    void OnDestroy() => CleanupPreview();

    // =========================================================
    // CREATE GUI (UI TOOLKIT ENTRY)
    // =========================================================
    public void CreateGUI()
    {
        // Load stylesheet
        var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
            "Assets/Project/Scripts/Editor/AnimationEditor.uss");
        if (uss != null)
            rootVisualElement.styleSheets.Add(uss);

        var root = new VisualElement();
        root.AddToClassList("root-container");
        rootVisualElement.Add(root);

        // ── 1. HEADER BAR ──
        root.Add(BuildHeaderBar());

        // ── 2. BODY (3-column) ──
        var body = new VisualElement();
        body.AddToClassList("body-container");

        // LEFT SIDEBAR
        body.Add(BuildSidebar());

        // CENTER AREA
        body.Add(BuildCenterArea());

        // RIGHT PANEL
        body.Add(BuildPropertyPanel());

        root.Add(body);

        // ── Keyboard Shortcuts ──
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
        rootVisualElement.focusable = true;

        // ── Initial Build ──
        if (selectedCharacter != null)
            RebuildAll();
    }

    // =========================================================
    // HEADER BAR
    // =========================================================
    private VisualElement BuildHeaderBar()
    {
        var bar = new VisualElement();
        bar.AddToClassList("header-bar");

        // Left: Character
        var charArea = new VisualElement();
        charArea.AddToClassList("header-character");

        headerCharIcon = new VisualElement();
        headerCharIcon.AddToClassList("header-character__icon");
        charArea.Add(headerCharIcon);

        headerCharName = new Label("No Character");
        headerCharName.AddToClassList("header-character__name");
        charArea.Add(headerCharName);

        bar.Add(charArea);

        // Center: Breadcrumb
        var bcArea = new VisualElement();
        bcArea.AddToClassList("header-breadcrumb");

        var bcPill = new VisualElement();
        bcPill.AddToClassList("header-breadcrumb__pill");

        breadcrumbLabel = new Label("Select a character to begin");
        breadcrumbLabel.AddToClassList("header-breadcrumb__text");
        bcPill.Add(breadcrumbLabel);
        bcArea.Add(bcPill);
        bar.Add(bcArea);

        // Right: Frame Counter
        var fcArea = new VisualElement();
        fcArea.AddToClassList("header-frame-counter");

        var fcPill = new VisualElement();
        fcPill.AddToClassList("header-frame-counter__pill");

        frameCounterLabel = new Label("Frame: 0 / 0");
        frameCounterLabel.AddToClassList("header-frame-counter__text");
        fcPill.Add(frameCounterLabel);
        fcArea.Add(fcPill);
        bar.Add(fcArea);

        return bar;
    }

    // =========================================================
    // LEFT SIDEBAR
    // =========================================================
    private VisualElement BuildSidebar()
    {
        var sidebar = new VisualElement();
        sidebar.AddToClassList("sidebar");

        // Character object field
        charField = new ObjectField("Character");
        charField.objectType = typeof(CharacterObject);
        charField.value = selectedCharacter;
        charField.style.marginLeft = 8;
        charField.style.marginRight = 8;
        charField.style.marginTop = 4;
        charField.style.marginBottom = 8;
        charField.RegisterValueChangedCallback(evt =>
        {
            selectedCharacter = evt.newValue as CharacterObject;
            RebuildAll();
        });
        sidebar.Add(charField);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("sidebar-scroll");

        sidebarScrollContent = new VisualElement();
        scroll.Add(sidebarScrollContent);
        sidebar.Add(scroll);

        // Build tree controller
        treeController = new ActionTreeController(sidebarScrollContent);
        treeController.OnSelectionChanged += OnSidebarSelectionChanged;

        return sidebar;
    }

    // =========================================================
    // CENTER AREA
    // =========================================================
    private VisualElement BuildCenterArea()
    {
        var center = new VisualElement();
        center.AddToClassList("center-area");

        // ── Preview Viewport ──
        var viewport = new VisualElement();
        viewport.AddToClassList("preview-viewport");

        previewStatusLabel = new Label("No preview active");
        previewStatusLabel.AddToClassList("preview-viewport__status");
        viewport.Add(previewStatusLabel);

        previewClipLabel = new Label("");
        previewClipLabel.AddToClassList("preview-viewport__clip-name");
        viewport.Add(previewClipLabel);

        center.Add(viewport);

        // ── Clip Selector ──
        var clipRow = new VisualElement();
        clipRow.AddToClassList("clip-selector-row");

        clipField = new ObjectField("Clip");
        clipField.objectType = typeof(AnimationClip);
        clipField.style.flexGrow = 1;
        clipField.RegisterValueChangedCallback(OnClipChanged);
        clipRow.Add(clipField);
        center.Add(clipRow);

        // ── Timeline Dashboard ──
        var dashboard = new VisualElement();
        dashboard.AddToClassList("timeline-dashboard");

        timelineElement = new TimelineElement();
        timelineElement.AddToClassList("timeline-canvas");
        timelineElement.RegisterCallback<PointerUpEvent>(evt =>
        {
            timelineElement.ReleasePointer(evt.pointerId);
        });
        timelineElement.OnSeek += OnTimelineSeek;
        dashboard.Add(timelineElement);

        // Transport Controls
        dashboard.Add(BuildTransportBar());

        center.Add(dashboard);

        // ── Add Hit Frame Button ──
        var addBtn = new Button(OnAddHitFrame);
        addBtn.text = "＋  Mark Current Frame as Hit Trigger";
        addBtn.AddToClassList("add-hitframe-btn");
        center.Add(addBtn);

        return center;
    }

    // =========================================================
    // TRANSPORT BAR
    // =========================================================
    private VisualElement BuildTransportBar()
    {
        var bar = new VisualElement();
        bar.AddToClassList("transport-bar");

        // Step Back
        var backBtn = new Button(() =>
        {
            scrubberFrame = Mathf.Max(0, scrubberFrame - 1);
            isPlaying = false;
            RefreshTimeline();
        });
        backBtn.text = "◄◄";
        backBtn.AddToClassList("transport-btn");
        bar.Add(backBtn);

        // Play/Pause
        var playBtn = new Button(() =>
        {
            isPlaying = !isPlaying;
            if (isPlaying && selectedCharacter?.previewPrefab != null)
                InstantiatePreviewIfNeeded(selectedCharacter.previewPrefab);
        });
        playBtn.text = "▶";
        playBtn.AddToClassList("transport-btn");
        playBtn.AddToClassList("transport-btn--play");
        bar.Add(playBtn);

        // Step Forward
        var fwdBtn = new Button(() =>
        {
            int max = GetCurrentTotalFrames();
            scrubberFrame = Mathf.Min(max, scrubberFrame + 1);
            isPlaying = false;
            RefreshTimeline();
        });
        fwdBtn.text = "►►";
        fwdBtn.AddToClassList("transport-btn");
        bar.Add(fwdBtn);

        // Frame Label
        transportFrameLabel = new Label("Frame: 0 / 0");
        transportFrameLabel.AddToClassList("transport-frame-label");
        bar.Add(transportFrameLabel);

        // Slider
        transportSlider = new Slider(0, 1);
        transportSlider.AddToClassList("transport-slider");
        transportSlider.RegisterValueChangedCallback(evt =>
        {
            if (!isPlaying)
            {
                int max = GetCurrentTotalFrames();
                scrubberFrame = Mathf.RoundToInt(evt.newValue * max);
                RefreshTimeline();
            }
        });
        bar.Add(transportSlider);

        // Cleanup Preview Button
        var cleanBtn = new Button(CleanupPreview);
        cleanBtn.text = "✕";
        cleanBtn.tooltip = "Cleanup Scene Preview";
        cleanBtn.AddToClassList("transport-btn");
        bar.Add(cleanBtn);

        return bar;
    }

    // =========================================================
    // RIGHT PROPERTY PANEL
    // =========================================================
    private VisualElement BuildPropertyPanel()
    {
        var panel = new VisualElement();
        panel.AddToClassList("property-panel");

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("property-scroll");

        propertyScrollContent = new VisualElement();
        scroll.Add(propertyScrollContent);
        panel.Add(scroll);

        cardBuilder = new PropertyCardBuilder(propertyScrollContent, OnDataChanged);

        return panel;
    }

    // =========================================================
    // REBUILD ALL
    // =========================================================
    private void RebuildAll()
    {
        if (charField != null)
            charField.SetValueWithoutNotify(selectedCharacter);

        // Header
        if (headerCharName != null)
            headerCharName.text = selectedCharacter != null ? selectedCharacter.characterId ?? selectedCharacter.name : "No Character";

        // Sidebar
        if (treeController != null && selectedCharacter != null)
        {
            treeController.Build(selectedCharacter);
        }

        // Cards
        if (cardBuilder != null)
        {
            cardBuilder.SetCharacter(selectedCharacter);
            if (treeController?.SelectedNode != null)
                cardBuilder.Build(treeController.SelectedNode);
        }

        RefreshTimeline();
    }

    // =========================================================
    // EVENT HANDLERS
    // =========================================================
    private void OnSidebarSelectionChanged(ActionNode node)
    {
        scrubberFrame = 0;
        isPlaying = false;

        // Update clip field
        AnimationClip clip = GetClipFromNode(node);
        if (clipField != null)
            clipField.SetValueWithoutNotify(clip);

        // Update breadcrumb
        if (breadcrumbLabel != null && node != null)
            breadcrumbLabel.text = $"Moveset > {node.Category} > {node.Label}";

        // Rebuild cards
        if (cardBuilder != null)
        {
            cardBuilder.SetCharacter(selectedCharacter);
            cardBuilder.Build(node);
        }

        RefreshTimeline();
    }

    private void OnClipChanged(ChangeEvent<Object> evt)
    {
        var node = treeController?.SelectedNode;
        if (node == null || selectedCharacter == null) return;

        Undo.RecordObject(selectedCharacter, "Change Clip");

        if (node.DataType == "AttackStep" && node.DataRef is AttackStepData step)
            step.animationClip = evt.newValue as AnimationClip;
        else if (node.DataType == "Dash" && node.DataRef is DashData dash)
            dash.animationClip = evt.newValue as AnimationClip;

        EditorUtility.SetDirty(selectedCharacter);
        RefreshTimeline();
    }

    private void OnTimelineSeek(int frame)
    {
        scrubberFrame = frame;
        isPlaying = false;
        RefreshTimeline();
    }

    private void OnAddHitFrame()
    {
        var node = treeController?.SelectedNode;
        if (node?.DataType != "AttackStep" || !(node.DataRef is AttackStepData step)) return;

        Undo.RecordObject(selectedCharacter, "Add Hit Frame");

        int offset = scrubberFrame - step.startupFrames;
        if (offset < 0) offset = 0;

        var newFrame = new AttackHitFrameData
        {
            startFrameOffset = offset,
            activeFrames = 1
        };

        var list = step.hitFrames != null
            ? new List<AttackHitFrameData>(step.hitFrames)
            : new List<AttackHitFrameData>();
        list.Add(newFrame);
        step.hitFrames = list.ToArray();

        EditorUtility.SetDirty(selectedCharacter);
        cardBuilder?.Build(node);
        RefreshTimeline();
    }

    private void OnDataChanged()
    {
        if (selectedCharacter != null)
            EditorUtility.SetDirty(selectedCharacter);
        RefreshTimeline();

        // Rebuild cards to reflect changes
        if (treeController?.SelectedNode != null)
            cardBuilder?.Build(treeController.SelectedNode);
    }

    // =========================================================
    // KEYBOARD SHORTCUTS
    // =========================================================
    private void OnKeyDown(KeyDownEvent evt)
    {
        int max = GetCurrentTotalFrames();

        if (evt.keyCode == KeyCode.LeftArrow)
        {
            scrubberFrame = Mathf.Max(0, scrubberFrame - 1);
            isPlaying = false;
            RefreshTimeline();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.RightArrow)
        {
            scrubberFrame = Mathf.Min(max, scrubberFrame + 1);
            isPlaying = false;
            RefreshTimeline();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Space)
        {
            isPlaying = !isPlaying;
            evt.StopPropagation();
        }
    }

    // =========================================================
    // EDITOR UPDATE (Auto-Play)
    // =========================================================
    private void EditorUpdate()
    {
        if (isPlaying)
        {
            AnimationClip clip = GetCurrentClip();
            if (clip != null)
            {
                float dt = (float)EditorApplication.timeSinceStartup - lastTime;
                scrubberFrame += Mathf.RoundToInt(dt * 24f);

                int total = GetTotalClipFrames(clip);
                if (scrubberFrame >= total)
                {
                    scrubberFrame = 0;
                    isPlaying = false;
                }
                RefreshTimeline();
            }
        }
        lastTime = (float)EditorApplication.timeSinceStartup;
    }

    // =========================================================
    // TIMELINE REFRESH
    // =========================================================
    private void RefreshTimeline()
    {
        if (timelineElement == null) return;

        var node = treeController?.SelectedNode;
        int totalFrames = 0;
        int su = 0, act = 0, rec = 0;

        if (node?.DataType == "AttackStep" && node.DataRef is AttackStepData step)
        {
            su = step.startupFrames;
            act = step.activeFrames;
            rec = step.recoveryFrames;
            totalFrames = step.animationClip != null ? GetTotalClipFrames(step.animationClip) : su + act + rec;

            timelineElement.HitMarkers.Clear();
            if (step.hitFrames != null)
            {
                foreach (var h in step.hitFrames)
                {
                    timelineElement.HitMarkers.Add(new TimelineElement.HitMarker
                    {
                        start = su + h.startFrameOffset,
                        duration = h.activeFrames,
                        color = h.debugColor
                    });
                }
            }

            timelineElement.HasCancel = true;
            timelineElement.CancelStart = su + act + step.cancelWindowStartFrame;
            timelineElement.HasMotion = step.useForwardMotion;
            timelineElement.MotionStart = step.motionStartFrame;
            timelineElement.MotionEnd = step.motionEndFrame;
        }
        else if (node?.DataType == "Dash" && node.DataRef is DashData dash)
        {
            su = dash.startupFrames;
            act = dash.activeFrames;
            rec = dash.recoveryFrames;
            totalFrames = dash.animationClip != null ? GetTotalClipFrames(dash.animationClip) : su + act + rec;

            timelineElement.HitMarkers.Clear();
            timelineElement.HasCancel = false;
            timelineElement.HasMotion = false;
        }
        else
        {
            timelineElement.HitMarkers.Clear();
            timelineElement.HasCancel = false;
            timelineElement.HasMotion = false;
        }

        timelineElement.TotalFrames = totalFrames;
        timelineElement.StartupFrames = su;
        timelineElement.ActiveFrames = act;
        timelineElement.RecoveryFrames = rec;
        timelineElement.PlayheadFrame = scrubberFrame;
        timelineElement.Refresh();

        // Update frame counter
        if (frameCounterLabel != null)
            frameCounterLabel.text = $"Frame: {scrubberFrame} / {totalFrames}";
        if (transportFrameLabel != null)
            transportFrameLabel.text = $"Frame: {scrubberFrame} / {totalFrames}";
        if (transportSlider != null && totalFrames > 0)
            transportSlider.SetValueWithoutNotify((float)scrubberFrame / totalFrames);

        // Update preview
        AnimationClip clip = GetCurrentClip();
        if (previewStatusLabel != null)
        {
            previewStatusLabel.text = clip != null ? "Previewing in Scene View" : "No clip assigned";
        }
        if (previewClipLabel != null)
        {
            previewClipLabel.text = clip != null ? $"{clip.name} — {GetTotalClipFrames(clip)}f @ {Mathf.RoundToInt(clip.frameRate)}fps" : "";
        }

        // Sample animation
        if (!Application.isPlaying && selectedCharacter?.previewPrefab != null && clip != null)
        {
            InstantiatePreviewIfNeeded(selectedCharacter.previewPrefab);
            SampleAnimation(clip, scrubberFrame);
        }
    }

    // =========================================================
    // DATA HELPERS
    // =========================================================
    private AnimationClip GetCurrentClip()
    {
        var node = treeController?.SelectedNode;
        return GetClipFromNode(node);
    }

    private AnimationClip GetClipFromNode(ActionNode node)
    {
        if (node == null) return null;
        if (node.DataType == "AttackStep" && node.DataRef is AttackStepData step)
            return step.animationClip;
        if (node.DataType == "Dash" && node.DataRef is DashData dash)
            return dash.animationClip;
        return null;
    }

    private int GetCurrentTotalFrames()
    {
        var node = treeController?.SelectedNode;
        if (node?.DataType == "AttackStep" && node.DataRef is AttackStepData step)
            return step.animationClip != null ? GetTotalClipFrames(step.animationClip) : step.startupFrames + step.activeFrames + step.recoveryFrames;
        if (node?.DataType == "Dash" && node.DataRef is DashData dash)
            return dash.animationClip != null ? GetTotalClipFrames(dash.animationClip) : dash.startupFrames + dash.activeFrames + dash.recoveryFrames;
        return 0;
    }

    private AttackStepData GetCurrentStep()
    {
        var node = treeController?.SelectedNode;
        if (node?.DataType == "AttackStep")
            return node.DataRef as AttackStepData;
        return null;
    }

    private int GetTotalClipFrames(AnimationClip clip)
    {
        return Mathf.RoundToInt(clip.length * 24f);
    }

    // =========================================================
    // PREVIEW & ANIMATION
    // =========================================================
    private void InstantiatePreviewIfNeeded(GameObject prefab)
    {
        if (previewInstance == null && prefab != null)
        {
            previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (previewInstance == null) previewInstance = Instantiate(prefab);
            previewInstance.hideFlags = HideFlags.HideAndDontSave;
            previewInstance.transform.position = Vector3.zero;
        }
    }

    private void SampleAnimation(AnimationClip clip, int frame)
    {
        if (clip == null || previewInstance == null) return;
        if (!AnimationMode.InAnimationMode())
            AnimationMode.StartAnimationMode();

        float time = frame / 24f;
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(previewInstance, clip, time);
        AnimationMode.EndSampling();
        SceneView.RepaintAll();
    }

    private void CleanupPreview()
    {
        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();
        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }
    }

    // =========================================================
    // SCENE VIEW HANDLES (Preserved from IMGUI version)
    // =========================================================
    private void OnSceneGUI(SceneView sceneView)
    {
        if (selectedCharacter == null || previewInstance == null) return;

        AttackStepData step = GetCurrentStep();
        if (step == null || step.hitFrames == null) return;

        bool changed = false;

        for (int i = 0; i < step.hitFrames.Length; i++)
        {
            AttackHitFrameData h = step.hitFrames[i];
            int startAt = step.startupFrames + h.startFrameOffset;
            int endAt = startAt + h.activeFrames;

            if (scrubberFrame >= startAt && scrubberFrame < endAt)
            {
                Vector3 worldPos = previewInstance.transform.TransformPoint(new Vector3(h.offset.x, h.offset.y, 0));
                Color fillColor = h.debugColor;
                fillColor.a = 0.3f;
                Handles.color = fillColor;

                Vector3 halfSize = new Vector3(h.size.x * 0.5f, h.size.y * 0.5f, 0);
                Quaternion rot = previewInstance.transform.rotation;

                Vector3 p1 = worldPos + rot * new Vector3(-halfSize.x, -halfSize.y, 0);
                Vector3 p2 = worldPos + rot * new Vector3(halfSize.x, -halfSize.y, 0);
                Vector3 p3 = worldPos + rot * new Vector3(halfSize.x, halfSize.y, 0);
                Vector3 p4 = worldPos + rot * new Vector3(-halfSize.x, halfSize.y, 0);

                Handles.DrawSolidRectangleWithOutline(new Vector3[] { p1, p2, p3, p4 }, fillColor, h.debugColor);

                // Positional handle
                EditorGUI.BeginChangeCheck();
                Handles.Label(worldPos + Vector3.up * (h.size.y * 0.6f), "Impact #" + (i + 1), EditorStyles.miniBoldLabel);
                float handleSize = 0.05f * HandleUtility.GetHandleSize(worldPos);
                Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, handleSize, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedCharacter, "Move Hitbox");
                    Vector3 localPos = previewInstance.transform.InverseTransformPoint(newWorldPos);
                    h.offset = new Vector2(localPos.x, localPos.y);
                    changed = true;
                }

                // Scaling handles
                const float minSize = 0.1f;
                Handles.color = Color.white;

                // Right
                Vector3 rightWorld = worldPos + rot * new Vector3(halfSize.x, 0, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newRight = Handles.FreeMoveHandle(rightWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedCharacter, "Scale Hitbox");
                    Vector3 lp = previewInstance.transform.InverseTransformPoint(newRight);
                    float oldLeft = h.offset.x - (h.size.x * 0.5f);
                    float nw = lp.x - oldLeft;
                    if (nw < minSize) nw = minSize;
                    h.size.x = nw; h.offset.x = oldLeft + (nw * 0.5f);
                    changed = true;
                }

                // Left
                Vector3 leftWorld = worldPos + rot * new Vector3(-halfSize.x, 0, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newLeft = Handles.FreeMoveHandle(leftWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedCharacter, "Scale Hitbox");
                    Vector3 lp = previewInstance.transform.InverseTransformPoint(newLeft);
                    float oldRight = h.offset.x + (h.size.x * 0.5f);
                    float nw = oldRight - lp.x;
                    if (nw < minSize) nw = minSize;
                    h.size.x = nw; h.offset.x = oldRight - (nw * 0.5f);
                    changed = true;
                }

                // Top
                Vector3 topWorld = worldPos + rot * new Vector3(0, halfSize.y, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newTop = Handles.FreeMoveHandle(topWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedCharacter, "Scale Hitbox");
                    Vector3 lp = previewInstance.transform.InverseTransformPoint(newTop);
                    float oldBot = h.offset.y - (h.size.y * 0.5f);
                    float nh = lp.y - oldBot;
                    if (nh < minSize) nh = minSize;
                    h.size.y = nh; h.offset.y = oldBot + (nh * 0.5f);
                    changed = true;
                }

                // Bottom
                Vector3 botWorld = worldPos + rot * new Vector3(0, -halfSize.y, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newBot = Handles.FreeMoveHandle(botWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedCharacter, "Scale Hitbox");
                    Vector3 lp = previewInstance.transform.InverseTransformPoint(newBot);
                    float oldTop = h.offset.y + (h.size.y * 0.5f);
                    float nh = oldTop - lp.y;
                    if (nh < minSize) nh = minSize;
                    h.size.y = nh; h.offset.y = oldTop - (nh * 0.5f);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(selectedCharacter);
            RefreshTimeline();
        }
    }
}
