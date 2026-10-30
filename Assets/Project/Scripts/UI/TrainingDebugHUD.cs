using UnityEngine;
using CombatCore;

namespace CombatCore
{
    /// <summary>
    /// Industry-standard training debug HUD with 3 independently draggable panels:
    ///   1. Enemy Status   – HP bar, reaction state, stun bar, active status effects
    ///   2. Team Damage    – Per-character damage table with share %
    ///   3. DPS Showcase   – Prominent DPS readout, total damage, max hit
    ///
    /// Each panel uses GUI.Window + GUI.DragWindow for native drag support.
    /// Windows auto-size their height to fit content — no scrollbars.
    /// A toolbar strip at the screen top allows toggling each panel on/off.
    /// </summary>
    public class TrainingDebugHUD : MonoBehaviour
    {
        private static TrainingDebugHUD instance;
        private void Awake() => instance = this;

        public static bool IsPointerOverHud(Vector2 screenPosition)
        {
            if (instance == null || !TrainingDebugManager.ShowStatsHud) return false;
            if (Project.UI.Training.TrainingConfigurationPanel.Instance != null
                && Project.UI.Training.TrainingConfigurationPanel.Instance.IsOpen) return false;
            Vector2 point = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var toolbar = new Rect((Screen.width - 380f) * .5f - 8f, 4f, 396f, 28f);
            return toolbar.Contains(point) || (instance.rectsInitialized
                && ((instance.showEnemyStatus && instance.rectEnemyStatus.Contains(point))
                    || (instance.showTeamDamage && instance.rectTeamDamage.Contains(point))
                    || (instance.showDPSShowcase && instance.rectDPSShowcase.Contains(point))));
        }
        // =====================================================================
        // Window IDs (must be unique across the application)
        // =====================================================================
        private const int WINDOW_ID_ENEMY_STATUS = 91001;
        private const int WINDOW_ID_TEAM_DAMAGE  = 91002;
        private const int WINDOW_ID_DPS_SHOWCASE = 91003;

        // =====================================================================
        // Panel visibility toggles
        // =====================================================================
        private bool showEnemyStatus = true;
        private bool showTeamDamage  = true;
        private bool showDPSShowcase = true;

        // =====================================================================
        // Window rects (default positions, draggable at runtime)
        // =====================================================================
        private Rect rectEnemyStatus;
        private Rect rectTeamDamage;
        private Rect rectDPSShowcase;
        private bool rectsInitialized = false;

        // Measured content heights (updated each Repaint, applied next frame)
        private float measuredHeightEnemyStatus;
        private float measuredHeightTeamDamage;
        private float measuredHeightDPSShowcase;

        // =====================================================================
        // Style cache
        // =====================================================================
        private GUIStyle stylePanelTitle;
        private GUIStyle styleSectionHeader;
        private GUIStyle styleLabel;
        private GUIStyle styleValue;
        private GUIStyle styleValueLarge;
        private GUIStyle styleStatusEffect;
        private GUIStyle styleToolbarBtn;
        private GUIStyle styleToolbarBtnActive;
        private GUIStyle styleBarBackground;
        private GUIStyle styleBarFillHP;
        private GUIStyle styleBarFillStun;
        private GUIStyle styleBarFillShare;
        private GUIStyle styleSeparator;
        private GUIStyle styleWindowBg;
        private GUIStyle styleToolbarBg;
        private bool stylesInitialized = false;

        // Texture cache (created once, destroyed on component destroy)
        private Texture2D texDarkBg;
        private Texture2D texToolbarBg;
        private Texture2D texBarBg;
        private Texture2D texBarHP;
        private Texture2D texBarStun;
        private Texture2D texBarShare;
        private Texture2D texSeparator;
        private Texture2D texBtnNormal;
        private Texture2D texBtnActive;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            DestroyTextures();
        }

        // =====================================================================
        // OnGUI – Entry point
        // =====================================================================

