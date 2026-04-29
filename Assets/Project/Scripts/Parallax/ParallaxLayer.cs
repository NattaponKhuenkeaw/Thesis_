using UnityEngine;

namespace Project.Parallax
{
    /// <summary>
    /// Individual parallax layer. Moves relative to the camera based on weights.
    /// Supports infinite looping for tiled backgrounds.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Movement Weights")]
        [Tooltip("0 = Moves with camera (static background), 1 = Doesn't move (stays at world position). " +
                 "Usually 0.1 to 0.9 for background layers.")]
        [Range(0f, 1f)]
        [SerializeField] private float _parallaxFactorX = 0.5f;

        [Tooltip("Vertical parallax factor. Often 0 or very low for 2D platformers.")]
        [Range(0f, 1f)]
        [SerializeField] private float _parallaxFactorY = 0.05f;

        [Header("Infinite Looping")]
        [Tooltip("If true, the layer will snap its position to simulate an infinite tiling background.")]
        [SerializeField] private bool _infiniteLoopX = true;
        [SerializeField] private bool _infiniteLoopY = false;

        // Initial state
        private Vector3 _startPosition;
        private Vector3 _startCameraPosition;
        private float _spriteSizeX;
        private float _spriteSizeY;

        // References
        private SpriteRenderer _renderer;
        private Transform _camTransform;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            // Capture initial world position
            _startPosition = transform.position;

            // Get camera reference
            if (ParallaxController.Instance != null && ParallaxController.Instance.TargetCamera != null)
            {
                _camTransform = ParallaxController.Instance.TargetCamera.transform;
            }
            else
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    _camTransform = mainCam.transform;
                }
            }

            if (_camTransform != null)
            {
                _startCameraPosition = _camTransform.position;
            }

            // Calculate sprite dimensions for looping
            if (_renderer != null && _renderer.sprite != null)
            {
                _spriteSizeX = _renderer.bounds.size.x;
                _spriteSizeY = _renderer.bounds.size.y;
            }
            else
            {
                Debug.LogWarning($"[ParallaxLayer] {name} is missing a SpriteRenderer or Sprite. Infinite looping disabled.");
                _infiniteLoopX = false;
                _infiniteLoopY = false;
            }
        }

        private void LateUpdate()
        {
            if (_camTransform == null) return;

            // Check if controller allows tracking
            if (ParallaxController.Instance != null && !ParallaxController.Instance.IsActive)
                return;

            float globalScale = ParallaxController.Instance != null ? ParallaxController.Instance.GlobalScale : 1f;

            // Calculate distance moved by camera
            Vector3 cameraDelta = _camTransform.position - _startCameraPosition;

            // Calculate parallax movement
            // Movement = CameraDelta * (1 - ParallaxFactor)
            // If Factor = 1: Moves exactly with camera (stays in place relative to screen)
            // If Factor = 0: Doesn't move at all (stays at world start position)
            float moveX = cameraDelta.x * (1 - _parallaxFactorX) * globalScale;
            float moveY = cameraDelta.y * (1 - _parallaxFactorY) * globalScale;

            // Apply movement to start position
            transform.position = new Vector3(
                _startPosition.x + moveX,
                _startPosition.y + moveY,
                _startPosition.z
            );

            // Handle infinite looping (Repositioning)
            if (_infiniteLoopX)
            {
                // How much 'view' has scrolled relative to the background's start
                float tempX = _camTransform.position.x * _parallaxFactorX;
                
                if (tempX > _startPosition.x + _spriteSizeX)
                {
                    _startPosition.x += _spriteSizeX;
                }
                else if (tempX < _startPosition.x - _spriteSizeX)
                {
                    _startPosition.x -= _spriteSizeX;
                }
            }

            if (_infiniteLoopY)
            {
                float tempY = _camTransform.position.y * _parallaxFactorY;

                if (tempY > _startPosition.y + _spriteSizeY)
                {
                    _startPosition.y += _spriteSizeY;
                }
                else if (tempY < _startPosition.y - _spriteSizeY)
                {
                    _startPosition.y -= _spriteSizeY;
                }
            }
        }
        
        /// <summary>
        /// Resets the parallax to current positions. Useful when teleporting the player/camera.
        /// </summary>
        public void ResetParallax()
        {
            _startPosition = transform.position;
            if (_camTransform != null)
            {
                _startCameraPosition = _camTransform.position;
            }
        }
    }
}
