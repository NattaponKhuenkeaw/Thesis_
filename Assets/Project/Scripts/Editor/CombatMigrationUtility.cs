using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace CombatEditor
{
    public static class CombatMigrationUtility
    {
        [MenuItem("Tools/Combat/Migrate All Characters to Combo Trees")]
        public static void MigrateAllCharacters()
        {
            MigrateAll(false);
        }

        [MenuItem("Tools/Combat/Force Migrate All (Overwrite)")]
        public static void ForceMigrateAll()
        {
            MigrateAll(true);
        }

        private static void MigrateAll(bool force)
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterObject");
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CharacterObject character = AssetDatabase.LoadAssetAtPath<CharacterObject>(path);

                if (character == null) continue;

                // Migrate if no table, or if force requested, or if the table appears unpopulated
                bool needsMigration = force || character.actionResolverTable == null;
                
                if (!needsMigration && character.actionResolverTable != null)
                {
                    needsMigration = character.actionResolverTable.groundComboTree == null || 
                                     character.actionResolverTable.groundComboTree.rootNode == null;
                }

                if (needsMigration)
                {
                    MigrateCharacter(character);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Successfully migrated {count} characters to the new Action Resolver system.");
        }

        public static void MigrateCharacter(CharacterObject character)
        {
            string charPath = AssetDatabase.GetAssetPath(character);
            string folder = Path.GetDirectoryName(charPath);
            string tableName = $"ActionResolverTable_{character.characterId}";
            string tablePath = Path.Combine(folder, $"{tableName}.asset");

            // 1. Create the Table Asset if it doesn't exist
            ActionResolverTable table = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(tablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<ActionResolverTable>();
                AssetDatabase.CreateAsset(table, tablePath);
            }

            Undo.RecordObject(table, "Migrate Legacy Combat Data");

            // Legacy migration logic (basicAttackComboData, neutralAirComboData, plungeAttackData) was removed 
            // as these fields no longer exist on CharacterObject.

            // 5. Link to Character Object
            Undo.RecordObject(character, "Assign Action Resolver Table");
            character.actionResolverTable = table;
            EditorUtility.SetDirty(character);

            Debug.Log($"Migrated data for {character.characterId} to {tablePath}", table);
        }
    }
}