        private void OnGUI()
        {
            if (!TrainingDebugManager.ShowStatsHud) return;
            if (Project.UI.Training.TrainingConfigurationPanel.Instance != null
                && Project.UI.Training.TrainingConfigurationPanel.Instance.IsOpen) return;
            if (Project.UI.PauseMenuController.IsOpen) return;

            if (!stylesInitialized) InitializeStyles();
            if (!rectsInitialized) InitializeRects();

            DrawToolbar();

            var stats = TrainingDebugStats.Instance;
            if (stats == null) return;

            // Apply measured heights from previous frame (avoids layout/repaint mismatch)
            if (measuredHeightEnemyStatus > 0f)
                rectEnemyStatus.height = measuredHeightEnemyStatus;
            if (measuredHeightTeamDamage > 0f)
                rectTeamDamage.height = measuredHeightTeamDamage;
            if (measuredHeightDPSShowcase > 0f)
                rectDPSShowcase.height = measuredHeightDPSShowcase;

            if (showEnemyStatus)
                rectEnemyStatus = GUI.Window(WINDOW_ID_ENEMY_STATUS, rectEnemyStatus, DrawEnemyStatusWindow, "", styleWindowBg);

            if (showTeamDamage)
                rectTeamDamage = GUI.Window(WINDOW_ID_TEAM_DAMAGE, rectTeamDamage, DrawTeamDamageWindow, "", styleWindowBg);

            if (showDPSShowcase)
                rectDPSShowcase = GUI.Window(WINDOW_ID_DPS_SHOWCASE, rectDPSShowcase, DrawDPSShowcaseWindow, "", styleWindowBg);
        }

        // =====================================================================
        // Toolbar
        // =====================================================================

        private void DrawToolbar()
        {
            float toolbarHeight = 28f;
            float btnWidth = 120f;
            float totalWidth = btnWidth * 3 + 20f;
            float startX = (Screen.width - totalWidth) * 0.5f;

            GUI.Box(new Rect(startX - 8f, 4f, totalWidth + 16f, toolbarHeight), "", styleToolbarBg);

            float x = startX;
            float y = 6f;

            showEnemyStatus = GUI.Toggle(new Rect(x, y, btnWidth, 22f), showEnemyStatus,
                "Enemy Status", showEnemyStatus ? styleToolbarBtnActive : styleToolbarBtn);
            x += btnWidth + 6f;

            showTeamDamage = GUI.Toggle(new Rect(x, y, btnWidth, 22f), showTeamDamage,
                "Team Damage", showTeamDamage ? styleToolbarBtnActive : styleToolbarBtn);
            x += btnWidth + 6f;

            showDPSShowcase = GUI.Toggle(new Rect(x, y, btnWidth, 22f), showDPSShowcase,
                "DPS Showcase", showDPSShowcase ? styleToolbarBtnActive : styleToolbarBtn);
        }

        // =====================================================================
        // Panel 1 – Enemy Status
        // =====================================================================

