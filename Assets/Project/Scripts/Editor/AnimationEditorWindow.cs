using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class AnimationEditorWindow : EditorWindow
{
    // =========================================================
    // State
    // =========================================================
    private CharacterObject selectedCharacter;
    private int comboModeIndex = 0;
    private readonly string[] comboModes = { "Ground Combo", "Air Combo", "Plunge", "Dash" };
    private int stepIndex = 0;

    private GameObject previewInstance;
    private int scrubberFrame = 0;
    private bool isPlaying = false;
    private float lastTime = 0f;

    private Vector2 scrollPos;
    private Dictionary<int, bool> hitFrameFoldouts = new Dictionary<int, bool>();

    // =========================================================
    // Color Palette
    // =========================================================
    private static readonly Color accentCyan     = new Color(0f, 0.83f, 1f);         // #00D4FF
    private static readonly Color startupGreen   = new Color(0.18f, 0.8f, 0.44f);     // #2ECC71
    private static readonly Color activeRed      = new Color(0.9f, 0.2f, 0.2f);       // #E74C3C
    private static readonly Color recoveryBlue   = new Color(0.2f, 0.6f, 1f);         // #3498DB
    private static readonly Color cardBg         = new Color(0.22f, 0.22f, 0.22f);    // #383838
    private static readonly Color panelBorder    = new Color(0.29f, 0.29f, 0.29f);    // #4A4A4A
    private static readonly Color hitMarkerGold  = new Color(1f, 0.84f, 0f);          // #FFD700
    private static readonly Color headerBg       = new Color(0.15f, 0.15f, 0.15f);    // #262626
    private static readonly Color tabInactive    = new Color(0.25f, 0.25f, 0.25f);
    private static readonly Color motionGreen    = new Color(0.2f, 0.8f, 0.2f);

    // =========================================================
    // GUIStyle Cache
    // =========================================================
    private bool stylesInitialized = false;
    private GUIStyle headerTitleStyle;
    private GUIStyle clipInfoStyle;
    private GUIStyle tabActiveStyle;
    private GUIStyle tabInactiveStyle;
    private GUIStyle stepActiveStyle;
    private GUIStyle stepInactiveStyle;
    private GUIStyle cardHeaderStyle;
    private GUIStyle cardSubHeaderStyle;
    private GUIStyle frameCounterStyle;
    private GUIStyle timelineLabelStyle;
    private GUIStyle rulerLabelStyle;
    private GUIStyle hitSummaryStyle;
    private GUIStyle phaseReadoutStyle;

    // =========================================================
    // Menu & Entry Points
    // =========================================================
    [MenuItem("Window/Animation Editor")]
    public static void ShowWindow()
    {
        var w = GetWindow<AnimationEditorWindow>("⚔ Combat Animation Editor");
        w.minSize = new Vector2(480, 600);
    }

    public static void OpenWindowFromInspector(CharacterObject character)
    {
        var w = GetWindow<AnimationEditorWindow>("⚔ Combat Animation Editor");
        w.minSize = new Vector2(480, 600);
        w.selectedCharacter = character;
        w.Show();
    }

    // =========================================================
    // Lifecycle
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
    // Style Init
    // =========================================================
    private void InitStyles()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        headerTitleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        clipInfoStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
        };

        tabActiveStyle = new GUIStyle("button")
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 26,
            normal = { textColor = Color.white },
            hover = { textColor = Color.white }
        };

        tabInactiveStyle = new GUIStyle("button")
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 26,
            normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
        };

        stepActiveStyle = new GUIStyle("button")
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            fixedHeight = 24,
            fixedWidth = 80,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        stepInactiveStyle = new GUIStyle("button")
        {
            fontSize = 10,
            fixedHeight = 24,
            fixedWidth = 80,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };

        cardHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = accentCyan }
        };

        cardSubHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };

        frameCounterStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        timelineLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 9,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        rulerLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 8,
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };

        hitSummaryStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
        };

        phaseReadoutStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };
    }

    // =========================================================
    // Editor Update (Auto-Play)
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

                int totalFrames = GetTotalClipFrames(clip);
                if (scrubberFrame >= totalFrames)
                {
                    scrubberFrame = 0;
                    isPlaying = false;
                }
                Repaint();
            }
        }
        lastTime = (float)EditorApplication.timeSinceStartup;
    }

    // =========================================================
    // Keyboard Input
    // =========================================================
    private void HandleKeyboard()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        AnimationClip clip = GetCurrentClip();
        int max = clip != null ? GetTotalClipFrames(clip) : 999;

        if (e.keyCode == KeyCode.LeftArrow)
        {
            scrubberFrame = Mathf.Max(0, scrubberFrame - 1);
            isPlaying = false;
            e.Use();
            Repaint();
        }
        else if (e.keyCode == KeyCode.RightArrow)
        {
            scrubberFrame = Mathf.Min(max, scrubberFrame + 1);
            isPlaying = false;
            e.Use();
            Repaint();
        }
        else if (e.keyCode == KeyCode.Space)
        {
            isPlaying = !isPlaying;
            e.Use();
            Repaint();
        }
    }

    // =========================================================
    // MAIN GUI
    // =========================================================
    void OnGUI()
    {
        InitStyles();
        HandleKeyboard();

        // ── HEADER BAR ──
        DrawHeaderBar();

        if (selectedCharacter == null)
        {
            EditorGUILayout.Space(40);
            EditorGUILayout.HelpBox("Select a CharacterObject to begin editing combat data.", MessageType.Info);
            return;
        }

        // ── TAB NAVIGATION ──
        EditorGUILayout.Space(6);
        DrawTabNavigation();

        // ── STEP SELECTOR ──
        if (comboModeIndex != 2 && comboModeIndex != 3)
        {
            EditorGUILayout.Space(4);
            DrawStepSelector();
        }

        // ── RESOLVE CURRENT DATA ──
        AttackStepData currentStep = GetCurrentStep();
        DashData currentDash = comboModeIndex == 3 ? selectedCharacter.dashData : null;

        if (currentStep == null && currentDash == null)
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox("No data available for this mode.", MessageType.Warning);
            return;
        }

        // ── SCROLLABLE CONTENT ──
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Preview Prefab
        EditorGUILayout.Space(8);
        EditorGUI.BeginChangeCheck();
        var newPrefab = (GameObject)EditorGUILayout.ObjectField("Preview Prefab", selectedCharacter.previewPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(selectedCharacter, "Change Preview Prefab");
            selectedCharacter.previewPrefab = newPrefab;
            EditorUtility.SetDirty(selectedCharacter);
        }

        if (comboModeIndex == 3 && currentDash != null)
            DrawDashMode(currentDash);
        else if (currentStep != null)
            DrawAttackMode(currentStep);

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // HEADER BAR
    // =========================================================
    private void DrawHeaderBar()
    {
        Rect headerRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, headerBg);

        // Title
        Rect titleRect = new Rect(headerRect.x + 10, headerRect.y, 260, headerRect.height);
        GUI.Label(titleRect, "⚔  Combat Animation Editor", headerTitleStyle);

        // Character Selector
        Rect objRect = new Rect(headerRect.x + 280, headerRect.y + 10, headerRect.width - 300, 20);
        EditorGUI.BeginChangeCheck();
        var newChar = (CharacterObject)EditorGUI.ObjectField(objRect, selectedCharacter, typeof(CharacterObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            selectedCharacter = newChar;
            stepIndex = 0;
        }

        // Clip Info
        AnimationClip clip = GetCurrentClip();
        if (clip != null && selectedCharacter != null)
        {
            string info = $"{clip.name} — {GetTotalClipFrames(clip)}f @ {Mathf.RoundToInt(clip.frameRate)}fps";
            Rect infoRect = new Rect(headerRect.xMax - 260, headerRect.y, 250, headerRect.height);
            GUI.Label(infoRect, info, clipInfoStyle);
        }
    }

    // =========================================================
    // TAB NAVIGATION (Pill Style)
    // =========================================================
    private void DrawTabNavigation()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        for (int i = 0; i < comboModes.Length; i++)
        {
            bool isActive = (comboModeIndex == i);
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isActive ? accentCyan : tabInactive;

            if (GUILayout.Button(comboModes[i], isActive ? tabActiveStyle : tabInactiveStyle, GUILayout.MinWidth(100)))
            {
                if (comboModeIndex != i)
                {
                    comboModeIndex = i;
                    stepIndex = 0;
                    scrubberFrame = 0;
                    isPlaying = false;
                }
            }
            GUI.backgroundColor = prevBg;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    // =========================================================
    // STEP SELECTOR (Numbered Circles)
    // =========================================================
    private void DrawStepSelector()
    {
        BasicAttackComboData comboData = comboModeIndex == 0 ? selectedCharacter.basicAttackComboData : selectedCharacter.neutralAirComboData;
        int max = comboData != null && comboData.steps != null ? comboData.steps.Length : 0;
        if (max == 0) return;

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        for (int i = 0; i < max; i++)
        {
            bool active = (stepIndex == i);
            string label = (i + 1).ToString();
            string name = comboData.steps[i].stepName ?? "Untitled";
            string btnText = $"{label}: {name}";

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = active ? accentCyan : tabInactive;

            if (GUILayout.Button(btnText, active ? stepActiveStyle : stepInactiveStyle))
            {
                stepIndex = i;
                scrubberFrame = 0;
                isPlaying = false;
            }
            GUI.backgroundColor = prevBg;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    // =========================================================
    // ATTACK MODE (Full UI)
    // =========================================================
    private void DrawAttackMode(AttackStepData step)
    {
        // Animation Clip
        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        var newClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", step.animationClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(selectedCharacter, "Change Animation Clip");
            step.animationClip = newClip;
            EditorUtility.SetDirty(selectedCharacter);
        }

        // Timeline
        EditorGUILayout.Space(8);
        int totalFrames = GetTotalFrames(step);
        if (totalFrames > 0)
        {
            DrawTimeline(step.startupFrames, step.activeFrames, step.recoveryFrames, totalFrames, step.hitFrames, step.useForwardMotion, step.motionStartFrame, step.motionEndFrame);
        }

        // Transport Controls
        EditorGUILayout.Space(6);
        AnimationClip clip = step.animationClip;
        int clipFrames = clip != null ? GetTotalClipFrames(clip) : totalFrames;
        DrawTransportControls(clip, clipFrames);

        // Add Hit Frame (Contextual)
        EditorGUILayout.Space(4);
        DrawAddHitFrameButton(step);

        // Auto-Sync Warning
        DrawAutoSyncWarning(step);

        // ── PHASE CONFIGURATION ──
        EditorGUILayout.Space(10);
        DrawPhaseCard(step);

        // ── FORWARD MOTION ──
        EditorGUILayout.Space(6);
        DrawForwardMotionCard(step);

        // ── HIT FRAMES ──
        EditorGUILayout.Space(10);
        DrawHitFrameList(step);
    }

    // =========================================================
    // DASH MODE
    // =========================================================
    private void DrawDashMode(DashData dash)
    {
        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        var newClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", dash.animationClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(selectedCharacter, "Change Dash Clip");
            dash.animationClip = newClip;
            EditorUtility.SetDirty(selectedCharacter);
        }

        int totalFrames = 0;
        if (dash.animationClip != null)
            totalFrames = GetTotalClipFrames(dash.animationClip);
        else
            totalFrames = dash.startupFrames + dash.activeFrames + dash.recoveryFrames;

        if (totalFrames > 0)
        {
            EditorGUILayout.Space(8);
            DrawTimeline(dash.startupFrames, dash.activeFrames, dash.recoveryFrames, totalFrames, null, false, 0, 0);
        }

        EditorGUILayout.Space(6);
        DrawTransportControls(dash.animationClip, dash.animationClip != null ? GetTotalClipFrames(dash.animationClip) : totalFrames);

        // Auto-Sync
        DrawAutoSyncWarningDash(dash);

        // Phase Card (Dash)
        EditorGUILayout.Space(10);
        DrawCard("⚡ Dash Phase Configuration", () =>
        {
            if (dash.animationClip != null)
            {
                int clipTotal = GetTotalClipFrames(dash.animationClip);
                DrawMinMaxPhaseSlider(ref dash.startupFrames, ref dash.activeFrames, ref dash.recoveryFrames, clipTotal, "Dash");
            }
            else
            {
                EditorGUILayout.HelpBox("Assign an Animation Clip to enable the Frame Sync Slider.", MessageType.Warning);
                dash.startupFrames = EditorGUILayout.IntField("Startup Frames", dash.startupFrames);
                dash.activeFrames = EditorGUILayout.IntField("Active Frames", dash.activeFrames);
                dash.recoveryFrames = EditorGUILayout.IntField("Recovery Frames", dash.recoveryFrames);
            }

            EditorGUILayout.Space(6);
            dash.dashSpeed = EditorGUILayout.FloatField("Dash Physics Speed", dash.dashSpeed);
            dash.cooldownFrames = EditorGUILayout.IntField("Cooldown Frames", dash.cooldownFrames);
        });
    }

    // =========================================================
    // INTERACTIVE TIMELINE
    // =========================================================
    private void DrawTimeline(int startup, int active, int recovery, int totalFrames,
                              AttackHitFrameData[] hitFrames, bool hasMotion, int motionStart, int motionEnd)
    {
        if (totalFrames <= 0) return;

        // ── Frame Ruler ──
        Rect rulerRect = GUILayoutUtility.GetRect(10, 16, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rulerRect, new Color(0.18f, 0.18f, 0.18f));
        float pxPerFrame = rulerRect.width / totalFrames;

        for (int f = 0; f <= totalFrames; f++)
        {
            float x = rulerRect.x + f * pxPerFrame;
            bool isMajor = (f % 10 == 0);
            bool isMinor = (f % 5 == 0);

            if (isMajor || isMinor)
            {
                float tickH = isMajor ? 8 : 4;
                EditorGUI.DrawRect(new Rect(x, rulerRect.yMax - tickH, 1, tickH), new Color(0.4f, 0.4f, 0.4f));
            }
            if (isMajor)
            {
                Rect labelR = new Rect(x - 12, rulerRect.y, 24, 12);
                GUI.Label(labelR, f.ToString(), rulerLabelStyle);
            }
        }

        // ── Phase Bars ──
        Rect barRect = GUILayoutUtility.GetRect(10, 28, GUILayout.ExpandWidth(true));
        float barPx = barRect.width / totalFrames;

        Rect suRect  = new Rect(barRect.x, barRect.y, startup * barPx, barRect.height);
        Rect actRect = new Rect(suRect.xMax, barRect.y, active * barPx, barRect.height);
        Rect recRect = new Rect(actRect.xMax, barRect.y, recovery * barPx, barRect.height);

        EditorGUI.DrawRect(suRect, startupGreen);
        EditorGUI.DrawRect(actRect, activeRed);
        EditorGUI.DrawRect(recRect, recoveryBlue);

        // Grid overlay
        for (int i = 0; i <= totalFrames; i++)
        {
            float x = barRect.x + i * barPx;
            EditorGUI.DrawRect(new Rect(x, barRect.y, 1, barRect.height), Color.black);
        }
        EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(barRect.x, barRect.yMax - 1, barRect.width, 1), Color.black);

        if (suRect.width > 30) GUI.Label(suRect, "SU:" + startup, timelineLabelStyle);
        if (actRect.width > 30) GUI.Label(actRect, "ACT:" + active, timelineLabelStyle);
        if (recRect.width > 30) GUI.Label(recRect, "REC:" + recovery, timelineLabelStyle);

        // ── Hit Frame Diamond Markers ──
        if (hitFrames != null)
        {
            for (int i = 0; i < hitFrames.Length; i++)
            {
                var h = hitFrames[i];
                int hitStart = startup + h.startFrameOffset;
                int hitEnd = hitStart + h.activeFrames;

                // Draw a small colored bar for the hit frame range
                float hx = barRect.x + hitStart * barPx;
                float hw = h.activeFrames * barPx;
                Rect hitBar = new Rect(hx, barRect.yMax - 5, hw, 5);
                EditorGUI.DrawRect(hitBar, hitMarkerGold);

                // Diamond marker at start
                float dx = barRect.x + hitStart * barPx;
                DrawDiamond(new Vector2(dx, barRect.y - 2), 4, hitMarkerGold);
            }
        }

        // ── Forward Motion Sub-Bar ──
        if (hasMotion)
        {
            Rect motionRect = GUILayoutUtility.GetRect(10, 6, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(motionRect, new Color(0.18f, 0.18f, 0.18f));
            float mx = motionRect.x + motionStart * barPx;
            float mw = (motionEnd - motionStart + 1) * barPx;
            EditorGUI.DrawRect(new Rect(mx, motionRect.y, mw, motionRect.height), motionGreen);

            // Small label
            Rect mLabel = new Rect(motionRect.x + 4, motionRect.y - 1, 120, motionRect.height);
            GUI.Label(mLabel, "Motion", EditorStyles.miniLabel);
        }

        // ── Playhead ──
        float playheadX = barRect.x + scrubberFrame * barPx;
        EditorGUI.DrawRect(new Rect(playheadX - 1, rulerRect.y, 2, barRect.yMax - rulerRect.y + (hasMotion ? 6 : 0)),
                           Color.white);
        // Triangle handle
        Vector3 triTop = new Vector3(playheadX, rulerRect.y + 2);
        Vector3 triLeft = new Vector3(playheadX - 5, rulerRect.y - 4);
        Vector3 triRight = new Vector3(playheadX + 5, rulerRect.y - 4);
        Handles.color = Color.white;
        Handles.DrawAAConvexPolygon(triTop, triLeft, triRight);

        // ── Click-to-Seek ──
        Rect clickArea = new Rect(barRect.x, rulerRect.y, barRect.width, barRect.yMax - rulerRect.y);
        if (Event.current.type == EventType.MouseDown && clickArea.Contains(Event.current.mousePosition))
        {
            float relX = Event.current.mousePosition.x - barRect.x;
            scrubberFrame = Mathf.Clamp(Mathf.RoundToInt(relX / barPx), 0, totalFrames);
            isPlaying = false;
            Event.current.Use();
            Repaint();
        }
    }

    private void DrawDiamond(Vector2 center, float size, Color color)
    {
        Handles.color = color;
        Vector3[] pts = new Vector3[4]
        {
            new Vector3(center.x, center.y - size),
            new Vector3(center.x + size, center.y),
            new Vector3(center.x, center.y + size),
            new Vector3(center.x - size, center.y),
        };
        Handles.DrawAAConvexPolygon(pts);
    }

    // =========================================================
    // TRANSPORT CONTROLS
    // =========================================================
    private void DrawTransportControls(AnimationClip clip, int maxFrames)
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // Step Back
        if (GUILayout.Button("◄◄", GUILayout.Width(36), GUILayout.Height(24)))
        {
            scrubberFrame = Mathf.Max(0, scrubberFrame - 1);
            isPlaying = false;
        }

        // Play / Pause
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = isPlaying ? activeRed : motionGreen;
        if (GUILayout.Button(isPlaying ? "❚❚" : "▶", GUILayout.Width(40), GUILayout.Height(24)))
        {
            isPlaying = !isPlaying;
            if (isPlaying && selectedCharacter != null && selectedCharacter.previewPrefab != null)
                InstantiatePreviewIfNeeded(selectedCharacter.previewPrefab);
        }
        GUI.backgroundColor = prevBg;

        // Step Forward
        if (GUILayout.Button("►►", GUILayout.Width(36), GUILayout.Height(24)))
        {
            scrubberFrame = Mathf.Min(maxFrames, scrubberFrame + 1);
            isPlaying = false;
        }

        GUILayout.Space(12);

        // Frame Counter
        GUILayout.Label($"Frame: {scrubberFrame} / {maxFrames}", frameCounterStyle, GUILayout.Width(140));

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // Slider
        EditorGUI.BeginChangeCheck();
        scrubberFrame = EditorGUILayout.IntSlider(scrubberFrame, 0, maxFrames);
        if (EditorGUI.EndChangeCheck())
            isPlaying = false;

        // Sample animation
        if (!Application.isPlaying && selectedCharacter != null && selectedCharacter.previewPrefab != null && clip != null)
        {
            InstantiatePreviewIfNeeded(selectedCharacter.previewPrefab);
            SampleAnimation(clip, scrubberFrame);
        }

        // Cleanup button
        if (!Application.isPlaying && previewInstance != null)
        {
            if (GUILayout.Button("Cleanup Preview", EditorStyles.miniButton))
                CleanupPreview();
        }
    }

    // =========================================================
    // ADD HIT FRAME (CONTEXTUAL)
    // =========================================================
    private void DrawAddHitFrameButton(AttackStepData step)
    {
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
        if (GUILayout.Button("＋  Mark Current Frame as Hit Trigger", GUILayout.Height(28)))
        {
            Undo.RecordObject(selectedCharacter, "Add Hit Frame");
            AddHitFrameAtScrubber(step);
        }
        GUI.backgroundColor = prevBg;
    }

    // =========================================================
    // AUTO-SYNC WARNING
    // =========================================================
    private void DrawAutoSyncWarning(AttackStepData step)
    {
        if (step.animationClip == null) return;
        int target = GetTotalClipFrames(step.animationClip);
        int current = step.startupFrames + step.activeFrames + step.recoveryFrames;
        if (current <= 0 || current == target) return;

        EditorGUILayout.Space(6);
        float ratio = (float)target / current;
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.HelpBox(
            $"Clip implies {target} frames but data totals {current}.\nSample Rate: {step.animationClip.frameRate} FPS",
            MessageType.Error);

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = hitMarkerGold;
        if (GUILayout.Button($"Auto-Sync Ratio ({ratio:F2}x)", GUILayout.Height(26)))
        {
            Undo.RecordObject(selectedCharacter, "Auto-Sync Frame Data");
            step.startupFrames = Mathf.RoundToInt(step.startupFrames * ratio);
            step.activeFrames = Mathf.RoundToInt(step.activeFrames * ratio);
            step.recoveryFrames = target - step.startupFrames - step.activeFrames;
            if (step.recoveryFrames < 0) step.recoveryFrames = 0;
            step.cancelWindowStartFrame = Mathf.RoundToInt(step.cancelWindowStartFrame * ratio);
            if (step.hitFrames != null)
            {
                for (int i = 0; i < step.hitFrames.Length; i++)
                {
                    step.hitFrames[i].startFrameOffset = Mathf.RoundToInt(step.hitFrames[i].startFrameOffset * ratio);
                    step.hitFrames[i].activeFrames = Mathf.RoundToInt(step.hitFrames[i].activeFrames * ratio);
                }
            }
            if (step.useForwardMotion)
            {
                step.motionStartFrame = Mathf.RoundToInt(step.motionStartFrame * ratio);
                step.motionEndFrame = Mathf.RoundToInt(step.motionEndFrame * ratio);
            }
            EditorUtility.SetDirty(selectedCharacter);
        }
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndVertical();
    }

    private void DrawAutoSyncWarningDash(DashData dash)
    {
        if (dash.animationClip == null) return;
        int target = GetTotalClipFrames(dash.animationClip);
        int current = dash.startupFrames + dash.activeFrames + dash.recoveryFrames;
        if (current <= 0 || current == target) return;

        EditorGUILayout.Space(6);
        float ratio = (float)target / current;
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.HelpBox(
            $"Clip implies {target} frames but data totals {current}.\nSample Rate: {dash.animationClip.frameRate} FPS",
            MessageType.Error);

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = hitMarkerGold;
        if (GUILayout.Button($"Auto-Sync Ratio ({ratio:F2}x)", GUILayout.Height(26)))
        {
            Undo.RecordObject(selectedCharacter, "Auto-Sync Dash Data");
            dash.startupFrames = Mathf.RoundToInt(dash.startupFrames * ratio);
            dash.activeFrames = Mathf.RoundToInt(dash.activeFrames * ratio);
            dash.recoveryFrames = target - dash.startupFrames - dash.activeFrames;
            if (dash.recoveryFrames < 0) dash.recoveryFrames = 0;
            dash.cooldownFrames = Mathf.RoundToInt(dash.cooldownFrames * ratio);
            EditorUtility.SetDirty(selectedCharacter);
        }
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // PHASE CARD
    // =========================================================
    private void DrawPhaseCard(AttackStepData step)
    {
        DrawCard("🎬 Phase Configuration", () =>
        {
            EditorGUI.BeginChangeCheck();
            step.stepName = EditorGUILayout.TextField("Step Name", step.stepName);

            if (step.animationClip != null)
            {
                int clipTotal = GetTotalClipFrames(step.animationClip);
                DrawMinMaxPhaseSlider(ref step.startupFrames, ref step.activeFrames, ref step.recoveryFrames, clipTotal, "Attack");
            }
            else
            {
                EditorGUILayout.HelpBox("Assign an Animation Clip for the Frame Sync Slider.", MessageType.Warning);
                step.startupFrames = EditorGUILayout.IntField("Startup Frames", step.startupFrames);
                step.activeFrames = EditorGUILayout.IntField("Active Frames", step.activeFrames);
                step.recoveryFrames = EditorGUILayout.IntField("Recovery Frames", step.recoveryFrames);
            }

            step.cancelWindowStartFrame = EditorGUILayout.IntField(
                new GUIContent("Cancel At Frame", "Frame in RECOVERY where combo cancel is allowed."),
                step.cancelWindowStartFrame);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selectedCharacter, "Edit Phase Config");
                EditorUtility.SetDirty(selectedCharacter);
            }
        });
    }

    // =========================================================
    // FORWARD MOTION CARD
    // =========================================================
    private void DrawForwardMotionCard(AttackStepData step)
    {
        DrawCard("🏃 Forward Motion", () =>
        {
            EditorGUI.BeginChangeCheck();
            step.useForwardMotion = EditorGUILayout.Toggle("Enable", step.useForwardMotion);
            if (step.useForwardMotion)
            {
                step.forwardDistance = EditorGUILayout.FloatField("Distance (Units)", step.forwardDistance);
                step.motionStartFrame = EditorGUILayout.IntField("Start Frame", step.motionStartFrame);
                step.motionEndFrame = EditorGUILayout.IntField("End Frame", step.motionEndFrame);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selectedCharacter, "Edit Forward Motion");
                EditorUtility.SetDirty(selectedCharacter);
            }
        });
    }

    // =========================================================
    // HIT FRAME LIST (Accordion)
    // =========================================================
    private void DrawHitFrameList(AttackStepData step)
    {
        int count = step.hitFrames != null ? step.hitFrames.Length : 0;

        // Section Header
        GUILayout.BeginHorizontal();
        GUILayout.Label($"💥 Active Hit Frames ({count})", cardHeaderStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (count == 0)
        {
            EditorGUILayout.HelpBox("Use the green button above to add hit triggers.", MessageType.Info);
            return;
        }

        for (int i = 0; i < step.hitFrames.Length; i++)
        {
            AttackHitFrameData h = step.hitFrames[i];
            int hitStart = step.startupFrames + h.startFrameOffset;
            int hitEnd = hitStart + h.activeFrames;

            // ── Summary Header Bar ──
            Rect headerRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));

            // Accent bar (left edge)
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 4, headerRect.height), h.debugColor);
            // Background
            EditorGUI.DrawRect(new Rect(headerRect.x + 4, headerRect.y, headerRect.width - 4, headerRect.height), cardBg);

            // Foldout toggle
            if (!hitFrameFoldouts.ContainsKey(i)) hitFrameFoldouts[i] = false;
            string arrow = hitFrameFoldouts[i] ? "▾" : "►";
            string summary = $"{arrow}  Impact #{i + 1}  —  Frame {hitStart}→{hitEnd}  ({h.activeFrames}f)    ●Dmg: {h.damageMultiplier:F1}x";

            Rect labelRect = new Rect(headerRect.x + 12, headerRect.y, headerRect.width - 80, headerRect.height);
            if (GUI.Button(labelRect, summary, hitSummaryStyle))
                hitFrameFoldouts[i] = !hitFrameFoldouts[i];

            // Delete button
            Rect delRect = new Rect(headerRect.xMax - 60, headerRect.y + 4, 52, 20);
            if (GUI.Button(delRect, "Delete", EditorStyles.miniButton))
            {
                Undo.RecordObject(selectedCharacter, "Delete Hit Frame");
                var list = new List<AttackHitFrameData>(step.hitFrames);
                list.RemoveAt(i);
                step.hitFrames = list.ToArray();
                EditorUtility.SetDirty(selectedCharacter);
                GUIUtility.ExitGUI();
            }

            // ── Expanded Content ──
            if (hitFrameFoldouts[i])
            {
                DrawHitFrameCards(h, i);
            }

            EditorGUILayout.Space(4);
        }
    }

    // =========================================================
    // HIT FRAME CARDS (Expanded)
    // =========================================================
    private void DrawHitFrameCards(AttackHitFrameData h, int index)
    {
        EditorGUI.BeginChangeCheck();

        // 📐 Hitbox Geometry
        DrawCard("📐 Hitbox Geometry", () =>
        {
            h.startFrameOffset = EditorGUILayout.IntField(new GUIContent("Start Offset", "Frames from Active phase start."), h.startFrameOffset);
            h.activeFrames = EditorGUILayout.IntField("Active Duration", h.activeFrames);
            h.offset = EditorGUILayout.Vector2Field("Position (Offset)", h.offset);
            h.size = EditorGUILayout.Vector2Field("Size (W, H)", h.size);
            h.debugColor = EditorGUILayout.ColorField("Debug Color", h.debugColor);
        });

        EditorGUILayout.Space(2);

        // 💥 Impact & Feel
        DrawCard("💥 Impact & Feel", () =>
        {
            h.damageMultiplier = EditorGUILayout.FloatField("Damage Scale", h.damageMultiplier);
            h.cameraShakeStrength = EditorGUILayout.FloatField("Camera Shake", h.cameraShakeStrength);
            h.knockbackForce = EditorGUILayout.FloatField("Knockback Force", h.knockbackForce);
            h.impactTier = (DamageImpactTier)EditorGUILayout.EnumPopup("Impact Tier", h.impactTier);
        });

        EditorGUILayout.Space(2);

        // 🎯 Enemy Reaction
        DrawCard("🎯 Enemy Reaction", () =>
        {
            h.hitstunFrames = EditorGUILayout.IntField("Hitstop Freeze", h.hitstunFrames);
            h.knockbackFrames = EditorGUILayout.IntField("Hitstun Duration", h.knockbackFrames);
            h.reactionVelocity = EditorGUILayout.Vector2Field("Reaction Velocity", h.reactionVelocity);
            h.isKnockdown = EditorGUILayout.Toggle("Is Knockdown", h.isKnockdown);

            EditorGUILayout.Space(4);
            GUI.Label(GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true)), "── Snap ──", cardSubHeaderStyle);
            h.snapToAttacker = EditorGUILayout.Toggle("Snap To Attacker", h.snapToAttacker);
            if (h.snapToAttacker)
                h.snapOffset = EditorGUILayout.Vector2Field("Snap Offset", h.snapOffset);

            EditorGUILayout.Space(4);
            GUI.Label(GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true)), "── Visual ──", cardSubHeaderStyle);
            h.flashColor = EditorGUILayout.ColorField("Flash Color", h.flashColor);
            h.flashFrames = EditorGUILayout.IntField("Flash Frames", h.flashFrames);
        });

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(selectedCharacter, $"Edit Hit Frame #{index + 1}");
            EditorUtility.SetDirty(selectedCharacter);
        }
    }

    // =========================================================
    // REUSABLE: Card Drawer
    // =========================================================
    private void DrawCard(string title, System.Action drawContent)
    {
        // Background
        EditorGUILayout.BeginVertical();
        Rect bgRect = EditorGUILayout.BeginVertical();

        // Draw card bg manually after layout
        EditorGUILayout.Space(2);

        // Header
        GUILayout.Label(title, cardHeaderStyle);

        // Border
        EditorGUILayout.BeginVertical("helpbox");
        drawContent();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // REUSABLE: MinMax Phase Slider
    // =========================================================
    private void DrawMinMaxPhaseSlider(ref int startup, ref int active, ref int recovery, int totalFrames, string undoLabel)
    {
        float minVal = startup;
        float maxVal = startup + active;

        EditorGUILayout.Space(4);
        GUILayout.Label($"Active Frames between {Mathf.RoundToInt(minVal)} and {Mathf.RoundToInt(maxVal)}", phaseReadoutStyle);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.MinMaxSlider(ref minVal, ref maxVal, 0, totalFrames);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(selectedCharacter, $"Adjust {undoLabel} Phases");
            startup = Mathf.RoundToInt(minVal);
            active = Mathf.RoundToInt(maxVal - minVal);
            recovery = Mathf.RoundToInt(totalFrames - maxVal);
            EditorUtility.SetDirty(selectedCharacter);
        }

        // Readout
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.IntField("SU", startup);
        EditorGUILayout.IntField("ACT", active);
        EditorGUILayout.IntField("REC", recovery);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    // DATA ACCESS HELPERS
    // =========================================================
    private AttackStepData GetCurrentStep()
    {
        if (selectedCharacter == null || comboModeIndex == 3) return null;

        if (comboModeIndex == 2)
            return selectedCharacter.plungeAttackData?.impactStep;

        BasicAttackComboData comboData = comboModeIndex == 0
            ? selectedCharacter.basicAttackComboData
            : selectedCharacter.neutralAirComboData;

        if (comboData != null && comboData.steps != null && comboData.steps.Length > 0)
        {
            if (stepIndex >= comboData.steps.Length) stepIndex = 0;
            return comboData.steps[stepIndex];
        }
        return null;
    }

    private AnimationClip GetCurrentClip()
    {
        if (selectedCharacter == null) return null;
        if (comboModeIndex == 3)
            return selectedCharacter.dashData?.animationClip;
        var step = GetCurrentStep();
        return step?.animationClip;
    }

    private int GetTotalClipFrames(AnimationClip clip)
    {
        return Mathf.RoundToInt(clip.length * 24f);
    }

    private int GetTotalFrames(AttackStepData step)
    {
        if (step.animationClip != null)
            return GetTotalClipFrames(step.animationClip);
        return step.startupFrames + step.activeFrames + step.recoveryFrames;
    }

    // =========================================================
    // HIT FRAME CREATION
    // =========================================================
    private void AddHitFrameAtScrubber(AttackStepData step)
    {
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
        Debug.Log($"Hit Frame added at scrubber frame {scrubberFrame} (offset: {offset})");
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
    // SCENE VIEW HANDLES (Unchanged)
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
                Vector3 newRightWorld = Handles.FreeMoveHandle(rightWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedCharacter, "Scale Hitbox");
                    Vector3 lp = previewInstance.transform.InverseTransformPoint(newRightWorld);
                    float oldLeft = h.offset.x - (h.size.x * 0.5f);
                    float nw = lp.x - oldLeft;
                    if (nw < minSize) nw = minSize;
                    h.size.x = nw;
                    h.offset.x = oldLeft + (nw * 0.5f);
                    changed = true;
                }

                // Left
                Vector3 leftWorld = worldPos + rot * new Vector3(-halfSize.x, 0, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newLeftWorld = Handles.FreeMoveHandle(leftWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedCharacter, "Scale Hitbox");
                    Vector3 lp = previewInstance.transform.InverseTransformPoint(newLeftWorld);
                    float oldRight = h.offset.x + (h.size.x * 0.5f);
                    float nw = oldRight - lp.x;
                    if (nw < minSize) nw = minSize;
                    h.size.x = nw;
                    h.offset.x = oldRight - (nw * 0.5f);
                    changed = true;
                }

                // Top
                Vector3 topWorld = worldPos + rot * new Vector3(0, halfSize.y, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newTopWorld = Handles.FreeMoveHandle(topWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedCharacter, "Scale Hitbox");
                    Vector3 lp = previewInstance.transform.InverseTransformPoint(newTopWorld);
                    float oldBottom = h.offset.y - (h.size.y * 0.5f);
                    float nh = lp.y - oldBottom;
                    if (nh < minSize) nh = minSize;
                    h.size.y = nh;
                    h.offset.y = oldBottom + (nh * 0.5f);
                    changed = true;
                }

                // Bottom
                Vector3 bottomWorld = worldPos + rot * new Vector3(0, -halfSize.y, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newBottomWorld = Handles.FreeMoveHandle(bottomWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedCharacter, "Scale Hitbox");
                    Vector3 lp = previewInstance.transform.InverseTransformPoint(newBottomWorld);
                    float oldTop = h.offset.y + (h.size.y * 0.5f);
                    float nh = oldTop - lp.y;
                    if (nh < minSize) nh = minSize;
                    h.size.y = nh;
                    h.offset.y = oldTop - (nh * 0.5f);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(selectedCharacter);
            Repaint();
        }
    }
}
