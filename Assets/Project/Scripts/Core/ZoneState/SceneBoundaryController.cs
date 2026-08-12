using Unity.Cinemachine;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Scene-scoped authority that keeps physical room walls, the transition gate,
    /// and the Cinemachine confiner on the same horizontal boundaries.
    /// </summary>
    public sealed class SceneBoundaryController : MonoBehaviour, IRoomExitGate
    {
        private const float BoundaryEpsilon = 0.001f;

        [Header("Scene References")]
        [Tooltip("Physical wall on the entrance side. Its room-facing edge is its collider maximum X.")]
        [SerializeField] private Collider2D _entranceWall;

        [Tooltip("Physical wall on the exit side. Its room-facing edge is its collider minimum X.")]
        [SerializeField] private Collider2D _exitWall;

        [Tooltip("Exit transition trigger whose near/far edges define locked/open right boundaries.")]
        [SerializeField] private TransitionTrigger _exitTrigger;

        [Header("Boundary Tuning")]
        [Tooltip("Extra space beyond an outside entrance spawn during PlayerIntroState.")]
        [Min(0f)] [SerializeField] private float _entrancePadding = 0.75f;

        [Tooltip("Extra walk space beyond the exit trigger after the room is cleared.")]
        [Min(0f)] [SerializeField] private float _exitPadding = 0.75f;

        [Header("Exit Camera Feedback")]
        [Tooltip("Temporary additive rightward look distance when the exit becomes available.")]
        [Min(0f)] [SerializeField] private float _cameraCueDistance = 3f;

        [Tooltip("Total ease-out-and-return duration for the exit camera cue.")]
        [Min(0.05f)] [SerializeField] private float _cameraCueDuration = 0.65f;

        private CinemachineConfiner2D[] _confinerExtensions;
        private BoxCollider2D _cameraBounds;
        private CameraLookAhead _cameraLookAhead;
        private float _lockedLeftWorld;
        private float _lockedRightWorld;
        private float _currentLeftWorld;
        private float _currentRightWorld;
        private bool _initialized;

        public bool IsExitAvailable { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        public void PrepareForIntro(EntrancePoint entrance)
        {
            if (!Initialize() || entrance == null)
            {
                return;
            }

            float introLeft = Mathf.Min(_lockedLeftWorld, entrance.SpawnPosition.x - _entrancePadding);
            ApplyHorizontalBounds(introLeft, _currentRightWorld);
            AlignEntranceWall(introLeft);
        }

        public void CompleteIntro()
        {
            if (!Initialize())
            {
                return;
            }

            ApplyHorizontalBounds(_lockedLeftWorld, _currentRightWorld);
            AlignEntranceWall(_lockedLeftWorld);
        }

        /// <summary>
        /// Briefly expands the exit-side playable edge so <see cref="PlayerExitState"/>
        /// can walk a short distance past the trigger before fade-out.
        /// </summary>
        public void PrepareForExit(Vector3 exitTarget)
        {
            if (!Initialize())
            {
                return;
            }

            float exitRight = Mathf.Max(_currentRightWorld, exitTarget.x + _exitPadding);
            ApplyHorizontalBounds(_currentLeftWorld, exitRight);
            AlignExitWall(exitRight);
        }

        public void SetExitAvailable(bool available, bool playFeedback = true)
        {
            if (!Initialize())
            {
                return;
            }

            bool changed = IsExitAvailable != available;
            IsExitAvailable = available;

            Collider2D triggerCollider = _exitTrigger.ExitCollider;
            float targetRight = available
                ? triggerCollider.bounds.max.x + _exitPadding
                : _lockedRightWorld;

            ApplyHorizontalBounds(_currentLeftWorld, targetRight);
            AlignExitWall(targetRight);

            if (changed && available && playFeedback && _cameraLookAhead != null)
            {
                _cameraLookAhead.PlayDirectionalCue(_cameraCueDistance, _cameraCueDuration);
            }
        }

        private bool Initialize()
        {
            if (_initialized)
            {
                return true;
            }

            if (_exitTrigger == null || _exitTrigger.ExitCollider == null)
            {
                Debug.LogError($"[SceneBoundaryController] '{name}' requires an exit TransitionTrigger with a Collider2D.", this);
                return false;
            }

            ResolveCameraDependencies();
            _exitTrigger.SetExitGate(this);

            _lockedLeftWorld = _cameraBounds != null
                ? _cameraBounds.bounds.min.x
                : (_entranceWall != null ? _entranceWall.bounds.max.x : transform.position.x);
            _lockedRightWorld = _exitTrigger.ExitCollider.bounds.min.x;
            _currentLeftWorld = _cameraBounds != null ? _cameraBounds.bounds.min.x : _lockedLeftWorld;
            _currentRightWorld = _cameraBounds != null ? _cameraBounds.bounds.max.x : _lockedRightWorld;
            _initialized = true;

            if (_cameraBounds == null)
            {
                Debug.LogWarning($"[SceneBoundaryController] '{name}' found no BoxCollider2D used by CinemachineConfiner2D. Physical and logical exit gating will still work.", this);
            }

            CompleteIntro();
            SetExitAvailable(false, playFeedback: false);
            return true;
        }

        private void ResolveCameraDependencies()
        {
            _confinerExtensions = Object.FindObjectsByType<CinemachineConfiner2D>(FindObjectsSortMode.None);
            for (int i = 0; i < _confinerExtensions.Length; i++)
            {
                if (_confinerExtensions[i] != null
                    && _confinerExtensions[i].BoundingShape2D is BoxCollider2D box)
                {
                    _cameraBounds = box;
                    break;
                }
            }

            _cameraLookAhead = Object.FindFirstObjectByType<CameraLookAhead>();
        }

        private void ApplyHorizontalBounds(float leftWorld, float rightWorld)
        {
            if (rightWorld <= leftWorld + BoundaryEpsilon)
            {
                Debug.LogError($"[SceneBoundaryController] Invalid horizontal bounds: left={leftWorld:F2}, right={rightWorld:F2}.", this);
                return;
            }

            bool changed = !Mathf.Approximately(_currentLeftWorld, leftWorld)
                || !Mathf.Approximately(_currentRightWorld, rightWorld);

            _currentLeftWorld = leftWorld;
            _currentRightWorld = rightWorld;
            if (_cameraBounds == null || !changed)
            {
                return;
            }

            Transform boundsTransform = _cameraBounds.transform;
            float sampleY = boundsTransform.position.y;
            float leftLocal = boundsTransform.InverseTransformPoint(new Vector3(leftWorld, sampleY, 0f)).x;
            float rightLocal = boundsTransform.InverseTransformPoint(new Vector3(rightWorld, sampleY, 0f)).x;
            float localMin = Mathf.Min(leftLocal, rightLocal);
            float localMax = Mathf.Max(leftLocal, rightLocal);

            Vector2 size = _cameraBounds.size;
            Vector2 offset = _cameraBounds.offset;
            size.x = localMax - localMin;
            offset.x = (localMin + localMax) * 0.5f;
            _cameraBounds.size = size;
            _cameraBounds.offset = offset;

            InvalidateConfinerCaches();
        }

        private void AlignEntranceWall(float boundaryWorldX)
        {
            AlignWallEdge(_entranceWall, boundaryWorldX, useMaximumEdge: true);
        }

        private void AlignExitWall(float boundaryWorldX)
        {
            AlignWallEdge(_exitWall, boundaryWorldX, useMaximumEdge: false);
        }

        private static void AlignWallEdge(Collider2D wall, float boundaryWorldX, bool useMaximumEdge)
        {
            if (wall == null)
            {
                return;
            }

            float currentEdge = useMaximumEdge ? wall.bounds.max.x : wall.bounds.min.x;
            float delta = boundaryWorldX - currentEdge;
            if (Mathf.Abs(delta) <= BoundaryEpsilon)
            {
                return;
            }

            Transform wallTransform = wall.transform;
            Vector3 position = wallTransform.position;
            position.x += delta;
            wallTransform.position = position;
            Physics2D.SyncTransforms();
        }

        private void InvalidateConfinerCaches()
        {
            if (_confinerExtensions == null)
            {
                return;
            }

            for (int i = 0; i < _confinerExtensions.Length; i++)
            {
                CinemachineConfiner2D confiner = _confinerExtensions[i];
                if (confiner != null && confiner.BoundingShape2D == _cameraBounds)
                {
                    confiner.InvalidateBoundingShapeCache();
                }
            }
        }
    }
}
