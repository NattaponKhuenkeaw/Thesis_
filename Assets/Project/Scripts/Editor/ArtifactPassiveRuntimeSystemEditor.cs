using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ArtifactPassiveRuntimeSystem))]
public sealed class ArtifactPassiveRuntimeSystemEditor : Editor
{
    private const string PrefKey = "ArtifactPassive_DebugLogs_Enabled";

    [InitializeOnLoadMethod]
    private static void RestoreLoggingPreference()
    {
        ArtifactPassiveRuntimeSystem.GlobalDebugLogs = EditorPrefs.GetBool(PrefKey, true);
    }

    [MenuItem("Tools/Project/Artifacts/Diagnostics/Toggle Passive Debug Logs", false, 100)]
    public static void ToggleDebugLogsMenu()
    {
        bool current = ArtifactPassiveRuntimeSystem.GlobalDebugLogs;
        bool updated = !current;
        ArtifactPassiveRuntimeSystem.GlobalDebugLogs = updated;
        EditorPrefs.SetBool(PrefKey, updated);

        if (ArtifactPassiveRuntimeSystem.Instance != null)
        {
            ArtifactPassiveRuntimeSystem.Instance.EnableDebugLogs = updated;
        }

        Debug.Log($"[ArtifactPassive] Live Diagnostic Logging is now: {(updated ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>")}");
    }

    [MenuItem("Tools/Project/Artifacts/Diagnostics/Log Active Artifact State", false, 101)]
    public static void LogActiveArtifactState()
    {
        ArtifactPassiveRuntimeSystem system = ArtifactPassiveRuntimeSystem.Instance;
        if (system == null)
        {
            Debug.LogWarning("[ArtifactPassive] No active ArtifactPassiveRuntimeSystem instance in the current scene.");
            return;
        }

        IReadOnlyList<ArtifactPassiveRuntime> runtimes = system.ActiveRuntimes;
        Debug.Log($"=== [ArtifactPassive Diagnostic Snapshot] ({runtimes.Count} active runtimes, {system.ActiveInfusionCount} infusions) ===");

        for (int i = 0; i < runtimes.Count; i++)
        {
            ArtifactPassiveRuntime runtime = runtimes[i];
            string wearer = runtime.Wearer != null ? runtime.Wearer.name : "None";
            string artName = runtime.Artifact.Definition != null ? runtime.Artifact.Definition.itemName : "Unknown";
            Debug.Log($"[{i + 1}] Wearer: {wearer} | Artifact: {artName} (ID: {runtime.InstanceId})");

            for (int r = 0; r < runtime.Rules.Length; r++)
            {
                ArtifactRuleRuntimeState state = runtime.Rules[r];
                float cd = Mathf.Max(0f, state.cooldownReadyAt - Time.time);
                Debug.Log($"    - Rule #{r}: Stacks={state.stacks}, Armed={state.armed}, CooldownRemaining={cd:F1}s, Activations={state.activationSequence}");
            }
        }
    }

    public override void OnInspectorGUI()
    {
        ArtifactPassiveRuntimeSystem system = (ArtifactPassiveRuntimeSystem)target;

        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Artifact Passive Diagnostics", EditorStyles.boldLabel);

        bool isEnabled = system.IsLoggingEnabled;
        GUI.backgroundColor = isEnabled ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button(isEnabled ? "Live Debug Logs: ACTIVE (Click to Disable)" : "Live Debug Logs: OFF (Click to Enable)", GUILayout.Height(30)))
        {
            ToggleDebugLogsMenu();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        DrawDefaultInspector();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to view live runtime artifact state, stacks, and triggers.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Live Runtime State", EditorStyles.boldLabel);

        IReadOnlyList<ArtifactPassiveRuntime> runtimes = system.ActiveRuntimes;
        EditorGUILayout.LabelField($"Active Passives Count: {runtimes.Count}");
        EditorGUILayout.LabelField($"Active Infusions: {system.ActiveInfusionCount}");

        if (runtimes.Count == 0)
        {
            EditorGUILayout.HelpBox("No equipped artifacts with passive sets currently registered on deployed team.", MessageType.None);
        }
        else
        {
            for (int i = 0; i < runtimes.Count; i++)
            {
                ArtifactPassiveRuntime runtime = runtimes[i];
                string wearer = runtime.Wearer != null ? runtime.Wearer.name : "None";
                string artName = runtime.Artifact.Definition != null ? runtime.Artifact.Definition.itemName : "Unknown";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{artName} (Wearer: {wearer})", EditorStyles.boldLabel);

                for (int r = 0; r < runtime.Rules.Length; r++)
                {
                    ArtifactRuleRuntimeState state = runtime.Rules[r];
                    float cdRemaining = Mathf.Max(0f, state.cooldownReadyAt - Time.time);
                    string cdText = cdRemaining > 0.05f ? $"{cdRemaining:F1}s" : "Ready";
                    EditorGUILayout.LabelField($"  Rule #{r} -> Stacks: {state.stacks} | Armed: {state.armed} | CD: {cdText} | Activations: {state.activationSequence}");
                }
                EditorGUILayout.EndVertical();
            }
        }

        if (GUILayout.Button("Log State Snapshot to Console", GUILayout.Height(24)))
        {
            LogActiveArtifactState();
        }

        serializedObject.ApplyModifiedProperties();
        Repaint();
    }
}
