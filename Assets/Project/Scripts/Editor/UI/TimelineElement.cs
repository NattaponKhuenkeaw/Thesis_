using UnityEngine;
using UnityEngine.UIElements;

namespace CombatEditor.UI
{
    /// <summary>
    /// Custom VisualElement that renders the interactive frame-data timeline.
    /// Handles ruler, phase bars, hit-frame markers, lane rows, and playhead.
    /// </summary>
    public class TimelineElement : VisualElement
    {
        // ── Colors ──
        private static readonly Color BgColor        = new Color(0.078f, 0.078f, 0.125f);  // #141420
        private static readonly Color RulerTickColor  = new Color(0.3f, 0.3f, 0.4f);
        private static readonly Color StartupColor    = new Color(1f, 0.6f, 0.2f);          // Orange
        private static readonly Color ActivePhaseColor = new Color(0f, 0.83f, 1f);          // Cyan
        private static readonly Color RecoveryColor   = new Color(1f, 0.27f, 0.27f);        // Red
        private static readonly Color HitMarkerColor  = new Color(1f, 0.84f, 0f);           // Gold
        private static readonly Color CancelColor     = new Color(0.27f, 0.87f, 0.4f);      // Green
        private static readonly Color MotionColor     = new Color(0.2f, 0.7f, 0.3f, 0.6f);
        private static readonly Color PlayheadColor   = Color.white;
        private static readonly Color LaneBg          = new Color(0.1f, 0.1f, 0.16f);
        private static readonly Color PhaseLabelColor = new Color(0, 0, 0, 0.6f);

        // ── Layout Constants ──
        private const float RULER_HEIGHT  = 18f;
        private const float PHASE_HEIGHT  = 26f;
        private const float LANE_HEIGHT   = 14f;
        private const float LANE_GAP      = 2f;
        private const float LEFT_MARGIN   = 60f;  // space for lane labels
        private const float RIGHT_MARGIN  = 8f;

        // ── Data ──
        public int TotalFrames { get; set; }
        public int StartupFrames { get; set; }
        public int ActiveFrames { get; set; }
        public int RecoveryFrames { get; set; }
        public int PlayheadFrame { get; set; }

        // Hit frame data (start frame from beginning, duration)
        public struct HitMarker { public int start; public int duration; public Color color; }
        public System.Collections.Generic.List<HitMarker> HitMarkers { get; set; } = new();

        // Cancel window
        public bool HasCancel { get; set; }
        public int CancelStart { get; set; } // absolute frame

        // Forward motion
        public bool HasMotion { get; set; }
        public int MotionStart { get; set; }
        public int MotionEnd { get; set; }

        // ── Events ──
        public event System.Action<int> OnSeek;