        private void DrawEnemyStatusWindow(int windowId)
        {
            var stats = TrainingDebugStats.Instance;
            if (stats == null) { GUI.DragWindow(); return; }

            GUILayout.BeginVertical();
            GUILayout.Space(4f);

            // Title
            GUILayout.Label("⚔  ENEMY STATUS", stylePanelTitle);
            DrawSeparator();

            // Target name
            string enemyName = stats.lastAttackedEnemy != null ? stats.lastAttackedEnemy.name : "—";
            DrawStatRow("Target", enemyName);

            // HP bar
            if (stats.lastAttackedEnemy != null)
            {
                float hpPct = Mathf.Clamp01(stats.lastAttackedEnemy.currentHP / Mathf.Max(stats.lastAttackedEnemy.maxHP, 1f));
                GUILayout.Space(2f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("HP", styleLabel, GUILayout.Width(45f));
                DrawProgressBar(hpPct, styleBarFillHP,
                    $"{stats.lastAttackedEnemy.currentHP:F0} / {stats.lastAttackedEnemy.maxHP:F0}  ({hpPct * 100f:F0}%)");
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);

            // Reaction state
            string stateName = stats.GetEnemyStateName();
            Color stateColor = GetStateColor(stateName);
            GUIStyle stateStyle = new GUIStyle(styleValue);
            stateStyle.normal.textColor = stateColor;
            GUILayout.BeginHorizontal();
            GUILayout.Label("State", styleLabel, GUILayout.Width(45f));
            GUILayout.Label(stateName, stateStyle);
            GUILayout.EndHorizontal();

            // Stun bar
            float stunProgress = stats.GetStunProgress();
            float stunSeconds = stats.GetRemainingStunSeconds();
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Stun", styleLabel, GUILayout.Width(45f));
            DrawProgressBar(stunProgress, styleBarFillStun, $"{stunSeconds:F1}s");
            GUILayout.EndHorizontal();

            // Status effects (no scrollbar – window grows to fit)
            var statuses = stats.GetActiveStatusEffects();
            if (statuses.Count > 0)
            {
                GUILayout.Space(6f);
                DrawSeparator();
                GUILayout.Label("STATUS EFFECTS", styleSectionHeader);

                for (int i = 0; i < statuses.Count; i++)
                {
                    var s = statuses[i];
                    string details = !string.IsNullOrEmpty(s.modifierDetails) ? $"  ({s.modifierDetails})" : "";
                    string duration = s.isInfinite ? "∞" : $"{s.remainingDuration:F1}s";
                    string stack = s.stackCount > 1 ? $" ×{s.stackCount}" : "";
                    GUILayout.Label($"• {s.displayName}{stack}{details}  [{duration}]", styleStatusEffect);
                }
            }

            GUILayout.EndVertical();

            // Measure actual content height on Repaint pass
            if (Event.current.type == EventType.Repaint)
            {
                measuredHeightEnemyStatus = GUILayoutUtility.GetLastRect().yMax + 20f;
            }

            GUI.DragWindow();
        }

        // =====================================================================
        // Panel 2 – Team Damage
        // =====================================================================

        private void DrawTeamDamageWindow(int windowId)
        {
            var stats = TrainingDebugStats.Instance;
            if (stats == null) { GUI.DragWindow(); return; }

            var records = stats.CharacterDamageRecords;

            GUILayout.BeginVertical();
            GUILayout.Space(4f);

            GUILayout.Label("👥  TEAM DAMAGE", stylePanelTitle);
            DrawSeparator();

            // Table header
            GUILayout.BeginHorizontal();
            GUILayout.Label("Character", styleSectionHeader, GUILayout.Width(130f));
            GUILayout.Label("Total", styleSectionHeader, GUILayout.Width(70f));
            GUILayout.Label("Share", styleSectionHeader, GUILayout.Width(60f));
            GUILayout.Label("Max Hit", styleSectionHeader, GUILayout.Width(70f));
            GUILayout.EndHorizontal();

            DrawSeparator();

            if (records.Count == 0)
            {
                GUILayout.Label("No data", styleLabel);
            }
            else
            {
                for (int i = 0; i < records.Count; i++)
                {
                    var rec = records[i];
                    float share = stats.GetTeamDamageSharePercent(rec);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(TrainingDebugManager.GetCharacterDisplayName(rec.character),
                        styleLabel, GUILayout.Width(130f));
                    GUILayout.Label(rec.totalDamage.ToString("F0"), styleValue, GUILayout.Width(70f));
                    GUILayout.Label($"{share:F1}%", styleValue, GUILayout.Width(60f));
                    GUILayout.Label(rec.maxDamageHit.ToString("F0"), styleValue, GUILayout.Width(70f));
                    GUILayout.EndHorizontal();

                    // Share bar
                    Rect barRect = GUILayoutUtility.GetRect(0f, 4f, GUILayout.ExpandWidth(true));
                    GUI.Box(barRect, "", styleBarBackground);
                    if (share > 0f)
                    {
                        Rect fillRect = new Rect(barRect.x, barRect.y,
                            barRect.width * Mathf.Clamp01(share / 100f), barRect.height);
                        GUI.Box(fillRect, "", styleBarFillShare);
                    }

                    GUILayout.Space(2f);
                }
            }

            // Summary
            if (stats.totalDamageOnCurrentTarget > 0f)
            {
                DrawSeparator();
                DrawStatRow("Total", $"{stats.totalDamageOnCurrentTarget:F0}");
            }

            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                measuredHeightTeamDamage = GUILayoutUtility.GetLastRect().yMax + 20f;
            }

            GUI.DragWindow();
        }

