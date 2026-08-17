using System.Text;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;
using UnityEditor;
using UnityEngine;

public static class ArtifactMigrationReport
{
    [MenuItem("Tools/Project/Artifacts/Dry-Run Legacy Migration Report")]
    public static void LogDryRunReport()
    {
        StringBuilder report = new StringBuilder();
        string[] artifactGuids = AssetDatabase.FindAssets("t:Artifact", new[] { "Assets/Project/Resources/ItemSO" });
        report.AppendLine("Artifact v3 migration dry-run (no assets changed)");
        report.AppendLine($"Definitions found: {artifactGuids.Length}");

        for (int i = 0; i < artifactGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(artifactGuids[i]);
            Artifact artifact = AssetDatabase.LoadAssetAtPath<Artifact>(path);
            if (artifact == null) continue;
            ArtifactInstance imported = ArtifactLegacyImporter.Create(artifact, i, $"dry-run-{i}");
            report.Append($"- {artifact.id} ({artifact.itemName}): ");
            if (imported.rolls.Count == 0)
            {
                report.AppendLine("no legacy stat lines");
                continue;
            }

            for (int rollIndex = 0; rollIndex < imported.rolls.Count; rollIndex++)
            {
                ArtifactRoll roll = imported.rolls[rollIndex];
                if (rollIndex > 0) report.Append(", ");
                report.Append(ArtifactStatCatalog.GetLabel(roll.statKind));
                report.Append(' ');
                report.Append(roll.Value.ToString("0.##"));
            }
            report.AppendLine(" (quality unavailable; imported exactly)");
        }

        int ownedDefinitionReferences = CountObjectReferences<PlayerRunInventory>("ownedItems");
        int retiredArtifactFragmentStacks = CountRetiredArtifactFragments();
        report.AppendLine($"PlayerRunInventory owned-item references to inspect: {ownedDefinitionReferences}");
        report.AppendLine($"Retired Artifact fragment stacks that runtime v3 will report and omit: {retiredArtifactFragmentStacks}");
        report.AppendLine("Character Potential fragments are not modified.");
        Debug.Log(report.ToString());
    }

    private static int CountObjectReferences<T>(string propertyName) where T : ScriptableObject
    {
        int count = 0;
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        for (int i = 0; i < guids.Length; i++)
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            SerializedProperty property = asset != null ? new SerializedObject(asset).FindProperty(propertyName) : null;
            if (property == null || !property.isArray) continue;
            for (int elementIndex = 0; elementIndex < property.arraySize; elementIndex++)
                if (property.GetArrayElementAtIndex(elementIndex).objectReferenceValue is ArtifactDefinition) count++;
        }
        return count;
    }

    private static int CountRetiredArtifactFragments()
    {
        int count = 0;
        string[] guids = AssetDatabase.FindAssets("t:PlayerMetaProgression");
        for (int i = 0; i < guids.Length; i++)
        {
            PlayerMetaProgression asset = AssetDatabase.LoadAssetAtPath<PlayerMetaProgression>(AssetDatabase.GUIDToAssetPath(guids[i]));
            SerializedProperty fragments = asset != null ? new SerializedObject(asset).FindProperty("fragments") : null;
            if (fragments == null || !fragments.isArray) continue;
            for (int elementIndex = 0; elementIndex < fragments.arraySize; elementIndex++)
            {
                SerializedProperty type = fragments.GetArrayElementAtIndex(elementIndex).FindPropertyRelative("fragmentType");
                if (type != null && type.intValue == 1) count++;
            }
        }
        return count;
    }
}