        public TimelineElement()
        {
            style.flexGrow = 1;
            style.minHeight = 100;
            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private bool isDragging = false;

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0)
            {
                int frame = PixelToFrame(evt.localPosition.x);
                if (frame >= 0)
                {
                    OnSeek?.Invoke(frame);
                    isDragging = true;
                    this.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (isDragging)
            {
                int frame = PixelToFrame(evt.localPosition.x);
                if (frame >= 0)
                    OnSeek?.Invoke(frame);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (isDragging)
            {
                isDragging = false;
                this.ReleasePointer(evt.pointerId);
            }
        }

        private int PixelToFrame(float px)
        {
            if (TotalFrames <= 0) return -1;
            float barWidth = resolvedStyle.width - LEFT_MARGIN - RIGHT_MARGIN;
            float rel = px - LEFT_MARGIN;
            int frame = Mathf.RoundToInt((rel / barWidth) * TotalFrames);
            return Mathf.Clamp(frame, 0, TotalFrames);
        }

        private float FrameToPixel(int frame)
        {
            if (TotalFrames <= 0) return LEFT_MARGIN;
            float barWidth = resolvedStyle.width - LEFT_MARGIN - RIGHT_MARGIN;
            return LEFT_MARGIN + (frame / (float)TotalFrames) * barWidth;
        }

        // =========================================================
        // RENDERING
        // =========================================================
        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var painter = mgc.painter2D;
            float width = resolvedStyle.width;
            float height = resolvedStyle.height;

            if (TotalFrames <= 0 || width < 10) return;

            float barLeft = LEFT_MARGIN;
            float barWidth = width - LEFT_MARGIN - RIGHT_MARGIN;
            float pxPerFrame = barWidth / TotalFrames;

            float y = 0;

            // ── 1. Frame Ruler ──
            DrawRect(painter, 0, y, width, RULER_HEIGHT, BgColor);

            for (int f = 0; f <= TotalFrames; f++)
            {
                float x = barLeft + f * pxPerFrame;
                bool isMajor = (f % 10 == 0);
                bool isMinor = (f % 5 == 0) && !isMajor;

                if (isMajor || isMinor)
                {
                    float tickH = isMajor ? 10 : 5;
                    DrawRect(painter, x, y + RULER_HEIGHT - tickH, 1, tickH, RulerTickColor);
                }
            }
            y += RULER_HEIGHT;

            // ── 2. Phase Bars ──
            float suWidth = StartupFrames * pxPerFrame;
            float actWidth = ActiveFrames * pxPerFrame;
            float recWidth = RecoveryFrames * pxPerFrame;

            DrawRect(painter, barLeft, y, suWidth, PHASE_HEIGHT, StartupColor);
            DrawRect(painter, barLeft + suWidth, y, actWidth, PHASE_HEIGHT, ActivePhaseColor);
            DrawRect(painter, barLeft + suWidth + actWidth, y, recWidth, PHASE_HEIGHT, RecoveryColor);

            y += PHASE_HEIGHT;

            // ── 3. Hit Frame Lane ──
            float laneY = y + LANE_GAP;
            DrawRect(painter, barLeft, laneY, barWidth, LANE_HEIGHT, LaneBg);

            if (HitMarkers != null)
            {
                foreach (var hm in HitMarkers)
                {
                    float hx = barLeft + hm.start * pxPerFrame;
                    float hw = Mathf.Max(hm.duration * pxPerFrame, 3);
                    Color c = hm.color;
                    c.a = 0.7f;
                    DrawRect(painter, hx, laneY + 1, hw, LANE_HEIGHT - 2, c);

                    // Diamond marker
                    DrawDiamond(painter, hx, laneY + LANE_HEIGHT * 0.5f, 4, HitMarkerColor);
                }
            }
            laneY += LANE_HEIGHT + LANE_GAP;

            // ── 4. Cancel Lane ──
            if (HasCancel)
            {
                DrawRect(painter, barLeft, laneY, barWidth, LANE_HEIGHT, LaneBg);
                float cx = barLeft + CancelStart * pxPerFrame;
                float cw = barWidth - (CancelStart * pxPerFrame);
                if (cw > 0)
                    DrawRect(painter, cx, laneY + 1, cw, LANE_HEIGHT - 2, CancelColor * 0.5f);
                laneY += LANE_HEIGHT + LANE_GAP;
            }

            // ── 5. Motion Lane ──
            if (HasMotion)
            {
                DrawRect(painter, barLeft, laneY, barWidth, LANE_HEIGHT, LaneBg);
                float mx = barLeft + MotionStart * pxPerFrame;
                float mw = (MotionEnd - MotionStart + 1) * pxPerFrame;
                if (mw > 0)
                    DrawRect(painter, mx, laneY + 1, mw, LANE_HEIGHT - 2, MotionColor);
                laneY += LANE_HEIGHT + LANE_GAP;
            }

            // ── 6. Playhead ──
            float phX = FrameToPixel(PlayheadFrame);
            float phTop = 0;
            float phBottom = laneY;
            DrawRect(painter, phX - 1, phTop, 2, phBottom - phTop, PlayheadColor);

            // Playhead triangle
            painter.fillColor = PlayheadColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(phX, phTop + 2));
            painter.LineTo(new Vector2(phX - 5, phTop - 4));
            painter.LineTo(new Vector2(phX + 5, phTop - 4));
            painter.ClosePath();
            painter.Fill();
        }

        // ── Drawing Helpers ──
        private void DrawRect(Painter2D painter, float x, float y, float w, float h, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y));
            painter.LineTo(new Vector2(x + w, y));
            painter.LineTo(new Vector2(x + w, y + h));
            painter.LineTo(new Vector2(x, y + h));
            painter.ClosePath();
            painter.Fill();
        }

        private void DrawDiamond(Painter2D painter, float cx, float cy, float size, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx, cy - size));
            painter.LineTo(new Vector2(cx + size, cy));
            painter.LineTo(new Vector2(cx, cy + size));
            painter.LineTo(new Vector2(cx - size, cy));
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>Rebuild visual content from current data.</summary>
        public void Refresh()
        {
            MarkDirtyRepaint();
        }
    }
}
