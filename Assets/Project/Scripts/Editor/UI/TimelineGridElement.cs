using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CombatEditor.UI
{
    /// <summary>
    /// Multi-track NLE-style timeline grid.
    /// 1 box = 1 frame at fixed pixel width. Horizontally scrollable.
    /// Dynamically adds/removes track rows based on ActionStepComponents.
    ///
    /// Layout (top → bottom):
    ///   • Ruler row   — frame numbers
    ///   • Phase row   — startup / active / recovery
    ///   • Dynamic track rows — one per attached component
    /// </summary>
    public class TimelineGridElement : VisualElement
    {
        // ── Colours ──
        private static readonly Color ColBg         = new Color(0.08f, 0.08f, 0.13f);
        private static readonly Color ColRuler       = new Color(0.15f, 0.15f, 0.22f);
        private static readonly Color ColRulerText   = new Color(0.45f, 0.45f, 0.60f);
        private static readonly Color ColGrid        = new Color(0.22f, 0.22f, 0.30f);
        private static readonly Color ColStartup     = new Color(0.18f, 0.72f, 0.38f);   // green
        private static readonly Color ColActive      = new Color(0.88f, 0.25f, 0.25f);   // red
        private static readonly Color ColRecovery    = new Color(0.25f, 0.55f, 0.95f);   // blue
        private static readonly Color ColPlayhead    = Color.white;
        private static readonly Color ColEmpty       = new Color(0.12f, 0.12f, 0.18f);

        // ── Layout constants ──
        private const float BOX_W        = 14f;
        private const float RULER_H      = 20f;
        private const float PHASE_H      = 24f;
        private const float TRACK_H      = 20f;
        private const float LABEL_W      = 62f;
        private const float ROW_GAP      = 2f;
        private const float PLAYHEAD_W   = 2f;

        // ── Public data ──
        public int   TotalFrames   { get; private set; }
        public float FrameRate     { get; private set; } = 24f;
        public int   StartupFrames { get; set; }
        public int   ActiveFrames  { get; set; }
        public int   RecoveryFrames{ get; set; }
        public int   CancelFrame   { get; set; }
        public int   PlayheadFrame { get; set; }

        // ── Dynamic tracks ──
        /// <summary>
        /// Represents one horizontal track row on the timeline.
        /// </summary>
        public class TimelineTrack
        {
            public string label;
            public Color  color;
            /// <summary>List of (startFrame, duration) ranges drawn as filled boxes.</summary>
            public List<(int start, int duration)> ranges = new List<(int, int)>();
            /// <summary>Optional diamond markers at specific frames.</summary>
            public List<int> diamonds = new List<int>();
        }

        public List<TimelineTrack> Tracks { get; } = new List<TimelineTrack>();

        // ── Internal scroll container ──
        private VisualElement _scrollContent;

        // ── Events ──
        public event System.Action<int>    OnSeek;
        public event System.Action<int, GameObject> OnVFXDropped;

        private bool _dragging;

        // =========================================================
        // CONSTRUCTOR
        // =========================================================
        public TimelineGridElement()
        {
            style.flexDirection = FlexDirection.Row;
            style.flexGrow      = 1;
            style.overflow      = Overflow.Hidden;

            // Fixed left label column
            var labelCol = new VisualElement();
            labelCol.style.width          = LABEL_W;
            labelCol.style.minWidth       = LABEL_W;
            labelCol.style.flexShrink     = 0;
            labelCol.style.backgroundColor = new StyleColor(ColBg);
            labelCol.name = "label-col";
            Add(labelCol);

            // Horizontally scrollable grid
            var scroller = new ScrollView(ScrollViewMode.Horizontal);
            scroller.style.flexGrow = 1;
            scroller.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scroller.verticalScrollerVisibility   = ScrollerVisibility.Hidden;

            _scrollContent = new VisualElement();
            _scrollContent.style.flexDirection = FlexDirection.Column;
            _scrollContent.generateVisualContent += DrawGrid;
            _scrollContent.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _scrollContent.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _scrollContent.RegisterCallback<PointerUpEvent>(OnPointerUp);

            // Drag-and-drop for VFX / Enemy prefabs
            _scrollContent.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            _scrollContent.RegisterCallback<DragPerformEvent>(OnDragPerform);

            scroller.Add(_scrollContent);
            Add(scroller);
        }

        // =========================================================
        // SETUP — called by the window whenever clip/data changes
        // =========================================================
        public void Setup(int totalFrames, float fps)
        {
            TotalFrames = Mathf.Max(1, totalFrames);
            FrameRate   = fps > 0 ? fps : 24f;
            SyncSize();
        }

        private void SyncSize()
        {
            float w = TotalFrames * BOX_W + 8f;
            float h = RULER_H + PHASE_H + ROW_GAP
                    + Tracks.Count * (TRACK_H + ROW_GAP)
                    + 4f;

            _scrollContent.style.width  = w;
            _scrollContent.style.height = h;
        }

        public void Refresh()
        {
            SyncSize();
            _scrollContent.MarkDirtyRepaint();

            // Rebuild label column
            var labelCol = this.Q("label-col");
            if (labelCol == null) return;
            labelCol.Clear();
            labelCol.style.height = _scrollContent.resolvedStyle.height;

            // Spacer for ruler + phase
            AddLabelRow(labelCol, "", RULER_H, ColBg);
            AddLabelRow(labelCol, "Phase", PHASE_H, ColBg);

            foreach (var track in Tracks)
            {
                string display = track.label.Length > 8 ? track.label.Substring(0, 7) + "…" : track.label;
                AddLabelRow(labelCol, display, TRACK_H, ColBg);
            }
        }

        private void AddLabelRow(VisualElement col, string text, float h, Color bg)
        {
            var row = new VisualElement();
            row.style.height     = h;
            row.style.minHeight  = h;
            row.style.marginBottom = ROW_GAP;
            row.style.backgroundColor = new StyleColor(bg);
            row.style.justifyContent  = Justify.Center;
            row.style.paddingLeft     = 4;

            var lbl = new Label(text);
            lbl.style.fontSize  = 9;
            lbl.style.color     = new StyleColor(ColRulerText);
            lbl.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            row.Add(lbl);
            col.Add(row);
        }

        // =========================================================
        // PAINTER2D GRID DRAW
        // =========================================================
        private void DrawGrid(MeshGenerationContext mgc)
        {
            var p     = mgc.painter2D;
            float w   = _scrollContent.resolvedStyle.width;
            float h   = _scrollContent.resolvedStyle.height;

            if (w < 1 || h < 1 || TotalFrames <= 0) return;

            float y = 0;

            // ── 1. Ruler ──
            DrawRect(p, 0, y, w, RULER_H, ColRuler);
            int rulerStep = TotalFrames <= 60 ? 5 : TotalFrames <= 120 ? 10 : 20;
            for (int f = 0; f <= TotalFrames; f++)
            {
                float fx = f * BOX_W;
                if (f % rulerStep == 0)
                {
                    DrawRect(p, fx, y, 1, RULER_H, ColGrid);
                }
                else if (rulerStep >= 5 && f % (rulerStep / 5 > 0 ? rulerStep / 5 : 1) == 0)
                {
                    DrawRect(p, fx, y + RULER_H * 0.5f, 1, RULER_H * 0.5f, ColGrid);
                }
            }
            y += RULER_H;

            // ── 2. Phase row — box per frame ──
            for (int f = 0; f < TotalFrames; f++)
            {
                float fx = f * BOX_W;
                Color c;
                if (f < StartupFrames)                              c = ColStartup;
                else if (f < StartupFrames + ActiveFrames)          c = ColActive;
                else if (f < StartupFrames + ActiveFrames + RecoveryFrames) c = ColRecovery;
                else                                                 c = ColEmpty;

                DrawBox(p, fx, y, BOX_W, PHASE_H, c);
            }
            y += PHASE_H + ROW_GAP;

            // ── 3. Dynamic track rows ──
            foreach (var track in Tracks)
            {
                // Background
                DrawRect(p, 0, y, TotalFrames * BOX_W, TRACK_H, ColEmpty);

                // Filled ranges
                foreach (var (start, duration) in track.ranges)
                {
                    for (int f = start; f < start + duration && f < TotalFrames; f++)
                    {
                        float fx = f * BOX_W;
                        Color c = track.color;
                        c.a = 0.85f;
                        DrawBox(p, fx, y, BOX_W, TRACK_H, c);
                    }
                }

                // Diamond markers
                foreach (int df in track.diamonds)
                {
                    if (df >= 0 && df < TotalFrames)
                    {
                        float dx = df * BOX_W + BOX_W * 0.5f;
                        float dy = y + TRACK_H * 0.5f;
                        DrawDiamond(p, dx, dy, 4f, Color.white);
                    }
                }

                y += TRACK_H + ROW_GAP;
            }

            // ── 4. Cancel Window Marker ──
            if (CancelFrame > 0 && CancelFrame < TotalFrames)
            {
                float cx = CancelFrame * BOX_W;
                p.strokeColor = new Color(1f, 0.8f, 0f, 0.6f); // Yellowish
                p.lineWidth = 1.5f;
                // Draw dashed vertical line
                for (float dashY = 0; dashY < y; dashY += 6)
                {
                    p.BeginPath();
                    p.MoveTo(new Vector2(cx, dashY));
                    p.LineTo(new Vector2(cx, Mathf.Min(dashY + 3, y)));
                    p.Stroke();
                }

                // Small triangular marker at top of phase row
                float markerY = RULER_H;
                p.fillColor = new Color(1f, 0.8f, 0f);
                p.BeginPath();
                p.MoveTo(new Vector2(cx, markerY));
                p.LineTo(new Vector2(cx - 4, markerY - 6));
                p.LineTo(new Vector2(cx + 4, markerY - 6));
                p.ClosePath();
                p.Fill();
            }

            // ── 5. Playhead ──
            float phX = PlayheadFrame * BOX_W + BOX_W * 0.5f;
            DrawRect(p, phX - PLAYHEAD_W * 0.5f, 0, PLAYHEAD_W, y, ColPlayhead);

            // Playhead triangle (top marker)
            p.fillColor = ColPlayhead;
            p.BeginPath();
            p.MoveTo(new Vector2(phX,       4f));
            p.LineTo(new Vector2(phX - 5f,  -3f));
            p.LineTo(new Vector2(phX + 5f,  -3f));
            p.ClosePath();
            p.Fill();
        }

        // ── Drawing helpers ──
        private static void DrawRect(Painter2D p, float x, float y, float w, float h, Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h));
            p.LineTo(new Vector2(x, y + h));
            p.ClosePath();
            p.Fill();
        }

        private static void DrawBox(Painter2D p, float x, float y, float w, float h, Color fill)
        {
            // Fill
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(new Vector2(x + 1,     y + 1));
            p.LineTo(new Vector2(x + w - 1, y + 1));
            p.LineTo(new Vector2(x + w - 1, y + h - 1));
            p.LineTo(new Vector2(x + 1,     y + h - 1));
            p.ClosePath();
            p.Fill();

            // 1px dark border (creates the grid line look)
            p.strokeColor = new Color(0, 0, 0, 0.4f);
            p.lineWidth   = 1f;
            p.BeginPath();
            p.MoveTo(new Vector2(x,     y));
            p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h));
            p.LineTo(new Vector2(x,     y + h));
            p.ClosePath();
            p.Stroke();
        }

        private static void DrawDiamond(Painter2D p, float cx, float cy, float size, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(cx, cy - size));
            p.LineTo(new Vector2(cx + size, cy));
            p.LineTo(new Vector2(cx, cy + size));
            p.LineTo(new Vector2(cx - size, cy));
            p.ClosePath();
            p.Fill();
        }

        // =========================================================
        // PIXEL ↔ FRAME HELPERS
        // =========================================================
        private int PixelToFrame(float localX)
        {
            int f = Mathf.FloorToInt(localX / BOX_W);
            return Mathf.Clamp(f, 0, TotalFrames - 1);
        }

        // =========================================================
        // POINTER INPUT — scrubbing
        // =========================================================
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            _dragging = true;
            _scrollContent.CapturePointer(evt.pointerId);
            int f = PixelToFrame(evt.localPosition.x);
            OnSeek?.Invoke(f);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging) return;
            int f = PixelToFrame(evt.localPosition.x);
            OnSeek?.Invoke(f);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_dragging) return;
            _dragging = false;
            _scrollContent.ReleasePointer(evt.pointerId);
        }

        // =========================================================
        // DRAG-AND-DROP — VFX / Enemy prefab from Project window
        // =========================================================
        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            bool hasPrefab = false;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject) { hasPrefab = true; break; }
            }
            DragAndDrop.visualMode = hasPrefab
                ? DragAndDropVisualMode.Link
                : DragAndDropVisualMode.Rejected;
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            DragAndDrop.AcceptDrag();
            int frame = PixelToFrame(evt.mousePosition.x);
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go)
                {
                    OnVFXDropped?.Invoke(frame, go);
                    break;
                }
            }
        }
    }
}
