using UnityEngine;

namespace Project.Parallax
{
    /// <summary>
    /// Standalone parallax background script. 
    /// Handles movement relative to the camera and infinite tiling.
    /// </summary>
    [DefaultExecutionOrder(200)] // CinemachineBrain updates the output camera at order 100.
    public class ParallaxBackground : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("(0,0) = Static in world, (1,1) = Moves exactly with camera (stays on screen).")]
        public Vector2 parallaxEffectMultiplier = new Vector2(0.5f, 0.5f);
        
        public bool infiniteHorizontal = true;
        public bool infiniteVertical = false;

        [Tooltip("Keep a sky row at camera height without enlarging its artwork to cover tall terrain.")]
        public bool centerVerticallyOnCamera = false;

        [Header("References")]
        public Transform targetCamera;

        private Vector3 _startPosition;
        private Vector3 _startCameraPosition;
        private float _textureUnitSizeX;
        private float _textureUnitSizeY;
        private bool _warnedMissingCamera;
        private Camera _skyCamera;
        private SpriteRenderer[] _skyImages;

        private void Start()
        {
            // Capture initial world position
            _startPosition = transform.position;
            TryBindTargetCamera();

            // Calculate texture size for infinite looping from SpriteRenderer
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                // We use bounds.size which is the world-space size of the sprite
                // This accounts for pixelsPerUnit correctly
                _textureUnitSizeX = spriteRenderer.bounds.size.x;
                _textureUnitSizeY = spriteRenderer.bounds.size.y;
            }
            else
            {
                Debug.LogWarning($"[ParallaxBackground] {name} is missing a SpriteRenderer or Sprite. Infinite looping disabled.");
                infiniteHorizontal = false;
                infiniteVertical = false;
            }
        }

        private void LateUpdate()
        {
            // Room layouts can be instantiated before the scene camera is available.
            // Retry only while unbound so prefab layers can recover without per-frame camera searches.
            if (targetCamera == null && !TryBindTargetCamera()) return;

            // Calculate how far the camera has moved desde start
            Vector3 cameraPosition = targetCamera.position;
            Vector3 travel = cameraPosition - _startCameraPosition;

            // 1. Calculate Parallax Movement (How much the object actually moves in world space)
            // If Multiplier = 0: Moves 0 units (Static in world)
            // If Multiplier = 1: Moves same as camera (Locked to camera)
            float moveX = travel.x * parallaxEffectMultiplier.x;
            float moveY = travel.y * parallaxEffectMultiplier.y;

            // Infinite looping is a periodic offset derived from total travel. Keep the
            // authored start position fixed; accumulating total travel into it every frame
            // causes runaway movement as soon as the camera has crossed one tile.
            float loopOffsetX = 0f;
            float loopOffsetY = 0f;

            // 2. Handle infinite looping by reducing relative travel to a whole-tile offset.
            // The remainder stays within one tile, keeping the layer aligned with the camera.
            if (infiniteHorizontal && _textureUnitSizeX > 0)
            {
                float relativeTravelX = travel.x * (1 - parallaxEffectMultiplier.x);
                loopOffsetX = relativeTravelX - (relativeTravelX % _textureUnitSizeX);
            }

            if (infiniteVertical && _textureUnitSizeY > 0)
            {
                float relativeTravelY = travel.y * (1 - parallaxEffectMultiplier.y);
                loopOffsetY = relativeTravelY - (relativeTravelY % _textureUnitSizeY);
            }

            transform.position = new Vector3(
                _startPosition.x + moveX + loopOffsetX,
                _startPosition.y + moveY + loopOffsetY,
                _startPosition.z
            );
            KeepSkyInsideViewport();
        }

        /// <summary>
        /// Resets the parallax to current position. Call after camera/player teleport.
        /// </summary>
        public void ResetParallax()
        {
            _startPosition = transform.position;
            if (targetCamera != null)
            {
                _startCameraPosition = targetCamera.position;
                CenterSkyOnCamera();
            }
        }

        private bool TryBindTargetCamera()
        {
            if (targetCamera == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    if (!_warnedMissingCamera)
                    {
                        Debug.LogWarning($"[ParallaxBackground] {name} is waiting for a MainCamera-tagged camera.", this);
                        _warnedMissingCamera = true;
                    }

                    return false;
                }

                targetCamera = mainCamera.transform;
            }

            _startCameraPosition = targetCamera.position;
            _startPosition = transform.position;
            if (centerVerticallyOnCamera)
            {
                _skyCamera = targetCamera.GetComponent<Camera>();
                _skyImages = GetComponentsInChildren<SpriteRenderer>(true);
                System.Array.Sort(_skyImages, (a, b) => a.bounds.min.x.CompareTo(b.bounds.min.x));
            }
            CenterSkyOnCamera();
            _warnedMissingCamera = false;
            return true;
        }

        private void CenterSkyOnCamera()
        {
            if (!centerVerticallyOnCamera) return;
            _startPosition.y = targetCamera.position.y;
            transform.position = _startPosition;
            KeepSkyInsideViewport();
        }

        private void KeepSkyInsideViewport()
        {
            if (!centerVerticallyOnCamera || _skyCamera == null || !_skyCamera.orthographic || _skyImages == null)
                return;

            float halfWidth = _skyCamera.orthographicSize * _skyCamera.aspect;
            float coveredTo = targetCamera.position.x - halfWidth - .05f;
            float right = targetCamera.position.x + halfWidth + .05f;
            foreach (var image in _skyImages)
            {
                if (!image.enabled || !image.gameObject.activeInHierarchy) continue;
                Bounds bounds = image.bounds;
                if (bounds.max.x <= coveredTo) continue;
                if (bounds.min.x > coveredTo + .001f) break;
                coveredTo = bounds.max.x;
                if (coveredTo >= right) return;
            }

            // A layout can bind while the output camera is still in the previous room.
            // Re-anchor only an exposed sky row; never move scenery or resize its artwork.
            var center = GetComponent<SpriteRenderer>();
            if (center == null || center.sprite == null) return;
            float correction = targetCamera.position.x - center.bounds.center.x;
            _startPosition.x += correction;
            transform.position += Vector3.right * correction;
        }
    }
}
