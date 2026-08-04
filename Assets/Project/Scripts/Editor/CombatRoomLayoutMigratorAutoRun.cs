#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// One-shot trigger: presence of the marker file runs combat_1 layout migration after domain reload.
    /// </summary>
    [InitializeOnLoad]
    internal static class CombatRoomLayoutMigratorAutoRun
    {
        private const string MarkerPath = "Assets/Project/Editor/.run-combat-layout-migrate";

        static CombatRoomLayoutMigratorAutoRun()
        {
            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            if (!File.Exists(MarkerPath))
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "[CombatRoomLayoutMigrator] Skipped auto-run while Play Mode is active. " +
                    "Exit Play Mode and use menu: Project/Rooms/Migrate combat_1 Into RoomLayout_Combat_01");
                return;
            }

            try
            {
                File.Delete(MarkerPath);
                string meta = MarkerPath + ".meta";
                if (File.Exists(meta))
                    File.Delete(meta);

                Debug.Log("[CombatRoomLayoutMigrator] Auto-run marker detected. Migrating combat_1...");
                bool ok = CombatRoomLayoutMigrator.MigrateCombat1Silent();
                Debug.Log(ok
                    ? "[CombatRoomLayoutMigrator] Auto-run finished successfully."
                    : "[CombatRoomLayoutMigrator] Auto-run finished with errors. See Console.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CombatRoomLayoutMigrator] Auto-run failed: {ex}");
            }
        }
    }
}
#endif
