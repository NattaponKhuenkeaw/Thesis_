using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using CombatCore;
using static CombatCore.FrameDataConstants;
using System.Collections.Generic;
using CombatEditor.UI;

/// <summary>
/// Premium NLE-style Combat Animation Editor built on UI Toolkit.
/// 3-column resizable layout via nested TwoPaneSplitView:
///   Header (free-sizing) → [Left Sidebar | Center Timeline | Right Property Cards]
/// </summary>
public class AnimationEditorWindow : EditorWindow
{
    // ── State ──
    private CharacterObject selectedCharacter;
    private int scrubberFrame = 0;
    private bool isPlaying = false;
    private float lastTime = 0f;
    private GameObject previewInstance;
    private int selectedSceneHitboxIndex = -1;
    private Dictionary<VFXEntry, GameObject> vfxPreviewInstances = new Dictionary<VFXEntry, GameObject>();
    
    // Persistent State
    private object lastSelectedDataRef;

    // ── UI Components ──
    private ActionTreeController treeController;
    private PropertyCardBuilder cardBuilder;
    private TimelineGridElement timelineGrid;

    // ── Element References ──
    private Label breadcrumbLabel;
    private Label frameCounterLabel;
    private Label previewStatusLabel;
    private Label previewClipLabel;
    private VisualElement sidebarScrollContent;
    private VisualElement propertyScrollContent;
    private VisualElement headerCharIcon;
    private Label headerCharName;
    private Label transportFrameLabel;
    private ObjectField clipField;
    private ObjectField charField;
    private GUIStyle sceneHitboxLabelStyle;

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
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
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

        // ── 1. HEADER BAR (free-sizing, sits above split) ──
        root.Add(BuildHeaderBar());

        // ── 2. BODY — Nested TwoPaneSplitView for 3 columns ──
        // Outer split: Left Sidebar | (Center + Right)
        var outerSplit = new TwoPaneSplitView(0, 220, TwoPaneSplitViewOrientation.Horizontal);
        outerSplit.AddToClassList("body-container");

        // LEFT SIDEBAR
        outerSplit.Add(BuildSidebar());

        // Inner split: Center Area | Right Panel
        var innerSplit = new TwoPaneSplitView(1, 300, TwoPaneSplitViewOrientation.Horizontal);

        // CENTER AREA
        innerSplit.Add(BuildCenterArea());

        // RIGHT PANEL
        innerSplit.Add(BuildPropertyPanel());

        outerSplit.Add(innerSplit);
        root.Add(outerSplit);

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

        center.Add(BuildViewport());
        center.Add(BuildClipSelector());
        center.Add(BuildTimelineArea());