        // =====================================================================
        // Panel 3 – DPS Showcase
        // =====================================================================

        private void DrawDPSShowcaseWindow(int windowId)
        {
            var stats = TrainingDebugStats.Instance;
            if (stats == null) { GUI.DragWindow(); return; }

            GUILayout.BeginVertical();
            GUILayout.Space(4f);

            GUILayout.Label("📊  DPS SHOWCASE", stylePanelTitle);
            DrawSeparator();

            // Large DPS value — show "—" during the 1s warmup window
            float dps = stats.GetDPS();
            string dpsText = (stats.dpsStartTime >= 0f && (Time.time - stats.dpsStartTime) < 1f)
                ? "—"
                : $"{dps:F1}";
            GUILayout.Label(dpsText, styleValueLarge);
            GUILayout.Label("damage per second", styleLabel);

            GUILayout.Space(6f);
            DrawSeparator();

            DrawStatRow("Total Damage", $"{stats.totalDamageOnCurrentTarget:F0}");
            DrawStatRow("Max Hit", $"{stats.maxDamageHit:F0}");
            DrawStatRow("Last Hit", $"{stats.lastDamagePerHit:F0}");

            // Elapsed sample time
            if (stats.dpsStartTime >= 0f)
            {
                float elapsed = Time.time - stats.dpsStartTime;
                DrawStatRow("Sample Time", $"{elapsed:F1}s");
            }

            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                measuredHeightDPSShowcase = GUILayoutUtility.GetLastRect().yMax + 20f;
            }

