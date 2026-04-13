using UnityEditor;
using UnityEngine;
using CombatCore;
using System.Collections.Generic;

namespace CombatEditor
{
    public static class FrameDataMigrationUtility
    {
        [MenuItem("Combat/Migrate Frame Data (24 -> 60 FPS)")]
        public static void MigrateAllFrom24To60()
        {
            if (!EditorUtility.DisplayDialog("Migrate Frame Data", 
                "This will scale all CharacterObject frame data from 24 FPS to 60 FPS (ratio 2.5x).\n\n" +
                "Only assets with 'Authored FPS' < 60 will be processed. Continue?", "Migrate", "Cancel"))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:CharacterObject");
            int migratedCount = 0;
            int skippedCount = 0;
            float ratio = 60f / 24f;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CharacterObject character = AssetDatabase.LoadAssetAtPath<CharacterObject>(path);

                if (character != null)
                {
                    if (character.authoredFPS >= 60f)
                    {
                        skippedCount++;
                        continue;
                    }

                    Undo.RecordObject(character, "Migrate Frame Data to 60 FPS");
                    
                    // Rescale Dash
                    character.dashData?.Rescale(ratio);
                    
                    // Rescale Action Resolver Table
                    if (character.actionResolverTable != null)
                    {
                        character.actionResolverTable.groundComboTree?.Rescale(ratio);
                        character.actionResolverTable.airComboTree?.Rescale(ratio);
                        character.actionResolverTable.groundSkillTree?.Rescale(ratio);
                        character.actionResolverTable.airSkillTree?.Rescale(ratio);
                        character.actionResolverTable.groundUltimateTree?.Rescale(ratio);
                        character.actionResolverTable.airUltimateTree?.Rescale(ratio);

                        if (character.actionResolverTable.standaloneActions != null)
                        {
                            foreach (var sa in character.actionResolverTable.standaloneActions)
                            {
                                sa.stepData?.Rescale(ratio);
                                sa.followUpNode?.stepData?.Rescale(ratio);
                            }
                        }
                        EditorUtility.SetDirty(character.actionResolverTable);
                    }
                    
                    // Mark as migrated
                    character.authoredFPS = 60f;
                    
                    EditorUtility.SetDirty(character);
                    migratedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            
            string report = $"Migration Complete.\n- Migrated: {migratedCount}\n- Skipped: {skippedCount} (Already 60 FPS)";
            Debug.Log(report);
            EditorUtility.DisplayDialog("Migration Report", report, "OK");
        }
    }
}
