using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Project.UI
{
    public static class UIDragGhostHelper
    {
        public static Transform CreateGhost(Canvas rootCanvas, Sprite sprite, Vector2 size, float alpha = 0.75f, string name = "DragGhost")
        {
            if (rootCanvas == null || sprite == null) return null;

            GameObject ghostObj = new GameObject(name, typeof(RectTransform), typeof(Image));
            Transform dragGhost = ghostObj.transform;
            dragGhost.SetParent(rootCanvas.transform, false);

            Image ghostImage = ghostObj.GetComponent<Image>();
            ghostImage.sprite = sprite;
            ghostImage.raycastTarget = false;
            ghostImage.color = new Color(1f, 1f, 1f, alpha);

            RectTransform ghostRect = ghostObj.GetComponent<RectTransform>();
            ghostRect.anchorMin = Vector2.zero;
            ghostRect.anchorMax = Vector2.zero;
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.sizeDelta = size;

            return dragGhost;
        }

        public static void UpdatePosition(Transform dragGhost, Canvas rootCanvas, Vector2 screenPosition)
        {
            if (dragGhost == null || rootCanvas == null) return;

            RectTransform ghostRect = dragGhost as RectTransform;
            if (ghostRect == null) ghostRect = dragGhost.GetComponent<RectTransform>();

            RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
            Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 localPoint))
            {
                Vector2 canvasSize = canvasRect.rect.size;
                Vector2 pivotOffset = canvasRect.pivot * canvasSize;
                ghostRect.anchoredPosition = localPoint + pivotOffset;
            }
        }

        public static void DestroyGhost(ref Transform dragGhost)
        {
            if (dragGhost != null)
            {
                Object.Destroy(dragGhost.gameObject);
                dragGhost = null;
            }
        }
    }
}