            GUI.DragWindow();
        }

        // =====================================================================
        // Helper: Draw a horizontal progress bar
        // =====================================================================

        private void DrawProgressBar(float fillPercent, GUIStyle fillStyle, string overlayText)
        {
            Rect barRect = GUILayoutUtility.GetRect(0f, 16f, GUILayout.ExpandWidth(true));
            GUI.Box(barRect, "", styleBarBackground);

            if (fillPercent > 0f)
            {
                Rect fillRect = new Rect(barRect.x, barRect.y,
                    barRect.width * Mathf.Clamp01(fillPercent), barRect.height);
                GUI.Box(fillRect, "", fillStyle);
            }

            // Overlay text centered on bar
            GUIStyle overlay = new GUIStyle(styleLabel);
            overlay.alignment = TextAnchor.MiddleCenter;
            overlay.fontSize = 10;
            overlay.normal.textColor = Color.white;
            GUI.Label(barRect, overlayText, overlay);
        }

        // =====================================================================
        // Helper: Draw a stat label/value row
        // =====================================================================

        private void DrawStatRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, styleLabel, GUILayout.Width(90f));
            GUILayout.Label(value, styleValue);
            GUILayout.EndHorizontal();
        }

        // =====================================================================
        // Helper: Draw a horizontal separator line
        // =====================================================================

        private void DrawSeparator()
        {
            GUILayout.Space(2f);
            Rect sep = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            GUI.Box(sep, "", styleSeparator);
            GUILayout.Space(2f);
        }

        // =====================================================================
        // Helper: Map reaction state name → color
        // =====================================================================

        private Color GetStateColor(string stateName)
        {
            switch (stateName)
            {
                case "None":
                case "Idle":
                    return new Color(0.4f, 1f, 0.4f); // green
                case "HitStun":
                case "Hitstun":
                    return new Color(1f, 0.35f, 0.35f); // red
                case "Knockback":
                case "Launch":
                case "Airborne":
                    return new Color(1f, 0.6f, 0.2f); // orange
                case "Knockdown":
                case "Down":
                    return new Color(1f, 1f, 0.3f); // yellow
                default:
                    return Color.white;
            }
        }

        // =====================================================================
        // Initialization
        // =====================================================================

        private void InitializeRects()
        {
            float sw = Screen.width;
            float margin = 40f; // below toolbar

            if (TrainingDebugManager.IsTrainingPresentation)
            {
                // Leave the currency, configuration controls and team portraits unobstructed.
                float width = Mathf.Min(300f, (sw - 80f) / 3.2f);
                float top = Mathf.Max(100f, Screen.height * .13f);
                rectEnemyStatus = new Rect(20f, top, width, 200f);
                rectTeamDamage = new Rect(40f + width, top, width * 1.2f, 180f);
                rectDPSShowcase = new Rect(60f + width * 2.2f, top, width, 200f);
                rectsInitialized = true;
                return;
            }

            // Default layout: 3 panels arranged in the top-right area
            rectEnemyStatus = new Rect(sw - 320f, margin, 300f, 200f);
            rectTeamDamage  = new Rect(sw - 640f, margin, 360f, 180f);
            rectDPSShowcase = new Rect(sw - 320f, 260f, 300f, 200f);

            rectsInitialized = true;
        }

        private void InitializeStyles()
        {
            // ── Textures ────────────────────────────────────────────
            texDarkBg     = MakeSolidTex(new Color(0.08f, 0.08f, 0.12f, 0.92f));
            texToolbarBg  = MakeSolidTex(new Color(0.06f, 0.06f, 0.10f, 0.90f));
            texBarBg      = MakeSolidTex(new Color(0.15f, 0.15f, 0.20f, 0.85f));
            texBarHP      = MakeSolidTex(new Color(0.2f,  0.85f, 0.35f, 1f));
            texBarStun    = MakeSolidTex(new Color(1f,    0.75f, 0.1f,  1f));
            texBarShare   = MakeSolidTex(new Color(0.35f, 0.65f, 1f,    0.9f));
            texSeparator  = MakeSolidTex(new Color(0.35f, 0.35f, 0.45f, 0.6f));
            texBtnNormal  = MakeSolidTex(new Color(0.18f, 0.18f, 0.25f, 0.85f));
            texBtnActive  = MakeSolidTex(new Color(0.25f, 0.45f, 0.75f, 0.9f));

            // ── Window background ───────────────────────────────────
            styleWindowBg = new GUIStyle(GUI.skin.window);
            styleWindowBg.normal.background  = texDarkBg;
            styleWindowBg.onNormal.background = texDarkBg;
            styleWindowBg.focused.background  = texDarkBg;
            styleWindowBg.padding = new RectOffset(10, 10, 6, 8);
            styleWindowBg.border = new RectOffset(0, 0, 0, 0);
            styleWindowBg.normal.textColor = Color.clear; // hide default title

            // ── Toolbar background ──────────────────────────────────
            styleToolbarBg = new GUIStyle();
            styleToolbarBg.normal.background = texToolbarBg;

            // ── Panel title ─────────────────────────────────────────
            stylePanelTitle = new GUIStyle(GUI.skin.label);
            stylePanelTitle.fontSize = 13;
            stylePanelTitle.fontStyle = FontStyle.Bold;
            stylePanelTitle.normal.textColor = new Color(0.55f, 0.85f, 1f);
            stylePanelTitle.alignment = TextAnchor.MiddleLeft;

            // ── Section header ──────────────────────────────────────
            styleSectionHeader = new GUIStyle(GUI.skin.label);
            styleSectionHeader.fontSize = 11;
            styleSectionHeader.fontStyle = FontStyle.Bold;
            styleSectionHeader.normal.textColor = new Color(0.7f, 0.75f, 0.85f);

            // ── Label (left column) ─────────────────────────────────
            styleLabel = new GUIStyle(GUI.skin.label);
            styleLabel.fontSize = 11;
            styleLabel.normal.textColor = new Color(0.65f, 0.65f, 0.72f);

            // ── Value (right column) ────────────────────────────────
            styleValue = new GUIStyle(GUI.skin.label);
            styleValue.fontSize = 11;
            styleValue.fontStyle = FontStyle.Bold;
            styleValue.normal.textColor = Color.white;

            // ── Large value (DPS readout) ───────────────────────────
            styleValueLarge = new GUIStyle(GUI.skin.label);
            styleValueLarge.fontSize = 28;
            styleValueLarge.fontStyle = FontStyle.Bold;
            styleValueLarge.normal.textColor = new Color(1f, 0.85f, 0.3f);
            styleValueLarge.alignment = TextAnchor.MiddleCenter;

            // ── Status effect text ──────────────────────────────────
            styleStatusEffect = new GUIStyle(GUI.skin.label);
            styleStatusEffect.fontSize = 10;
            styleStatusEffect.normal.textColor = new Color(0.85f, 0.85f, 0.9f);
            styleStatusEffect.wordWrap = true;

            // ── Toolbar buttons ─────────────────────────────────────
            styleToolbarBtn = new GUIStyle(GUI.skin.button);
            styleToolbarBtn.fontSize = 10;
            styleToolbarBtn.fontStyle = FontStyle.Bold;
            styleToolbarBtn.normal.background = texBtnNormal;
            styleToolbarBtn.normal.textColor = new Color(0.6f, 0.6f, 0.7f);
            styleToolbarBtn.hover.background = texBtnActive;
            styleToolbarBtn.hover.textColor = Color.white;
            styleToolbarBtn.alignment = TextAnchor.MiddleCenter;
            styleToolbarBtn.padding = new RectOffset(6, 6, 3, 3);
            styleToolbarBtn.border = new RectOffset(0, 0, 0, 0);

            styleToolbarBtnActive = new GUIStyle(styleToolbarBtn);
            styleToolbarBtnActive.normal.background = texBtnActive;
            styleToolbarBtnActive.normal.textColor = Color.white;

            // ── Bar styles ──────────────────────────────────────────
            styleBarBackground = new GUIStyle();
            styleBarBackground.normal.background = texBarBg;

            styleBarFillHP = new GUIStyle();
            styleBarFillHP.normal.background = texBarHP;

            styleBarFillStun = new GUIStyle();
            styleBarFillStun.normal.background = texBarStun;

            styleBarFillShare = new GUIStyle();
            styleBarFillShare.normal.background = texBarShare;

            // ── Separator ───────────────────────────────────────────
            styleSeparator = new GUIStyle();
            styleSeparator.normal.background = texSeparator;

            stylesInitialized = true;
        }

        // =====================================================================
        // Texture helpers
        // =====================================================================

        private Texture2D MakeSolidTex(Color color)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.DontSave;
            return tex;
        }

        private void DestroyTextures()
        {
            DestroyTex(ref texDarkBg);
            DestroyTex(ref texToolbarBg);
            DestroyTex(ref texBarBg);
            DestroyTex(ref texBarHP);
            DestroyTex(ref texBarStun);
            DestroyTex(ref texBarShare);
            DestroyTex(ref texSeparator);
            DestroyTex(ref texBtnNormal);
            DestroyTex(ref texBtnActive);
        }

        private void DestroyTex(ref Texture2D tex)
        {
            if (tex != null)
            {
                Destroy(tex);
                tex = null;
            }
        }
    }
}
