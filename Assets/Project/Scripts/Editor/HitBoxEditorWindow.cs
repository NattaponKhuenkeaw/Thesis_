using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class HitBoxEditorWindow : EditorWindow
{
    private CharacterObject selectedCharacter;
    private int comboModeIndex = 0;
    private readonly string[] comboModes = { "Basic Ground", "Neutral Air", "Plunge Impact" };
    private int stepIndex = 0;

    private GameObject previewInstance;
    private int scrubberFrame = 0;
    private bool isPlaying = false;
    private float lastTime = 0f;

    // Editor scroll tracking
    private Vector2 scrollPos;
    private GUIStyle headerStyle;
    private GUIStyle categoryStyle;

    // Foldout states
    private bool showStepDetails = true;
    private bool showForwardMotion = false;
    private bool showHitFrames = true;
    private Dictionary<int, bool> reactionFoldouts = new Dictionary<int, bool>();

    [MenuItem("Window/Hit Box Editor")]
    public static void ShowWindow()
    {
        GetWindow<HitBoxEditorWindow>("Hit Box Editor");
    }

    public static void OpenWindowFromInspector(CharacterObject character)
    {
        var window = GetWindow<HitBoxEditorWindow>("Hit Box Editor");
        window.selectedCharacter = character;
        window.Show();
    }

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

    void OnDestroy()
    {
        CleanupPreview();
    }

    private void EditorUpdate()
    {
        if (isPlaying)
        {
            AttackStepData step = GetCurrentStep();
            if (step != null && step.animationClip != null)
            {
                float dt = (float)EditorApplication.timeSinceStartup - lastTime;
                float framesToAdvance = dt * 24f; // 24 fps standard
                scrubberFrame += Mathf.RoundToInt(framesToAdvance);
                
                int totalFrames = Mathf.RoundToInt(step.animationClip.length * 24f);
                if (totalFrames <= 0) totalFrames = 1;

                if (scrubberFrame >= totalFrames)
                {
                    scrubberFrame = 0;
                    isPlaying = false; // Stop at end or loop
                }

                Repaint();
            }
        }
        lastTime = (float)EditorApplication.timeSinceStartup;
    }

    void OnGUI()
    {
        if (headerStyle == null)
            headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        if (categoryStyle == null)
            categoryStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("COMBAT HIT BOX EDITOR (24 FPS)", headerStyle);
        EditorGUILayout.Space(10);

        selectedCharacter = (CharacterObject)EditorGUILayout.ObjectField("Character Asset", selectedCharacter, typeof(CharacterObject), false);

        if (selectedCharacter == null)
        {
            EditorGUILayout.HelpBox("Select a CharacterObject to edit its Hit Frames.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        comboModeIndex = GUILayout.Toolbar(comboModeIndex, comboModes);
        EditorGUILayout.Space(5);

        AttackStepData currentStep = GetCurrentStep();
        RenderStepSelector();

        if (currentStep == null)
        {
            EditorGUILayout.HelpBox("No Attack Step Data available.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space(10);
        
        EditorGUI.BeginChangeCheck();
        
        // --- Core Preview ---
        selectedCharacter.previewPrefab = (GameObject)EditorGUILayout.ObjectField("Preview Character Prefab", selectedCharacter.previewPrefab, typeof(GameObject), false);
        currentStep.animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", currentStep.animationClip, typeof(AnimationClip), false);

        EditorGUILayout.Space(10);
        RenderTimeline(currentStep);

        EditorGUILayout.Space(10);
        RenderPreviewScrubber(currentStep);
        
        EditorGUILayout.Space(15);
        
        // --- Step Setup Details ---
        showStepDetails = EditorGUILayout.Foldout(showStepDetails, "Step Phase & Cancellation", true, categoryStyle);
        if (showStepDetails)
        {
            EditorGUILayout.BeginVertical("helpbox");
            currentStep.stepName = EditorGUILayout.TextField("Step Name", currentStep.stepName);
            
            // --- NEW RANGE SLIDER CONTROLLER ---
            if (currentStep.animationClip != null)
            {
                int totalFrames = Mathf.RoundToInt(currentStep.animationClip.length * 24f);
                float minVal = currentStep.startupFrames;
                float maxVal = currentStep.startupFrames + currentStep.activeFrames;

                EditorGUILayout.Space(5);
                GUIStyle boldLabel = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
                GUILayout.Label($"Active Frames between {Mathf.RoundToInt(minVal)} and {Mathf.RoundToInt(maxVal)}", boldLabel);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.MinMaxSlider(ref minVal, ref maxVal, 0, totalFrames);
                if (EditorGUI.EndChangeCheck())
                {
                    currentStep.startupFrames = Mathf.RoundToInt(minVal);
                    currentStep.activeFrames = Mathf.RoundToInt(maxVal - minVal);
                    currentStep.recoveryFrames = Mathf.RoundToInt(totalFrames - maxVal);
                }
                
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("SU", currentStep.startupFrames);
                EditorGUILayout.IntField("ACT", currentStep.activeFrames);
                EditorGUILayout.IntField("REC", currentStep.recoveryFrames);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Assign an Animation Clip to enable Frame Sync Range Slider.", MessageType.Warning);
                currentStep.startupFrames = EditorGUILayout.IntField("Startup Frames", currentStep.startupFrames);
                currentStep.activeFrames = EditorGUILayout.IntField("Active Frames", currentStep.activeFrames);
                currentStep.recoveryFrames = EditorGUILayout.IntField("Recovery Frames", currentStep.recoveryFrames);
            }

            currentStep.cancelWindowStartFrame = EditorGUILayout.IntField(new GUIContent("Cancel At Frame", "Frame relative to RECOVERY phase start where move can be cancelled."), currentStep.cancelWindowStartFrame);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(5);

        // --- Forward Motion ---
        showForwardMotion = EditorGUILayout.Foldout(showForwardMotion, "Forward Motion Settings", true, categoryStyle);
        if (showForwardMotion)
        {
            EditorGUILayout.BeginVertical("helpbox");
            currentStep.useForwardMotion = EditorGUILayout.Toggle("Enable Forward Motion", currentStep.useForwardMotion);
            if (currentStep.useForwardMotion)
            {
                currentStep.forwardDistance = EditorGUILayout.FloatField("Distance (Units)", currentStep.forwardDistance);
                currentStep.motionStartFrame = EditorGUILayout.IntField("Motion Start frame", currentStep.motionStartFrame);
                currentStep.motionEndFrame = EditorGUILayout.IntField("Motion End frame", currentStep.motionEndFrame);
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(15);

        // --- Hit Frames ---
        showHitFrames = EditorGUILayout.Foldout(showHitFrames, "Active Hit Frame Data (" + (currentStep.hitFrames != null ? currentStep.hitFrames.Length : 0) + ")", true, categoryStyle);
        if (showHitFrames)
        {
            RenderHitFramesSettings(currentStep);
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(selectedCharacter);
        }

        EditorGUILayout.EndScrollView();
    }

    private void RenderStepSelector()
    {
        if (comboModeIndex == 2) return; // Plunge only has 1 impact step

        BasicAttackComboData comboData = comboModeIndex == 0 ? selectedCharacter.basicAttackComboData : selectedCharacter.neutralAirComboData;
        int max = comboData != null && comboData.steps != null ? comboData.steps.Length : 0;
        if (max == 0) return;

        string[] steps = new string[max];
        for (int i = 0; i < max; i++) steps[i] = "Step " + (i + 1) + " (" + (comboData.steps[i].stepName ?? "Untitled") + ")";

        stepIndex = GUILayout.Toolbar(stepIndex, steps);
        if (stepIndex >= max) stepIndex = 0;
    }

    private AttackStepData GetCurrentStep()
    {
        if (selectedCharacter == null) return null;

        if (comboModeIndex == 2)
            return selectedCharacter.plungeAttackData?.impactStep;

        BasicAttackComboData comboData = comboModeIndex == 0 ? selectedCharacter.basicAttackComboData : selectedCharacter.neutralAirComboData;
        
        if (comboData != null && comboData.steps != null && comboData.steps.Length > 0)
        {
            if (stepIndex >= comboData.steps.Length) stepIndex = 0;
            return comboData.steps[stepIndex];
        }

        return null;
    }

    private void RenderTimeline(AttackStepData step)
    {
        GUILayout.Label("Frame Data Timeline", EditorStyles.boldLabel);
        
        int totalFrames = 0;
        if (step.animationClip != null)
        {
            totalFrames = Mathf.RoundToInt(step.animationClip.length * 24f);
        }
        else
        {
            totalFrames = step.startupFrames + step.activeFrames + step.recoveryFrames;
        }

        if (totalFrames <= 0)
        {
            EditorGUILayout.HelpBox("Total frames is 0. Assign an animation or add logic frames.", MessageType.Info);
            return;
        }

        Rect rect = GUILayoutUtility.GetRect(10, 30, GUILayout.ExpandWidth(true));
        float widthMap = rect.width / totalFrames;

        Rect startUpRect = new Rect(rect.x, rect.y, step.startupFrames * widthMap, rect.height);
        Rect activeRect = new Rect(startUpRect.xMax, rect.y, step.activeFrames * widthMap, rect.height);
        Rect recovRect = new Rect(activeRect.xMax, rect.y, step.recoveryFrames * widthMap, rect.height);

        EditorGUI.DrawRect(startUpRect, new Color(1f, 0.6f, 0f)); // Orange
        EditorGUI.DrawRect(activeRect, Color.cyan);
        EditorGUI.DrawRect(recovRect, Color.red);
        
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.black } };
        GUI.Label(startUpRect, "SU: " + step.startupFrames, labelStyle);
        GUI.Label(activeRect, "ACT: " + step.activeFrames, labelStyle);
        GUI.Label(recovRect, "REC: " + step.recoveryFrames, labelStyle);

        // Forward Motion Marker (Sub-Timeline)
        if (step.useForwardMotion)
        {
            Rect motionRectTotal = GUILayoutUtility.GetRect(10, 6, GUILayout.ExpandWidth(true));
            float motionStartOffset = step.motionStartFrame * widthMap;
            float motionWidth = (step.motionEndFrame - step.motionStartFrame + 1) * widthMap;
            
            Rect motionFill = new Rect(motionRectTotal.x + motionStartOffset, motionRectTotal.y, motionWidth, motionRectTotal.height);
            EditorGUI.DrawRect(motionRectTotal, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            EditorGUI.DrawRect(motionFill, Color.green);
            GUI.Label(motionRectTotal, "   Forward Motion Window", EditorStyles.miniLabel);
        }
    }

    private void RenderPreviewScrubber(AttackStepData step)
    {
        if (step.animationClip == null)
        {
            EditorGUILayout.HelpBox("Assign an Animation Clip to enable preview scrubbing.", MessageType.Warning);
            return;
        }

        int totalClipFrames = Mathf.RoundToInt(step.animationClip.length * 24f);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(isPlaying ? "Pause" : "Play", GUILayout.Width(60)))
        {
            isPlaying = !isPlaying;
            if (isPlaying && selectedCharacter.previewPrefab != null)
            {
                InstantiatePreviewIfNeeded(selectedCharacter.previewPrefab);
            }
        }

        EditorGUI.BeginChangeCheck();
        scrubberFrame = EditorGUILayout.IntSlider(scrubberFrame, 0, totalClipFrames);
        if (EditorGUI.EndChangeCheck())
        {
            isPlaying = false;
        }
        GUILayout.EndHorizontal();

        // Scene Sample
        if (!Application.isPlaying && selectedCharacter.previewPrefab != null)
        {
            InstantiatePreviewIfNeeded(selectedCharacter.previewPrefab);
            SampleAnimation(step.animationClip, scrubberFrame);
        }
        
        GUILayout.Space(5);
        if (!Application.isPlaying && GUILayout.Button("Cleanup Scene Preview Instance"))
        {
            CleanupPreview();
        }

        // Add Hit Frame Feature
        GUILayout.Space(10);
        
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Mark Current Scrubber Frame as Hit Frame Trigger", GUILayout.Height(30)))
        {
            AddHitFrameAtScrubber(step);
        }
        GUI.backgroundColor = Color.white;
    }

    private void RenderHitFramesSettings(AttackStepData step)
    {
        if (step.hitFrames == null || step.hitFrames.Length == 0)
        {
            EditorGUILayout.HelpBox("Use the green button above to add impact triggers.", MessageType.Info);
            return;
        }

        for (int i = 0; i < step.hitFrames.Length; i++)
        {
            EditorGUILayout.BeginVertical("helpbox");
            AttackHitFrameData h = step.hitFrames[i];
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("IMPACT DATA #" + (i+1), EditorStyles.boldLabel);
            if (GUILayout.Button("Delete Component", GUILayout.Width(120)))
            {
                var list = new List<AttackHitFrameData>(step.hitFrames);
                list.RemoveAt(i);
                step.hitFrames = list.ToArray();
                EditorUtility.SetDirty(selectedCharacter);
                break;
            }
            GUILayout.EndHorizontal();

            // Geometry & Timing
            EditorGUILayout.LabelField("Hitbox Geometry & Timing", EditorStyles.miniBoldLabel);
            h.startFrameOffset = EditorGUILayout.IntField(new GUIContent("Start Offset", "Frames from the START OF ACTIVE phase."), h.startFrameOffset);
            h.activeFrames = EditorGUILayout.IntField("Active Durations", h.activeFrames);
            h.offset = EditorGUILayout.Vector2Field("Box Position (Offset)", h.offset);
            h.size = EditorGUILayout.Vector2Field("Box Size", h.size);
            h.debugColor = EditorGUILayout.ColorField("Debug Render Color", h.debugColor);
            
            EditorGUILayout.Space(5);

            // Core Impact
            EditorGUILayout.LabelField("Damage & Feel", EditorStyles.miniBoldLabel);
            h.damageMultiplier = EditorGUILayout.FloatField("Damage Scale", h.damageMultiplier);
            h.cameraShakeStrength = EditorGUILayout.FloatField("Camera Shake", h.cameraShakeStrength);
            h.impactTier = (DamageImpactTier)EditorGUILayout.EnumPopup("Impact UI Tier", h.impactTier);

            EditorGUILayout.Space(5);
            
            // Enemy Reaction (Hidden in a sub-foldout to save vertical space)
            if (!reactionFoldouts.ContainsKey(i)) reactionFoldouts[i] = false;
            reactionFoldouts[i] = EditorGUILayout.Foldout(reactionFoldouts[i], "Target Reaction Details (Knockback/Hitstun)", true);
            
            if (reactionFoldouts[i])
            {
                EditorGUI.indentLevel++;
                h.hitstunFrames = EditorGUILayout.IntField("Impact Freeze (Hitstop)", h.hitstunFrames);
                h.knockbackFrames = EditorGUILayout.IntField("Hitstun Duration", h.knockbackFrames);
                h.reactionVelocity = EditorGUILayout.Vector2Field("Reaction Velocity (X,Y)", h.reactionVelocity);
                h.isKnockdown = EditorGUILayout.Toggle("Is Knockdown Launch", h.isKnockdown);
                
                EditorGUILayout.Space(5);
                h.snapToAttacker = EditorGUILayout.Toggle("Target Snapping", h.snapToAttacker);
                if (h.snapToAttacker)
                {
                    h.snapOffset = EditorGUILayout.Vector2Field("Snap Offset", h.snapOffset);
                }

                EditorGUILayout.Space(5);
                h.flashColor = EditorGUILayout.ColorField("Sprite Flash Color", h.flashColor);
                h.flashFrames = EditorGUILayout.IntField("Sprite Flash Frames", h.flashFrames);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }

    private void AddHitFrameAtScrubber(AttackStepData step)
    {
        int offset = scrubberFrame - step.startupFrames;
        if (offset < 0) offset = 0;

        AttackHitFrameData newFrame = new AttackHitFrameData();
        newFrame.startFrameOffset = offset;
        newFrame.activeFrames = 1; 

        var list = step.hitFrames != null ? new List<AttackHitFrameData>(step.hitFrames) : new List<AttackHitFrameData>();
        list.Add(newFrame);
        step.hitFrames = list.ToArray();
        
        EditorUtility.SetDirty(selectedCharacter);
        Debug.Log("Hit Frame Added at Scrubber Frame: " + scrubberFrame + " (Offset stored as: " + offset + ")");
    }

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

    private void OnSceneGUI(SceneView sceneView)
    {
        if (selectedCharacter == null || previewInstance == null) return;

        AttackStepData step = GetCurrentStep();
        if (step == null || step.hitFrames == null) return;

        bool changed = false;

        for (int i = 0; i < step.hitFrames.Length; i++)
        {
            AttackHitFrameData h = step.hitFrames[i];

            // Only show/edit if scrubber is within the specific hit frame's active window
            int startAt = step.startupFrames + h.startFrameOffset;
            int endAt = startAt + h.activeFrames;

            if (scrubberFrame >= startAt && scrubberFrame < endAt)
            {
                // Calculate world position based on offset
                Vector3 worldPos = previewInstance.transform.TransformPoint(new Vector3(h.offset.x, h.offset.y, 0));

                // Draw Solid Hitbox
                Color fillColor = h.debugColor;
                fillColor.a = 0.3f; // Semi-transparent solid
                Handles.color = fillColor;
                
                // Construct rectangle points in world space
                Vector3 halfSize = new Vector3(h.size.x * 0.5f, h.size.y * 0.5f, 0);
                Quaternion rot = previewInstance.transform.rotation;
                
                Vector3 p1 = worldPos + rot * new Vector3(-halfSize.x, -halfSize.y, 0);
                Vector3 p2 = worldPos + rot * new Vector3(halfSize.x, -halfSize.y, 0);
                Vector3 p3 = worldPos + rot * new Vector3(halfSize.x, halfSize.y, 0);
                Vector3 p4 = worldPos + rot * new Vector3(-halfSize.x, halfSize.y, 0);

                Handles.DrawSolidRectangleWithOutline(new Vector3[] { p1, p2, p3, p4 }, fillColor, h.debugColor);

                // --- POSITIONAL HANDLE (CENTER) ---
                EditorGUI.BeginChangeCheck();
                Handles.Label(worldPos + Vector3.up * (h.size.y * 0.6f), "Impact #" + (i + 1), EditorStyles.miniBoldLabel);
                float handleSize = 0.05f * HandleUtility.GetHandleSize(worldPos);
                Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, handleSize, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 localPos = previewInstance.transform.InverseTransformPoint(newWorldPos);
                    h.offset = new Vector2(localPos.x, localPos.y);
                    changed = true;
                }

                // --- SCALING HANDLES (EDGES) ---
                const float minSize = 0.1f;
                Handles.color = Color.white;

                // 1. RIGHT HANDLE
                Vector3 rightWorld = worldPos + rot * new Vector3(halfSize.x, 0, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newRightWorld = Handles.FreeMoveHandle(rightWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 localPos = previewInstance.transform.InverseTransformPoint(newRightWorld);
                    float oldLeft = h.offset.x - (h.size.x * 0.5f);
                    float newWidth = localPos.x - oldLeft;
                    if (newWidth < minSize) newWidth = minSize;
                    h.size.x = newWidth;
                    h.offset.x = oldLeft + (newWidth * 0.5f);
                    changed = true;
                }

                // 2. LEFT HANDLE
                Vector3 leftWorld = worldPos + rot * new Vector3(-halfSize.x, 0, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newLeftWorld = Handles.FreeMoveHandle(leftWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 localPos = previewInstance.transform.InverseTransformPoint(newLeftWorld);
                    float oldRight = h.offset.x + (h.size.x * 0.5f);
                    float newWidth = oldRight - localPos.x;
                    if (newWidth < minSize) newWidth = minSize;
                    h.size.x = newWidth;
                    h.offset.x = oldRight - (newWidth * 0.5f);
                    changed = true;
                }

                // 3. TOP HANDLE
                Vector3 topWorld = worldPos + rot * new Vector3(0, halfSize.y, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newTopWorld = Handles.FreeMoveHandle(topWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 localPos = previewInstance.transform.InverseTransformPoint(newTopWorld);
                    float oldBottom = h.offset.y - (h.size.y * 0.5f);
                    float newHeight = localPos.y - oldBottom;
                    if (newHeight < minSize) newHeight = minSize;
                    h.size.y = newHeight;
                    h.offset.y = oldBottom + (newHeight * 0.5f);
                    changed = true;
                }

                // 4. BOTTOM HANDLE
                Vector3 bottomWorld = worldPos + rot * new Vector3(0, -halfSize.y, 0);
                EditorGUI.BeginChangeCheck();
                Vector3 newBottomWorld = Handles.FreeMoveHandle(bottomWorld, handleSize * 0.8f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 localPos = previewInstance.transform.InverseTransformPoint(newBottomWorld);
                    float oldTop = h.offset.y + (h.size.y * 0.5f);
                    float newHeight = oldTop - localPos.y;
                    if (newHeight < minSize) newHeight = minSize;
                    h.size.y = newHeight;
                    h.offset.y = oldTop - (newHeight * 0.5f);
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

    private void CleanupPreview()
    {
        if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }
            
        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }
    }
}
