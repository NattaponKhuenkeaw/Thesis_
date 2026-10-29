using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Art
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SortingGroup))]
    [AddComponentMenu("")]
    public sealed class SpriteCropFrame : MonoBehaviour
    {
        [SerializeField] private Sprite sourceSprite;
        [SerializeField] private Vector2 cropSize = new Vector2(4f, 4f);
        [SerializeField] private Vector2 imageOffset;
        [SerializeField, Min(.001f)] private float imagePixelScale = 1f;
        [SerializeField, HideInInspector] private SpriteRenderer artwork;
        [SerializeField, HideInInspector] private SpriteMask clippingMask;

        public Sprite SourceSprite { get => sourceSprite; set { sourceSprite = value; Refresh(); } }
        public Vector2 CropSize { get => cropSize; set { cropSize = value; Refresh(); } }
        public Vector2 ImageOffset { get => imageOffset; set { imageOffset = value; Refresh(); } }
        public float ImagePixelScale { get => imagePixelScale; set { imagePixelScale = value; Refresh(); } }
        public SpriteRenderer Artwork => artwork;
        public SpriteMask ClippingMask => clippingMask;

        private void OnEnable() => Refresh();
        private void OnValidate() => Refresh();
        private void LateUpdate() => Refresh();

        public void Configure(Sprite sprite, SpriteRenderer renderer, SpriteMask mask)
        {
            sourceSprite = sprite; artwork = renderer; clippingMask = mask;
            cropSize = sprite != null ? (Vector2)sprite.bounds.size : new Vector2(4, 4);
            imageOffset = sprite != null ? -(Vector2)sprite.bounds.center : Vector2.zero;
            Refresh();
        }

        public void Refresh()
        {
            cropSize = new Vector2(Mathf.Max(.01f, cropSize.x), Mathf.Max(.01f, cropSize.y));
            imagePixelScale = Mathf.Max(.001f, imagePixelScale);
            if (artwork == null || clippingMask == null || clippingMask.sprite == null) return;
            Vector2 scale = EffectiveScale();
            // Scaling the frame changes only its window, never the source image's pixel density.
            Vector3 artScale = new Vector3(imagePixelScale / scale.x, imagePixelScale / scale.y, 1f);
            Vector3 position = new Vector3(imageOffset.x / scale.x, imageOffset.y / scale.y, 0);
            if (artwork.transform.localScale != artScale) artwork.transform.localScale = artScale;
            if (artwork.transform.localPosition != position) artwork.transform.localPosition = position;
            if (artwork.transform.localRotation != Quaternion.identity) artwork.transform.localRotation = Quaternion.identity;
            if (artwork.sprite != sourceSprite) artwork.sprite = sourceSprite;
            if (artwork.drawMode != SpriteDrawMode.Simple) artwork.drawMode = SpriteDrawMode.Simple;
            if (artwork.maskInteraction != SpriteMaskInteraction.VisibleInsideMask) artwork.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            Vector3 maskScale = new Vector3(cropSize.x / clippingMask.sprite.bounds.size.x, cropSize.y / clippingMask.sprite.bounds.size.y, 1f);
            if (clippingMask.transform.localScale != maskScale) clippingMask.transform.localScale = maskScale;
            if (clippingMask.transform.localPosition != Vector3.zero) clippingMask.transform.localPosition = Vector3.zero;
            if (clippingMask.transform.localRotation != Quaternion.identity) clippingMask.transform.localRotation = Quaternion.identity;
        }

        public void ResizeLocalRect(Rect rectangle)
        {
            Vector2 center = rectangle.center;
            Vector3 worldCenter = transform.TransformPoint(center);
            // Moving one crop edge must not drag the source picture along with the window.
            imageOffset -= Vector2.Scale(center, EffectiveScale());
            cropSize = rectangle.size;
            transform.position = worldCenter;
            Refresh();
        }

        public void FitImage()
        {
            if (sourceSprite == null) return;
            Vector2 scale = EffectiveScale();
            cropSize = new Vector2(sourceSprite.bounds.size.x * imagePixelScale / scale.x, sourceSprite.bounds.size.y * imagePixelScale / scale.y);
            imageOffset = -(Vector2)sourceSprite.bounds.center * imagePixelScale;
            Refresh();
        }

        private Vector2 EffectiveScale()
        {
            Vector3 scale = transform.lossyScale;
            return new Vector2(Mathf.Max(.0001f, Mathf.Abs(scale.x)), Mathf.Max(.0001f, Mathf.Abs(scale.y)));
        }
    }
}