        return center;
    }

    private VisualElement BuildViewport()
    {
        var viewport = new VisualElement();
        viewport.AddToClassList("preview-viewport");

        previewStatusLabel = new Label("No preview active");
        previewStatusLabel.AddToClassList("preview-viewport__status");
        viewport.Add(previewStatusLabel);

        previewClipLabel = new Label("");
        previewClipLabel.AddToClassList("preview-viewport__clip-name");
        viewport.Add(previewClipLabel);

        return viewport;
    }

    private VisualElement BuildClipSelector()
    {
        var clipRow = new VisualElement();
        clipRow.AddToClassList("clip-selector-row");

        clipField = new ObjectField("Clip");
        clipField.objectType = typeof(AnimationClip);
        clipField.style.flexGrow = 1;
        clipField.RegisterValueChangedCallback(OnClipChanged);
        clipRow.Add(clipField);

        return clipRow;
    }

    private VisualElement BuildTimelineArea()
    {
        var dashboard = new VisualElement();
        dashboard.AddToClassList("timeline-dashboard");

        timelineGrid = new TimelineGridElement();
        timelineGrid.AddToClassList("timeline-canvas");
        timelineGrid.OnSeek += OnTimelineSeek;
        dashboard.Add(timelineGrid);

        dashboard.Add(BuildTransportBar());
        return dashboard;
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

        cardBuilder = new PropertyCardBuilder(propertyScrollContent, treeController, OnDataChanged, RebuildSelectedNode);

        return panel;
    }

    // =========================================================
    // REBUILD ALL
    // =========================================================
    private void RebuildAll()
    {
        selectedSceneHitboxIndex = -1;

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
        bool isSameData = node != null && lastSelectedDataRef == node.DataRef;
        
        if (!isSameData)
        {
            scrubberFrame = 0;
            isPlaying = false;
            selectedSceneHitboxIndex = -1;
        }
        
        lastSelectedDataRef = node?.DataRef;

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

        if (selectedCharacter?.previewPrefab != null)
            InstantiatePreviewIfNeeded(selectedCharacter.previewPrefab);

        RefreshTimeline();
    }

    private void OnClipChanged(ChangeEvent<Object> evt)
    {
        var node = treeController?.SelectedNode;
        if (node == null || selectedCharacter == null) return;

        Undo.RecordObject(selectedCharacter, "Change Clip");

        if (node.DataType == "AttackStep" && node.DataRef is AttackStepData step)
            step.animationClip = evt.newValue as AnimationClip;
        else if (node.DataType == "ComboNode" && node.DataRef is ComboNode cnode)
            cnode.stepData.animationClip = evt.newValue as AnimationClip;
        else if (node.DataType == "Dash" && node.DataRef is DashData dash)
            dash.animationClip = evt.newValue as AnimationClip;

        EditorUtility.SetDirty(selectedCharacter);
        RebuildSelectedNode();
    }

    private void OnTimelineSeek(int frame)
    {
        scrubberFrame = frame;
        isPlaying = false;
        RefreshTimeline();
    }

    private void OnDataChanged()
    {
        if (selectedCharacter != null)
            EditorUtility.SetDirty(selectedCharacter);
        
        // Refresh grid visuals (safe, does not destroy focus)
        RefreshTimeline();
    }

    private void RebuildSelectedNode()
    {
        var node = treeController?.SelectedNode;
        object dataRef = node?.DataRef;

        if (treeController != null && selectedCharacter != null)
        {
            treeController.Build(selectedCharacter);
            
            // Re-select to restore object references if they changed
            if (dataRef != null) treeController.SelectNodeByData(dataRef);
        }

        node = treeController?.SelectedNode; // Get updated node reference

        if (cardBuilder != null)
        {
            cardBuilder.SetCharacter(selectedCharacter);
            cardBuilder.Build(node);
        }

        RefreshTimeline();
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
        else if (evt.ctrlKey && evt.keyCode == KeyCode.Z)
        {
            Undo.PerformUndo();
            evt.StopPropagation();
        }
        else if (evt.ctrlKey && evt.shiftKey && evt.keyCode == KeyCode.Z)
        {
            Undo.PerformRedo();
            evt.StopPropagation();
        }
        else if (evt.ctrlKey && evt.keyCode == KeyCode.Y)
        {
            Undo.PerformRedo();
            evt.StopPropagation();
        }
    }

    private void OnUndoRedo()
    {
        RebuildAll();
        Repaint();
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
                scrubberFrame += Mathf.RoundToInt(dt * FrameDataConstants.LOGIC_FPS);

                int total = GetCurrentTotalFrames();
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
    // TIMELINE REFRESH — Component-synced track system
    // =========================================================
    // Unifying Frame Rate Constants
    private static readonly Color MotionTimelineColor = Color.white;

    private void RefreshTimeline()
    {
        if (timelineGrid == null) return;

        var node = treeController?.SelectedNode;
        AnimationClip clip = GetCurrentClip();
        int totalFrames = 0;
        int su = 0, act = 0, rec = 0;
        int cancel = 0;

        // Clear tracks
        timelineGrid.Tracks.Clear();

        var step = node?.GetStepData();

        // Update header/breadcrumb to reflect any name changes (Fast path)
        if (breadcrumbLabel != null && node != null)
        {
            string label = step != null ? step.stepName : node.Label;
            breadcrumbLabel.text = $"Moveset > {node.Category} > {label}";
        }
        if (step != null)
        {
            su = step.startupFrames;
            act = step.activeFrames;
            rec = step.recoveryFrames;
            cancel = step.cancelWindowStartFrame;
            int phaseTotal = su + act + rec;
            int clipFrames = clip != null ? Mathf.CeilToInt(clip.length * LOGIC_FPS) : 0;
            totalFrames = Mathf.Max(phaseTotal, clipFrames);

            timelineGrid.Setup(totalFrames, LOGIC_FPS);

            // ── Build tracks from attached components ──

            foreach (string componentKey in step.GetOrderedComponentKeys())
            {
                switch (componentKey)
                {
                    case AttackStepData.MotionComponentKey when step.motionData != null
                        && step.motionData.keyframes != null
                        && step.motionData.keyframes.Count > 0:
                    {
                        var motionTrack = new TimelineGridElement.TimelineTrack
                        {
                            label = "Motion",
                            color = MotionTimelineColor,
                            drawRanges = true,
                            diamondColor = MotionTimelineColor,
                            diamondSize = 5f
                        };
                        foreach (var kf in step.motionData.keyframes)
                        {
                            if (kf.startFrame == kf.endFrame)
                                motionTrack.diamonds.Add(kf.startFrame);
                            else
                                motionTrack.ranges.Add((kf.startFrame, kf.endFrame - kf.startFrame));
                        }
                        timelineGrid.Tracks.Add(motionTrack);
                        break;
                    }

                    case AttackStepData.VfxComponentKey when step.vfxData != null && step.vfxData.entries != null:
                    {
                        foreach (var entry in step.vfxData.entries)
                        {
                            var vfxTrack = new TimelineGridElement.TimelineTrack
                            {
                                label = entry.label,
                                color = entry.trackColor
                            };
                            vfxTrack.ranges.Add((entry.triggerFrame, entry.duration));
                            timelineGrid.Tracks.Add(vfxTrack);
                        }
                        break;
                    }

                    case AttackStepData.AudioComponentKey when step.audioData != null && step.audioData.entries != null:
                    {
                        foreach (var audioEntry in step.audioData.entries)
                        {
                            var audioTrack = new TimelineGridElement.TimelineTrack
                            {
                                label = audioEntry.label,
                                color = audioEntry.trackColor,
                                drawRanges = true,
                                diamondColor = audioEntry.trackColor,
                                diamondSize = 4f
                            };
                            // Show a diamond at the trigger frame and a range for duration
                            audioTrack.diamonds.Add(audioEntry.triggerFrame);
                            audioTrack.ranges.Add((audioEntry.triggerFrame, audioEntry.duration));
                            timelineGrid.Tracks.Add(audioTrack);
                        }
                        break;
                    }

                    case AttackStepData.HitboxComponentKey when step.hitData != null && step.hitData.hitboxes != null:
                    {
                        Dictionary<string, int> hitboxTypeCounts = new Dictionary<string, int>();
                        for (int i = 0; i < step.hitData.hitboxes.Count; i++)
                        {
                            var hb = step.hitData.hitboxes[i];
                            if (hb == null)
                            {
                                continue;
                            }

                            hitboxTypeCounts.TryGetValue(hb.HitboxTypeName, out int typeIndex);
                            typeIndex++;
                            hitboxTypeCounts[hb.HitboxTypeName] = typeIndex;

                            var hitTrack = new TimelineGridElement.TimelineTrack
                            {
                                label = $"{hb.HitboxTypeName} #{typeIndex}",
                                color = hb.TimelineTrackColor
                            };
                            int hitStart = su + hb.startFrameOffset;
                            hitTrack.ranges.Add((hitStart, hb.activeFrames));
                            timelineGrid.Tracks.Add(hitTrack);
                        }
                        break;
                    }

                    case AttackStepData.EventComponentKey when step.eventData != null && step.eventData.entries != null:
                    {
                        foreach (var eventEntry in step.eventData.entries)
                        {
                            if (eventEntry == null)
                            {
                                continue;
                            }

                            var eventTrack = new TimelineGridElement.TimelineTrack
                            {
                                label = eventEntry.label,
                                color = eventEntry.trackColor
                            };
                            eventTrack.ranges.Add((eventEntry.triggerFrame, eventEntry.GetTimelineDurationFrames(step.TotalFrames)));
                            timelineGrid.Tracks.Add(eventTrack);
                        }
                        break;
                    }
                }
            }
        }
        else if (node?.DataType == "Dash" && node.DataRef is DashData dash)
        {
            su = dash.startupFrames;
            act = dash.activeFrames;
            rec = dash.recoveryFrames;
            cancel = dash.cancelWindowStartFrame;
            int phaseTotal = su + act + rec;
            int clipFrames = clip != null ? Mathf.CeilToInt(clip.length * LOGIC_FPS) : 0;
            totalFrames = Mathf.Max(phaseTotal, clipFrames);

            timelineGrid.Setup(totalFrames, LOGIC_FPS);

            var dashWindowTrack = new TimelineGridElement.TimelineTrack
            {
                label = "Dash Window",
                color = new Color(0.1f, 0.6f, 1f) // Sky blue for dash
            };
            dashWindowTrack.ranges.Add((su, act));
            timelineGrid.Tracks.Add(dashWindowTrack);
        }
        else
        {
            timelineGrid.Setup(1, FrameDataConstants.LOGIC_FPS);
        }

        timelineGrid.StartupFrames = su;
        timelineGrid.ActiveFrames = act;
        timelineGrid.RecoveryFrames = rec;
        timelineGrid.CancelFrame = cancel;
        timelineGrid.PlayheadFrame = scrubberFrame;

        timelineGrid.Refresh();

        // Update frame counter
        if (frameCounterLabel != null)
            frameCounterLabel.text = $"Frame: {scrubberFrame} / {totalFrames}";
        if (transportFrameLabel != null)
            transportFrameLabel.text = $"Frame: {scrubberFrame} / {totalFrames}";

        // Update preview
        if (previewStatusLabel != null)
        {
            previewStatusLabel.text = selectedCharacter?.previewPrefab == null
                ? "Assign a Preview Prefab to edit hitboxes in Scene View"
                : clip != null ? "Scene Preview active - drag hitbox handles to edit" : "Scene Preview ready - drag hitbox handles to edit";
        }
        if (previewClipLabel != null)
        {
            previewClipLabel.text = clip != null ? $"{clip.name} — {GetTotalClipFrames(clip)}f @ {Mathf.RoundToInt(clip.frameRate)}fps" : "";
        }

        // Sample animation and update VFX
        if (!Application.isPlaying && selectedCharacter?.previewPrefab != null)
        {
            InstantiatePreviewIfNeeded(selectedCharacter.previewPrefab);
            
            // Logic Frame wrapping for looping
            int logicFrame = scrubberFrame;
            if (step != null)
                logicFrame = step.GetLogicFrame(scrubberFrame);

            if (clip != null)
            {
                SampleAnimation(clip, logicFrame);
            }
            else
            {
                SceneView.RepaintAll();
            }

            // Update VFX even if no clip is assigned (they have their own trigger frames)
            if (step != null && step.vfxData != null)
                UpdateVFXPreviews(step, scrubberFrame);
            else
                CleanupVfxPreviews();
        }
        else
        {
            CleanupVfxPreviews();
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
        var step = node.GetStepData();
        if (step != null) return step.animationClip;
        
        if (node.DataType == "Dash" && node.DataRef is DashData dash)
            return dash.animationClip;
        return null;
    }

    private int GetCurrentTotalFrames()
    {
        var node = treeController?.SelectedNode;
        if (node?.DataType == "AttackStep" && node.DataRef is AttackStepData step)
        {
            int phaseTotal = step.startupFrames + step.activeFrames + step.recoveryFrames;
            int clipFrames = step.animationClip != null ? Mathf.CeilToInt(step.animationClip.length * FrameDataConstants.LOGIC_FPS) : 0;
            return Mathf.Max(phaseTotal, clipFrames);
        }
        if (node?.DataType == "Dash" && node.DataRef is DashData dash)
        {
            int phaseTotal = dash.startupFrames + dash.activeFrames + dash.recoveryFrames;
            int clipFrames = dash.animationClip != null ? Mathf.CeilToInt(dash.animationClip.length * FrameDataConstants.LOGIC_FPS) : 0;
            return Mathf.Max(phaseTotal, clipFrames);
        }
        return 0;
    }

    private AttackStepData GetCurrentStep()
    {
        return treeController?.SelectedNode?.GetStepData();
    }

    private int GetTotalClipFrames(AnimationClip clip)
    {
        return Mathf.RoundToInt(clip.length * clip.frameRate);
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

        float time = frame / FrameDataConstants.LOGIC_FPS;
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(previewInstance, clip, time);
        AnimationMode.EndSampling();

        // Evaluate and visualize Runtime MotionData Offset
        AttackStepData step = GetCurrentStep();
        if (step != null && step.motionData != null)
        {
            Vector3 offset = EvaluateMotionOffset(step.motionData, frame);
            previewInstance.transform.position = Vector3.zero + offset;
        }

        SceneView.RepaintAll();
    }

    // =========================================================
    // VFX PREVIEW LOGIC
    // =========================================================
    private void UpdateVFXPreviews(AttackStepData step, int absoluteFrame)
    {
        if (step?.vfxData == null)
        {
            CleanupVfxPreviews();
            return;
        }

        int logicFrame = step.GetLogicFrame(absoluteFrame);

        HashSet<VFXEntry> currentEntries = new HashSet<VFXEntry>();

        foreach (var entry in step.vfxData.entries)
        {
            if (entry == null || (entry.vfxPrefab == null && entry.vfxType == Project.Combat.VFXType.None && entry.vfxClip == null)) continue;

            // Determine if VFX should be active
            bool shouldBeActive = false;
            int relativeFrame = -1;

            if (step.loopActivePhase && entry.loopDuringActive && absoluteFrame >= step.ActivePhaseStart)
            {
                // If it's a looping VFX, it follows the logic frame if it was triggered during the active phase
                if (entry.triggerFrame >= step.ActivePhaseStart && entry.triggerFrame <= step.ActivePhaseEnd)
                {
                    shouldBeActive = true;
                    relativeFrame = logicFrame - entry.triggerFrame;
                    // Handle case where trigger is late in the loop: if relative is negative, it means it hasn't 
                    // reached the trigger frame in the current iteration yet.
                    if (relativeFrame < 0) shouldBeActive = false;
                    else if (relativeFrame >= entry.duration) shouldBeActive = false;
                }
            }
            
            // Fallback to absolute check if not looping or not in active phase
            if (!shouldBeActive)
            {
                if (absoluteFrame >= entry.triggerFrame && absoluteFrame < entry.triggerFrame + entry.duration)
                {
                    shouldBeActive = true;
                    relativeFrame = absoluteFrame - entry.triggerFrame;
                }
            }

            if (shouldBeActive)
            {
                currentEntries.Add(entry);
                GameObject instance = GetOrCreateVfxInstance(entry);
                if (instance != null)
                {
                    UpdateVfxTransform(instance, entry, relativeFrame);
                    SampleVfxState(instance, entry, relativeFrame);
                }
            }
        }

        // Cleanup stale instances
        List<VFXEntry> toRemove = new List<VFXEntry>();
        foreach (var kvp in vfxPreviewInstances)
        {
            if (!currentEntries.Contains(kvp.Key))
            {
                if (kvp.Value != null) DestroyImmediate(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove) vfxPreviewInstances.Remove(key);
    }

    private GameObject GetOrCreateVfxInstance(VFXEntry entry)
    {
        if (vfxPreviewInstances.TryGetValue(entry, out GameObject instance) && instance != null)
            return instance;

        GameObject prefab = entry.vfxPrefab;
        // Fallback to library generic prefab if direct prefab is null
        if (prefab == null && (entry.vfxType != Project.Combat.VFXType.None || entry.vfxClip != null) && Project.Combat.VFXManager.Instance != null)
        {
            var generic = Project.Combat.VFXManager.Instance.VFXPrefab;
            if (generic != null) prefab = generic.gameObject;
        }

        if (prefab == null) return null;

        instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance == null) instance = Instantiate(prefab);
        
        instance.hideFlags = HideFlags.HideAndDontSave;
        
        // Parent to preview instance if it exists for relative positioning
        if (previewInstance != null)
            instance.transform.SetParent(previewInstance.transform, false);

        vfxPreviewInstances[entry] = instance;
        return instance;
    }

    private void UpdateVfxTransform(GameObject instance, VFXEntry entry, int relativeFrame)
    {
        if (instance == null || entry == null) return;

        (Vector3 pos, Vector3 rot, Vector3 scl) = SampleVFXTransform(entry, relativeFrame);
        
        // If library-based, multiply scale by definition's localScale
        if (entry.vfxPrefab == null && entry.vfxType != Project.Combat.VFXType.None && Project.Combat.VFXManager.Instance?.InstanceLibrary != null)
        {
            var def = Project.Combat.VFXManager.Instance.InstanceLibrary.GetDefinition(entry.vfxType);
            if (def != null)
            {
                scl = Vector3.Scale(scl, def.localScale == Vector3.zero ? Vector3.one : def.localScale);
            }
        }

        instance.transform.localPosition = pos;
        instance.transform.localEulerAngles = rot;
        instance.transform.localScale = scl;
    }

    private (Vector3 pos, Vector3 rot, Vector3 scl) SampleVFXTransform(VFXEntry entry, int relativeFrame)
    {
        if (entry.transformKeys == null || entry.transformKeys.Count == 0)
            return (Vector3.zero, Vector3.zero, Vector3.one);

        VFXTransformKey prev = null;
        VFXTransformKey next = null;

        foreach (var key in entry.transformKeys)
        {
            if (key.frame <= relativeFrame)
            {
                if (prev == null || key.frame > prev.frame) prev = key;
            }
            else
            {
                if (next == null || key.frame < next.frame) next = key;
            }
        }

        if (prev != null && next != null)
        {
            float t = (float)(relativeFrame - prev.frame) / (next.frame - prev.frame);
            return (
                Vector3.Lerp(prev.position, next.position, t),
                Vector3.Lerp(prev.rotation, next.rotation, t),
                Vector3.Lerp(prev.scale, next.scale, t)
            );
        }
        else if (prev != null)
        {
            return (prev.position, prev.rotation, prev.scale);
        }

        return (Vector3.zero, Vector3.zero, Vector3.one);
    }

    private void SampleVfxState(GameObject instance, VFXEntry entry, int relativeFrame)
    {
        float time = relativeFrame / FrameDataConstants.LOGIC_FPS;

        if (entry.vfxClip != null && entry.vfxType == Project.Combat.VFXType.None)
        {
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(instance, entry.vfxClip, time);
            AnimationMode.EndSampling();
            return;
        }

        // 1. Process Animator if present
        var anim = instance.GetComponent<Animator>();
        if (anim == null) anim = instance.GetComponentInChildren<Animator>();
        
        if (anim != null)
        {
            // If it's a library-based VFX, we need to apply the clip from the definition
            if (entry.vfxPrefab == null && entry.vfxType != Project.Combat.VFXType.None && Project.Combat.VFXManager.Instance?.InstanceLibrary != null)
            {
                var def = Project.Combat.VFXManager.Instance.InstanceLibrary.GetDefinition(entry.vfxType);
                if (def != null)
                {
                    if (def.clip != null)
                    {
                        // Ensure AnimatorOverrideController is set up
                        var overrideController = anim.runtimeAnimatorController as AnimatorOverrideController;
                        if (overrideController == null || overrideController.name != "VFX_Editor_Override")
                        {
                            overrideController = new AnimatorOverrideController(anim.runtimeAnimatorController);
                            overrideController.name = "VFX_Editor_Override";
                            anim.runtimeAnimatorController = overrideController;
                        }
                        
                        if (overrideController["VFX_Default"] != def.clip)
                        {
                            overrideController["VFX_Default"] = def.clip;
                        }
                    }

                    // Apply Tint to SpriteRenderer
                    var renderer = instance.GetComponent<SpriteRenderer>();
                    if (renderer == null) renderer = instance.GetComponentInChildren<SpriteRenderer>();
                    if (renderer != null)
                    {
                        renderer.color = (def.tint.a <= 0f) ? Color.white : def.tint;
                    }
                }
            }

            if (anim.runtimeAnimatorController != null)
            {
                if (!AnimationMode.InAnimationMode())
                    AnimationMode.StartAnimationMode();

                // If we have a clip from the library, sample it directly
                if (entry.vfxPrefab == null && entry.vfxType != Project.Combat.VFXType.None)
                {
                    var def = Project.Combat.VFXManager.Instance?.InstanceLibrary?.GetDefinition(entry.vfxType);
                    if (def != null && def.clip != null)
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(instance, def.clip, time);
                        AnimationMode.EndSampling();
                    }
                }
                else
                {
                    // Fallback: Just update the animator (might not work perfectly in scrubber but better than nothing)
                    AnimationMode.BeginSampling();
                    anim.Update(0f);
                    AnimationMode.EndSampling();
                }
            }
        }

        // 2. Process ParticleSystems if present
        var particleSystems = instance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particleSystems)
        {
            // Reset and simulate forward to evaluate properly
            ps.Simulate(time, true, true, false);
        }
    }

    private void CleanupVfxPreviews()
    {
        foreach (var kvp in vfxPreviewInstances)
        {
            if (kvp.Value != null) DestroyImmediate(kvp.Value);
        }
        vfxPreviewInstances.Clear();
    }

    private Vector3 EvaluateMotionOffset(MotionFrameData motion, int currentFrame)
    {
        if (motion == null || motion.keyframes == null || motion.keyframes.Count == 0)
            return Vector3.zero;

        Vector3 totalOffset = Vector3.zero;
        foreach (var kf in motion.keyframes)
        {
            if (currentFrame < kf.startFrame) continue;

            float t = 1f;
            if (kf.endFrame > kf.startFrame)
            {
                t = Mathf.Clamp01((float)(currentFrame - kf.startFrame) / (kf.endFrame - kf.startFrame));
            }

            if (kf.taskType == MotionTaskType.Relative || kf.taskType == MotionTaskType.SnapToEnemy)
            {
                totalOffset += Vector3.Lerp(Vector3.zero, kf.position, t);
            }
            else if (kf.taskType == MotionTaskType.Absolute)
            {
                totalOffset = Vector3.Lerp(Vector3.zero, kf.position, t);
            }
        }
        return totalOffset;
    }

    private void DrawMotionTrajectory(MotionFrameData motion)
    {
        if (motion == null || motion.keyframes == null || motion.keyframes.Count == 0) return;
        
        Handles.color = Color.cyan;
        int maxFrames = GetCurrentTotalFrames();
        
        Vector3 anchor = Vector3.zero;
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i <= maxFrames; i++)
        {
            Vector3 offset = EvaluateMotionOffset(motion, i);
            points.Add(anchor + offset);
        }
        
        Handles.DrawAAPolyLine(4f, points.ToArray());
        
        // Emphasize the current frame position
        Vector3 currentOffset = EvaluateMotionOffset(motion, scrubberFrame);
        Handles.color = Color.yellow;
        Handles.DrawSolidDisc(anchor + currentOffset, Vector3.back, 0.04f * HandleUtility.GetHandleSize(anchor + currentOffset));
    }

    private void DrawVFXGizmos(VFXFrameData vfxData)
    {
        if (vfxData == null || vfxData.entries == null) return;

        foreach (var entry in vfxData.entries)
        {
            if (entry == null) continue;

            // 1. Determine spawn position (relative to player position at trigger frame)
            Vector3 playerOffsetAtTrigger = EvaluateMotionOffset(GetCurrentStep()?.motionData, entry.triggerFrame);
            Vector3 spawnBasePos = playerOffsetAtTrigger;
            
            Vector3 vfxLocalOffset = entry.transformKeys.Count > 0 ? entry.transformKeys[0].position : Vector3.zero;
            Vector3 spawnWorldPos = spawnBasePos + vfxLocalOffset;

            // 2. Draw Trajectory if multiple keys exist
            if (entry.transformKeys.Count > 1)
            {
                DrawVFXTrajectory(entry, spawnBasePos);
            }

            // 3. Draw Marker
            bool isTriggerFrame = scrubberFrame == entry.triggerFrame;
            bool isVisible = scrubberFrame >= entry.triggerFrame && scrubberFrame < entry.triggerFrame + entry.duration;
            
            Handles.color = entry.trackColor;
            float size = HandleUtility.GetHandleSize(spawnWorldPos) * 0.15f;
            
            if (isTriggerFrame)
            {
                Handles.DrawSolidDisc(spawnWorldPos, Vector3.back, size * 1.2f);
                Handles.color = Color.white;
                Handles.DrawWireDisc(spawnWorldPos, Vector3.back, size * 1.3f);
                Handles.Label(spawnWorldPos + Vector3.up * size * 2f, $"SPAWN: {entry.label}", GetSceneHitboxLabelStyle(true));
            }
            else
            {
                // Non-trigger frames: draw a small disc to show it's active
                float markerSize = size * 0.6f;
                if (isVisible)
                {
                    Handles.color = new Color(entry.trackColor.r, entry.trackColor.g, entry.trackColor.b, 0.4f);
                    Handles.DrawSolidDisc(spawnWorldPos, Vector3.back, markerSize);
                }
                
                Handles.color = entry.trackColor;
                Handles.DrawWireDisc(spawnWorldPos, Vector3.back, markerSize);
            }
        }
    }

    private void DrawVFXTrajectory(VFXEntry entry, Vector3 basePos)
    {
        if (entry.transformKeys == null || entry.transformKeys.Count < 2) return;

        Handles.color = new Color(entry.trackColor.r, entry.trackColor.g, entry.trackColor.b, 0.5f);
        List<Vector3> points = new List<Vector3>();

        // Sort keys by frame just in case
        var sortedKeys = new List<VFXTransformKey>(entry.transformKeys);
        sortedKeys.Sort((a, b) => a.frame.CompareTo(b.frame));

        for (int i = 0; i < sortedKeys.Count; i++)
        {
            points.Add(basePos + sortedKeys[i].position);
        }

        Handles.DrawAAPolyLine(2f, points.ToArray());
        
        // Draw dots at keys
        foreach (var key in sortedKeys)
        {
            Handles.DrawSolidDisc(basePos + key.position, Vector3.back, HandleUtility.GetHandleSize(basePos + key.position) * 0.03f);
        }
    }

    private void CleanupPreview()
    {
        CleanupVfxPreviews();

        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();
        selectedSceneHitboxIndex = -1;
        if (previewInstance != null)
        {
            // Guard against MissingReferenceException in Inspector by deselecting the object before destruction
            if (Selection.activeGameObject != null && (Selection.activeGameObject == previewInstance || Selection.activeGameObject.transform.IsChildOf(previewInstance.transform)))
            {
                Selection.activeGameObject = null;
            }

            DestroyImmediate(previewInstance);
            previewInstance = null;
        }
    }

    // =========================================================
    // SCENE VIEW HANDLES — Hitbox gizmos for NormalHitbox
    // =========================================================
    private void OnSceneGUI(SceneView sceneView)
    {
        if (selectedCharacter == null || previewInstance == null) return;

        AttackStepData step = GetCurrentStep();
        if (step == null) return;

        if (step.eventData?.entries != null)
            foreach (var cameraEvent in step.eventData.entries)
                if (cameraEvent != null && cameraEvent.eventType == ActionEventType.ActionCameraMotion
                    && cameraEvent.cameraMotionSettings != null && scrubberFrame >= cameraEvent.triggerFrame)
                {
                    cameraEvent.cameraMotionSettings.Sample(scrubberFrame - cameraEvent.triggerFrame, out float size, out float offset);
                    Handles.Label(previewInstance.transform.position + Vector3.up * 4f,
                        $"Camera size x{size:F3} / horizontal {offset:P2}");
                }
        
        if (step.hitData != null && step.hitData.hitboxes != null && selectedSceneHitboxIndex >= step.hitData.hitboxes.Count)
            selectedSceneHitboxIndex = -1;

        bool changed = false;
        CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = CompareFunction.Always;

        try
        {
            DrawSceneHitboxOverlay();
            
            // Draw Motion Simulation path
            if (step.motionData != null)
                DrawMotionTrajectory(step.motionData);

            // Draw VFX Simulation points
            if (step.vfxData != null)
                DrawVFXGizmos(step.vfxData);

            if (step.hitData == null || step.hitData.hitboxes == null || step.hitData.hitboxes.Count == 0) return;

            Dictionary<string, int> hitboxTypeCounts = new Dictionary<string, int>();
            for (int i = 0; i < step.hitData.hitboxes.Count; i++)
            {
                HitboxBase hitbox = step.hitData.hitboxes[i];
                if (hitbox == null) continue;

                hitboxTypeCounts.TryGetValue(hitbox.HitboxTypeName, out int typeIndex);
                typeIndex++;
                hitboxTypeCounts[hitbox.HitboxTypeName] = typeIndex;

                int startAt = step.startupFrames + hitbox.startFrameOffset;
                int endAt = startAt + Mathf.Max(1, hitbox.activeFrames) - 1;
                bool isActive = scrubberFrame >= startAt && scrubberFrame <= endAt;
                if (!isActive) continue;

                bool isSelected = selectedSceneHitboxIndex == i || (selectedSceneHitboxIndex < 0 && isActive);
                string hitboxLabel = $"{hitbox.HitboxTypeName} #{typeIndex}";

                if (hitbox is NormalHitbox normalHitbox)
                {
                    changed |= DrawRectHitboxHandle(i, hitboxLabel, startAt, endAt, isActive, isSelected, normalHitbox.debugColor, () => normalHitbox.offset, value => normalHitbox.offset = value, () => normalHitbox.size, value => normalHitbox.size = value);
                }
                else if (hitbox is GrabHitbox grabHitbox)
                {
                    changed |= DrawRectHitboxHandle(i, hitboxLabel, startAt, endAt, isActive, isSelected, grabHitbox.debugColor, () => grabHitbox.offset, value => grabHitbox.offset = value, () => grabHitbox.size, value => grabHitbox.size = value);
                }
                else if (hitbox is ProjectileSpawnHitbox projHitbox)
                {
                    changed |= DrawRectHitboxHandle(i, hitboxLabel, startAt, endAt, isActive, isSelected, projHitbox.debugColor, 
                        () => projHitbox.ResolveSpawnOffset(), 
                        value => { 
                            projHitbox.overrideSpawnOffset = true; 
                            projHitbox.spawnOffset = value; 
                        }, 
                        () => new Vector2(0.5f, 0.5f), 
                        value => { } // Ignore resize
                    );
                }
            }
        }
        finally
        {
            Handles.zTest = previousZTest;
        }

        if (changed)
        {
            OnDataChanged();
        }
    }

    private void DrawSceneHitboxOverlay()
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(14f, 14f, 280f, 72f), EditorStyles.helpBox);
        GUILayout.Label("Hitbox Scene Preview", EditorStyles.boldLabel);
        GUILayout.Label("Drag the center handle to move a hitbox. Drag the edge handles to resize it. Bright labels are active on the current frame.", EditorStyles.wordWrappedMiniLabel);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private bool DrawRectHitboxHandle(int hitboxIndex, string label, int startFrame, int endFrame, bool isActive, bool isSelected, Color debugColor, System.Func<Vector2> getOffset, System.Action<Vector2> setOffset, System.Func<Vector2> getSize, System.Action<Vector2> setSize)
    {
        bool changed = false;
        Vector2 offset = getOffset();
        Vector2 size = getSize();
        size.x = Mathf.Max(0.1f, size.x);
        size.y = Mathf.Max(0.1f, size.y);

        Vector3 worldPos = HitboxLocalToWorld(offset);
        Color fillColor = debugColor;
        fillColor.a = isSelected ? 0.28f : isActive ? 0.22f : 0.08f;
        Color outlineColor = debugColor;
        outlineColor.a = isSelected ? 1f : isActive ? 0.95f : 0.45f;
        Handles.color = outlineColor;

        Vector3 halfSize = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
        Vector3 p1 = HitboxLocalToWorld(offset + new Vector2(-halfSize.x, -halfSize.y));
        Vector3 p2 = HitboxLocalToWorld(offset + new Vector2(halfSize.x, -halfSize.y));
        Vector3 p3 = HitboxLocalToWorld(offset + new Vector2(halfSize.x, halfSize.y));
        Vector3 p4 = HitboxLocalToWorld(offset + new Vector2(-halfSize.x, halfSize.y));
        Handles.DrawSolidRectangleWithOutline(new Vector3[] { p1, p2, p3, p4 }, fillColor, outlineColor);

        Handles.color = isSelected ? Color.yellow : isActive ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.8f);
        Vector3 labelPosition = HitboxLocalToWorld(offset + new Vector2(0f, (size.y * 0.6f) + 0.15f));
        Handles.Label(labelPosition, $"{label}\n{startFrame}-{endFrame}", GetSceneHitboxLabelStyle(isActive || isSelected));

        float handleSize = HandleUtility.GetHandleSize(worldPos) * (isSelected ? 0.095f : isActive ? 0.085f : 0.07f);
        if (Handles.Button(worldPos, Quaternion.identity, handleSize, handleSize * 1.15f, Handles.RectangleHandleCap))
        {
            selectedSceneHitboxIndex = hitboxIndex;
            SceneView.RepaintAll();
        }

        if (isSelected || isActive)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, handleSize * 0.85f, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selectedCharacter, "Move Hitbox");
                Vector3 localPos = previewInstance.transform.InverseTransformPoint(newWorldPos);
                setOffset(new Vector2(localPos.x, localPos.y));
                offset = getOffset();
                selectedSceneHitboxIndex = hitboxIndex;
                changed = true;
            }

            changed |= ResizeRectHitbox(hitboxIndex, handleSize * 0.8f, offset, size, getOffset, setOffset, getSize, setSize);
        }

        return changed;
    }

    private bool ResizeRectHitbox(int hitboxIndex, float handleSize, Vector2 offset, Vector2 size, System.Func<Vector2> getOffset, System.Action<Vector2> setOffset, System.Func<Vector2> getSize, System.Action<Vector2> setSize)
    {
        bool changed = false;
        const float minSize = 0.1f;

        Vector3 halfSize = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
        Handles.color = Color.white;

        changed |= ResizeHorizontalHandle(hitboxIndex, HitboxLocalToWorld(offset + new Vector2(halfSize.x, 0f)), handleSize, minSize, true, getOffset, setOffset, getSize, setSize);
        changed |= ResizeHorizontalHandle(hitboxIndex, HitboxLocalToWorld(offset + new Vector2(-halfSize.x, 0f)), handleSize, minSize, false, getOffset, setOffset, getSize, setSize);
        changed |= ResizeVerticalHandle(hitboxIndex, HitboxLocalToWorld(offset + new Vector2(0f, halfSize.y)), handleSize, minSize, true, getOffset, setOffset, getSize, setSize);
        changed |= ResizeVerticalHandle(hitboxIndex, HitboxLocalToWorld(offset + new Vector2(0f, -halfSize.y)), handleSize, minSize, false, getOffset, setOffset, getSize, setSize);

        return changed;
    }

    private bool ResizeHorizontalHandle(int hitboxIndex, Vector3 handleWorld, float handleSize, float minSize, bool isRightSide, System.Func<Vector2> getOffset, System.Action<Vector2> setOffset, System.Func<Vector2> getSize, System.Action<Vector2> setSize)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 movedHandle = Handles.FreeMoveHandle(handleWorld, handleSize, Vector3.zero, Handles.RectangleHandleCap);
        if (!EditorGUI.EndChangeCheck()) return false;

        Undo.RecordObject(selectedCharacter, "Scale Hitbox");
        Vector2 offset = getOffset();
        Vector2 size = getSize();
        Vector3 localPoint = previewInstance.transform.InverseTransformPoint(movedHandle);

        float anchor = isRightSide ? offset.x - (size.x * 0.5f) : offset.x + (size.x * 0.5f);
        float width = isRightSide ? localPoint.x - anchor : anchor - localPoint.x;
        width = Mathf.Max(minSize, width);

        offset.x = isRightSide ? anchor + (width * 0.5f) : anchor - (width * 0.5f);
        size.x = width;
        setOffset(offset);
        setSize(size);
        selectedSceneHitboxIndex = hitboxIndex;
        return true;
    }

    private bool ResizeVerticalHandle(int hitboxIndex, Vector3 handleWorld, float handleSize, float minSize, bool isTopSide, System.Func<Vector2> getOffset, System.Action<Vector2> setOffset, System.Func<Vector2> getSize, System.Action<Vector2> setSize)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 movedHandle = Handles.FreeMoveHandle(handleWorld, handleSize, Vector3.zero, Handles.RectangleHandleCap);
        if (!EditorGUI.EndChangeCheck()) return false;

        Undo.RecordObject(selectedCharacter, "Scale Hitbox");
        Vector2 offset = getOffset();
        Vector2 size = getSize();
        Vector3 localPoint = previewInstance.transform.InverseTransformPoint(movedHandle);

        float anchor = isTopSide ? offset.y - (size.y * 0.5f) : offset.y + (size.y * 0.5f);
        float height = isTopSide ? localPoint.y - anchor : anchor - localPoint.y;
        height = Mathf.Max(minSize, height);

        offset.y = isTopSide ? anchor + (height * 0.5f) : anchor - (height * 0.5f);
        size.y = height;
        setOffset(offset);
        setSize(size);
        selectedSceneHitboxIndex = hitboxIndex;
        return true;
    }

    private Vector3 HitboxLocalToWorld(Vector2 localPoint)
    {
        return previewInstance.transform.TransformPoint(new Vector3(localPoint.x, localPoint.y, 0f));
    }

    private GUIStyle GetSceneHitboxLabelStyle(bool isActive)
    {
        sceneHitboxLabelStyle ??= new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            padding = new RectOffset(6, 6, 4, 4),
            richText = false
        };

        sceneHitboxLabelStyle.normal.textColor = isActive
            ? Color.white
            : new Color(0.85f, 0.85f, 0.9f, 0.9f);

        return sceneHitboxLabelStyle;
    }
}
