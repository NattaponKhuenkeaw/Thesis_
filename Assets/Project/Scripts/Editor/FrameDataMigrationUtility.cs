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
                    
                    // Rescale Combos
                    character.basicAttackComboData?.Rescale(ratio);
                    character.neutralAirComboData?.Rescale(ratio);
                    
                    // Rescale Plunge
                    character.plungeAttackData?.Rescale(ratio);
                    
                    // Rescale Skill
                    character.skillData?.Rescale(ratio);
                    
                    // Rescale Ultimate
                    character.ultimateData?.Rescale(ratio);
                    
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
