using UnityEngine;

namespace Project.Parallax
{
    /// <summary>
    /// Master controller for the Parallax system. 
    /// Coordinates camera movement and provides a central point for configuration.
    /// Attach this to a persistent object or a scene-level background manager.
    /// </summary>
    public class ParallaxController : MonoBehaviour
    {
        public static ParallaxController Instance { get; private set; }

        [Header("References")]
        [Tooltip("The camera to track. If null, defaults to Camera.main.")]
        [SerializeField] private Camera _targetCamera;

        [Header("Global Settings")]
        [Tooltip("Multiplies the parallax effect for all layers.")]
        [SerializeField] private float _globalScrollScale = 1.0f;

        [Tooltip("Whether the parallax system should run.")]
        [SerializeField] private bool _trackMovement = true;

        // Cached state for layers to consume
        public Camera TargetCamera => _targetCamera;
        public Vector3 CameraPosition => _targetCamera != null ? _targetCamera.transform.position : Vector3.zero;
        public float GlobalScale => _globalScrollScale;
        public bool IsActive => _trackMovement && _targetCamera != null;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }
        }

        private void Start()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
                if (_targetCamera == null)
                {
                    Debug.LogWarning("[ParallaxController] No Main Camera found! Parallax will not work until assigned.");
                }
            }
        }
    }
}
