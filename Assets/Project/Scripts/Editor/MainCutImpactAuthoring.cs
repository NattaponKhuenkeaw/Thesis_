#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Installs the gated URP pass and enables it on the existing barrage and finishing slash events.</summary>
public static class MainCutImpactAuthoring
{
    private const string RendererPath = "Assets/Settings/Renderer2D.asset";
    private const string MaterialPath = MainDimensionSlashAuthoring.Root + "CutImpactDistortion.mat";

    [MenuItem("Tools/Project/Combat/Author Main Cut Impact Distortion")]
    public static void Author()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset");
        var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
        var shader = Shader.Find("Project/CutImpactDistortion");
        if (character == null || character.actionResolverTable == null || renderer == null || shader == null)
            throw new InvalidOperationException("Main, its action table, Renderer2D or CutImpactDistortion shader is missing.");
        if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("CutImpactDistortion has shader import errors; check the Console.");

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Cut Impact Distortion" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            Undo.RecordObject(material, "Update Cut Impact Material");
            material.shader = shader;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        Undo.RecordObject(renderer, "Install Cut Impact Refraction");
        CutImpactRendererFeature feature = null;
        foreach (var existing in renderer.rendererFeatures)
            if (existing is CutImpactRendererFeature cutImpact) { feature = cutImpact; break; }
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<CutImpactRendererFeature>();
            feature.name = "Action Cut Impact Refraction";
            Undo.RegisterCreatedObjectUndo(feature, "Create Cut Impact Refraction");
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
        }
        Undo.RecordObject(feature, "Configure Cut Impact Refraction");
        feature.passMaterial = material;
        feature.SetActive(true);
        feature.Create();
        // Keep URP's feature/subasset recovery map consistent without saving other dirty assets.
        var serializedRenderer = new SerializedObject(renderer);
        var map = serializedRenderer.FindProperty("m_RendererFeatureMap");
        map.arraySize = renderer.rendererFeatures.Count;
        for (int i = 0; i < map.arraySize; i++)
        {
            long localId = 0;
            if (renderer.rendererFeatures[i] != null)
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out localId);
            map.GetArrayElementAtIndex(i).longValue = localId;
        }
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        renderer.SetDirty();
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssetIfDirty(renderer);

        var table = character.actionResolverTable;
        Undo.RecordObjects(new UnityEngine.Object[] { character, table }, "Author Main Cut Impact Distortion");
        var visited = new HashSet<ComboNode>();
        int count = 0;
        foreach (var action in table.comboActions) count += Configure(action?.comboTree?.rootNode, visited);
        foreach (var action in table.skillActions) count += Configure(action?.comboTree?.rootNode, visited);
        foreach (var action in table.ultimateActions) count += Configure(action?.comboTree?.rootNode, visited);
        foreach (var action in table.qteActions) count += Configure(action?.comboTree?.rootNode, visited);
        foreach (var action in table.standaloneActions) count += Configure(action?.comboTree?.rootNode, visited);
        count += Configure(character.qteSwap?.introComboTree?.rootNode, visited);
        var plunge = character.qteSwap?.plunge?.visuals;
        if (plunge != null)
        {
            plunge.impactDistortion ??= new CutImpactDistortionSettings();
            plunge.impactDistortion.enabled = false; plunge.impactDistortion.startFrame = 0;
            plunge.impactDistortion.durationFrames = 12; plunge.impactDistortion.waveWidth = 0.04f;
            plunge.impactDistortion.refractionPixels = 4f;
            plunge.impactDistortion.chromaticPixels = 0f; plunge.impactDistortion.blurPixels = 0f;
            plunge.suppressChromaticAberration = true;
            count++;
        }
        EditorUtility.SetDirty(table);
        EditorUtility.SetDirty(character);
        AssetDatabase.SaveAssetIfDirty(table);
        AssetDatabase.SaveAssetIfDirty(character);
        Debug.Log($"Cut impact refraction authored on {count} slash/sprite/plunge sources. Lightning Plunge refraction is disabled and uses no RGB split. No action trees or scene objects rebuilt.");
    }

    private static int Configure(ComboNode node, HashSet<ComboNode> visited)
    {
        if (node == null || !visited.Add(node)) return 0;
        int count = 0;
        var step = node.stepData;
        bool dimensionCut = step?.stepName == "DimensionCut";
        bool execution = step?.stepName == ExecutionBreakdownAuthoring.StepName;
        if ((dimensionCut || execution) && step.vfxData?.entries != null)
            foreach (var entry in step.vfxData.entries)
            {
                if (entry == null) continue;
                bool primary = dimensionCut || entry.vfxType == Project.Combat.VFXType.Main_DimensionSlash_BurstA
                    || entry.vfxType == Project.Combat.VFXType.Main_DimensionSlash_BurstB
                    || entry.vfxType == Project.Combat.VFXType.Main_DimensionSlash_BurstC;
                if (!primary) continue;
                entry.impactDistortion ??= new CutImpactDistortionSettings();
                entry.impactDistortion.enabled = true; entry.impactDistortion.startFrame = 0;
                entry.impactDistortion.durationFrames = Mathf.Clamp(entry.duration, 1, 18);
                count++;
            }
        if ((step?.stepName == "DimensionSlash" || step?.stepName == "DimensionSlashFinalCut" || step?.stepName == ExecutionBreakdownAuthoring.StepName)
            && step.eventData?.entries != null)
        {
            foreach (var entry in step.eventData.entries)
            {
                if (entry?.eventType != ActionEventType.ProceduralSlashField || entry.slashFieldSettings == null) continue;
                var impact = entry.slashFieldSettings.impactDistortion ??= new CutImpactDistortionSettings();
                impact.enabled = true;
                // The rendered ribbon/tear supplies the position and lifetime, including its reveal.
                impact.startFrame = 0;
                count++;
            }
        }
        count += Configure(node.defaultNext, visited);
        count += Configure(node.onGrabSuccessNode, visited);
        if (node.routes != null) foreach (var route in node.routes) count += Configure(route?.targetNode, visited);
        if (step?.triggerWindows != null) foreach (var trigger in step.triggerWindows) count += Configure(trigger?.targetNode, visited);
        return count;
    }
}
#endif
