using UnityEngine;

namespace Project.Parallax
{
    /// <summary>
    /// Standalone parallax background script. 
    /// Handles movement relative to the camera and infinite tiling.
    /// </summary>
    [DefaultExecutionOrder(10)] // Ensures this runs AFTER Camera movement in LateUpdate
    public class ParallaxBackground : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("(0,0) = Static in world, (1,1) = Moves exactly with camera (stays on screen).")]
        public Vector2 parallaxEffectMultiplier = new Vector2(0.5f, 0.5f);
        
        public bool infiniteHorizontal = true;
        public bool infiniteVertical = false;

        [Header("References")]
        public Transform targetCamera;

        private Vector3 _startPosition;
        private Vector3 _startCameraPosition;
        private float _textureUnitSizeX;
        private float _textureUnitSizeY;

        private void Start()
        {
            // Auto-find main camera if not assigned
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            if (targetCamera != null)
            {
                _startCameraPosition = targetCamera.position;
            }
            else
            {
                Debug.LogWarning($"[ParallaxBackground] {name} could not find a target camera. Parallax disabled.");
            }

            // Capture initial world position
            _startPosition = transform.position;

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
            if (targetCamera == null) return;

            // Calculate how far the camera has moved desde start
            Vector3 cameraPosition = targetCamera.position;
            Vector3 travel = cameraPosition - _startCameraPosition;

            // 1. Calculate Parallax Movement (How much the object actually moves in world space)
            // If Multiplier = 0: Moves 0 units (Static in world)
            // If Multiplier = 1: Moves same as camera (Locked to camera)
            float moveX = travel.x * parallaxEffectMultiplier.x;
            float moveY = travel.y * parallaxEffectMultiplier.y;

            // Apply the base parallax position
            transform.position = new Vector3(
                _startPosition.x + moveX,
                _startPosition.y + moveY,
                _startPosition.z
            );

            // 2. Handle Infinite Looping (Repositioning the 'start' point)
            // We check the relative travel (how much the camera has passed the background)
            // RelativeTravel = Travel * (1 - Multiplier)
            if (infiniteHorizontal && _textureUnitSizeX > 0)
            {
                float relativeTravelX = travel.x * (1 - parallaxEffectMultiplier.x);
                
                if (Mathf.Abs(relativeTravelX) >= _textureUnitSizeX)
                {
                    // Shift the start position by whole texture units
                    float offset = relativeTravelX % _textureUnitSizeX;
                    _startPosition.x += (relativeTravelX - offset);
                }
            }

            if (infiniteVertical && _textureUnitSizeY > 0)
            {
                float relativeTravelY = travel.y * (1 - parallaxEffectMultiplier.y);

                if (Mathf.Abs(relativeTravelY) >= _textureUnitSizeY)
                {
                    float offset = relativeTravelY % _textureUnitSizeY;
                    _startPosition.y += (relativeTravelY - offset);
                }
            }
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
            }
        }
    }
}
