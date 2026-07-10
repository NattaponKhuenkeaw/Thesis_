using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Helper component attached to legacy prefab VFX to fit them to the screen size
    /// and keep them centered on the camera, matching the behavior of UltimateFlashOverlay.
    /// </summary>
    public class FullScreenVFX : MonoBehaviour
    {
        private Camera targetCamera;
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            targetCamera = Camera.main;
        }

        private void Start()
        {
            UpdateFullScreenScaleAndPosition();
        }

        private void LateUpdate()
        {
            UpdateFullScreenScaleAndPosition();
        }

        private void UpdateFullScreenScaleAndPosition()
        {
            if (targetCamera == null) return;

            // Follow camera
            Vector3 camPos = targetCamera.transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);

            // Fit scale
            float baseSize = Mathf.Max(targetCamera.orthographicSize * 2.5f, 15f);
            float cameraHeight = baseSize * 2f;
            float cameraWidth = cameraHeight * targetCamera.aspect;

            Vector2 spriteSize = Vector2.one;
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                spriteSize = spriteRenderer.sprite.bounds.size;
            }

            if (spriteSize.x > 0.001f && spriteSize.y > 0.001f)
            {
                transform.localScale = new Vector3(
                    cameraWidth / spriteSize.x,
                    cameraHeight / spriteSize.y,
                    1f);
            }
        }
    }
}
