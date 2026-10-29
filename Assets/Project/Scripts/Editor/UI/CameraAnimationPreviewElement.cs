using UnityEngine;
using UnityEngine.UIElements;

namespace CombatEditor.UI
{
    public sealed class CameraAnimationPreviewElement : VisualElement
    {
        private CameraAnimationPose _baseline, _pose;
        private float _aspect = 1f;

        public CameraAnimationPreviewElement()
        {
            style.alignSelf = Align.Stretch;
            style.flexGrow = 1;
            style.minHeight = 50;
            style.overflow = Overflow.Hidden;
            generateVisualContent += Draw;
            tooltip = "Read-only schematic. Gray: normal viewport. Blue: animated viewport. Runtime room bounds may limit the requested framing.";
        }

        public void SetPose(CameraAnimationPose baseline, CameraAnimationPose pose, float aspect)
        {
            _baseline = baseline; _pose = pose; _aspect = aspect;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = contentRect.width, height = contentRect.height;
            if (width <= 0 || height <= 0 || _baseline.Size <= 0) return;
            Vector2 delta = _pose.Position - _baseline.Position;
            float halfX = Mathf.Max(_baseline.Size * _aspect, Mathf.Abs(delta.x) + _pose.Size * _aspect);
            float halfY = Mathf.Max(_baseline.Size, Mathf.Abs(delta.y) + _pose.Size);
            float scale = Mathf.Min((width - 10f) / (2f * halfX), (height - 8f) / (2f * halfY));
            Vector2 center = new Vector2(width / 2f, height / 2f);
            var painter = context.painter2D;
            void Box(Vector2 origin, float size, Color color)
            {
                Vector2 half = new Vector2(size * _aspect, size) * scale;
                painter.strokeColor = color; painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(origin - half);
                painter.LineTo(origin + new Vector2(half.x, -half.y));
                painter.LineTo(origin + half);
                painter.LineTo(origin + new Vector2(-half.x, half.y));
                painter.ClosePath(); painter.Stroke();
            }
            Box(center, _baseline.Size, Color.gray);
            Box(center + new Vector2(delta.x, -delta.y) * scale, _pose.Size, new Color(0.3f, 0.75f, 1f));
        }
    }
}
