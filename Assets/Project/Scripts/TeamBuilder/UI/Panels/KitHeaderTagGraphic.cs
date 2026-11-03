using UnityEngine;
using UnityEngine.UI;

namespace Project.TeamBuilder.UI.Panels
{
    /// <summary>Small code-drawn context/resource symbols, independent of font glyph coverage.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KitHeaderTagGraphic : MaskableGraphic
    {
        private CharacterKitViewModel.TagKind _kind;

        public void Bind(CharacterKitViewModel.TagKind kind)
        {
            _kind = kind;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_kind == CharacterKitViewModel.TagKind.Cooldown)
            {
                for (int i = 0; i < 20; i++)
                {
                    float a = i * Mathf.PI / 10f, b = (i + 1) * Mathf.PI / 10f;
                    Line(mesh, new Vector2(8 + Mathf.Cos(a) * 6, 8 + Mathf.Sin(a) * 6),
                        new Vector2(8 + Mathf.Cos(b) * 6, 8 + Mathf.Sin(b) * 6));
                }
                Line(mesh, new Vector2(8, 8), new Vector2(8, 12));
                Line(mesh, new Vector2(8, 8), new Vector2(11, 8));
            }
            else if (_kind == CharacterKitViewModel.TagKind.Energy)
            {
                Line(mesh, new Vector2(8, 15), new Vector2(13, 8));
                Line(mesh, new Vector2(13, 8), new Vector2(8, 1));
                Line(mesh, new Vector2(8, 1), new Vector2(3, 8));
                Line(mesh, new Vector2(3, 8), new Vector2(8, 15));
            }
            else
            {
                Line(mesh, new Vector2(8, 4), new Vector2(8, 12));
                if (_kind != CharacterKitViewModel.TagKind.Ground)
                {
                    Line(mesh, new Vector2(4, 9), new Vector2(8, 13));
                    Line(mesh, new Vector2(8, 13), new Vector2(12, 9));
                }
                if (_kind != CharacterKitViewModel.TagKind.Airborne)
                {
                    Line(mesh, new Vector2(4, 7), new Vector2(8, 3));
                    Line(mesh, new Vector2(8, 3), new Vector2(12, 7));
                }
                if (_kind == CharacterKitViewModel.TagKind.Ground)
                    Line(mesh, new Vector2(2, 1), new Vector2(14, 1));
            }
        }

        private void Line(VertexHelper mesh, Vector2 start, Vector2 end)
        {
            Rect rect = rectTransform.rect;
            start = rect.min + Vector2.Scale(start / 16f, rect.size);
            end = rect.min + Vector2.Scale(end / 16f, rect.size);
            Vector2 offset = new Vector2(-(end - start).y, (end - start).x).normalized * Mathf.Min(rect.width, rect.height) * .045f;
            int index = mesh.currentVertCount;
            mesh.AddVert(start - offset, color, Vector2.zero);
            mesh.AddVert(start + offset, color, Vector2.zero);
            mesh.AddVert(end + offset, color, Vector2.zero);
            mesh.AddVert(end - offset, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
