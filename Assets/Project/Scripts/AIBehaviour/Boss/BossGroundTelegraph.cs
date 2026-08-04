using UnityEngine;

namespace Project.AIBehaviour.Boss
{
    /// <summary>
    /// Simple world-space landing marker for Jump Smash telegraphs.
    /// Assign a semi-transparent red sprite (or leave empty to no-op).
    /// </summary>
    public class BossGroundTelegraph : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer markerRenderer;
        [SerializeField] private float defaultRadius = 1.6f;
        [SerializeField] private Color markerColor = new Color(0.85f, 0.15f, 0.1f, 0.45f);

        private Vector3 baseScale = Vector3.one;

        private void Awake()
        {
            if (markerRenderer == null)
                markerRenderer = GetComponent<SpriteRenderer>();

            if (markerRenderer != null)
            {
                baseScale = markerRenderer.transform.localScale;
                markerRenderer.color = markerColor;
                markerRenderer.enabled = false;
            }
        }

        public void Show(Vector3 worldPosition, float radius = -1f)
        {
            if (markerRenderer == null)
                return;

            float resolvedRadius = Mathf.Max(0.05f, radius > 0f ? radius : defaultRadius);
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);

            // Treat input as world radius. Scale so sprite diameter matches 2 * radius.
            float diameter = resolvedRadius * 2f;
            if (markerRenderer.sprite != null)
            {
                Vector3 spriteSize = markerRenderer.sprite.bounds.size;
                float baseDim = Mathf.Max(spriteSize.x, spriteSize.y, 0.01f);
                float uniform = diameter / baseDim;
                markerRenderer.transform.localScale = new Vector3(uniform, uniform, baseScale.z);
            }
            else
            {
                markerRenderer.transform.localScale = new Vector3(
                    baseScale.x * diameter,
                    baseScale.y * diameter,
                    baseScale.z);
            }

            markerRenderer.color = markerColor;
            markerRenderer.enabled = true;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (markerRenderer != null)
                markerRenderer.enabled = false;
        }
    }
}
