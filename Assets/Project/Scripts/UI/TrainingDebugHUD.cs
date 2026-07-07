using UnityEngine;
using CombatCore;

namespace CombatCore
{
    /// <summary>
    /// Draws an OnGUI HUD for training debugging when TrainingDebugManager.IsActive is true.
    /// </summary>
    public class TrainingDebugHUD : MonoBehaviour
    {
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle barBackgroundStyle;
        private GUIStyle barForegroundStyle;
        private bool stylesInitialized = false;
        private float cachedHeight = 160f;

        private void InitializeStyles()
        {
            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = Color.cyan;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.white;

            // Background for the stun bar (dark grey)
            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            bgTex.Apply();
            barBackgroundStyle = new GUIStyle();
            barBackgroundStyle.normal.background = bgTex;

            // Foreground for the stun bar (yellow)
            Texture2D fgTex = new Texture2D(1, 1);
            fgTex.SetPixel(0, 0, new Color(1f, 0.8f, 0f, 1f));
            fgTex.Apply();
            barForegroundStyle = new GUIStyle();
            barForegroundStyle.normal.background = fgTex;

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!TrainingDebugManager.IsActive) return;
            if (!stylesInitialized) InitializeStyles();

            var stats = TrainingDebugStats.Instance;
            if (stats == null) return;

            var activeStatuses = stats.GetActiveStatusEffects();
            float width = 300;
            float margin = 10;
            Rect windowRect = new Rect(Screen.width - width - margin, margin, width, cachedHeight);

            GUI.Box(windowRect, ""); // Semi-transparent box background
            GUILayout.BeginArea(new Rect(windowRect.x + 10, windowRect.y + 5, width - 20, cachedHeight - 10));

            GUILayout.BeginVertical();

            GUILayout.Label("⚔ TRAINING DEBUG", headerStyle);
            GUILayout.Space(5);

            string enemyName = stats.lastAttackedEnemy != null ? stats.lastAttackedEnemy.name : "None";
            GUILayout.Label($"Target: {enemyName}", labelStyle);
            GUILayout.Label($"DPS: {stats.GetDPS():F1}", labelStyle);
            GUILayout.Label($"Total Damage: {stats.totalDamageOnCurrentTarget:F1}", labelStyle);

            GUILayout.Space(10);

            string stateName = stats.GetEnemyStateName();
            GUILayout.Label($"State: {stateName}", labelStyle);

            // Stun Bar
            float progress = stats.GetStunProgress();
            float stunSeconds = stats.GetRemainingStunSeconds();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Stun:", labelStyle, GUILayout.Width(40));
            
            Rect barRect = GUILayoutUtility.GetRect(100, 15);
            GUI.Box(barRect, "", barBackgroundStyle);
            if (progress > 0)
            {
                Rect progressRect = new Rect(barRect.x, barRect.y, barRect.width * progress, barRect.height);
                GUI.Box(progressRect, "", barForegroundStyle);
            }
            
            GUILayout.Space(5);
            GUILayout.Label($"{stunSeconds:F1}s", labelStyle);
            GUILayout.EndHorizontal();

            if (activeStatuses.Count > 0)
            {
                GUILayout.Space(10);
                GUILayout.Label("Status:", headerStyle);
                for (int i = 0; i < activeStatuses.Count; i++)
                {
                    var status = activeStatuses[i];
                    string details = !string.IsNullOrEmpty(status.modifierDetails) ? $" ({status.modifierDetails})" : "";
                    string duration = status.isInfinite ? "∞" : $"{status.remainingDuration:F1}s";
                    string stack = status.stackCount > 1 ? $" x{status.stackCount}" : "";
                    GUILayout.Label($"• {status.displayName}{stack}{details}: {duration}", labelStyle);
                }
            }

            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                // Set height based on layout content + bottom/top padding
                cachedHeight = GUILayoutUtility.GetLastRect().height + 20f;
            }

            GUILayout.EndArea();
        }
    }
}
