using Unity.Cinemachine;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Keeps invisible room walls and the Cinemachine confiner on the same X edges.
    /// Author walls (and optional exit trigger) in the layout; this script syncs the persistent camera.
    /// </summary>
    public sealed class SceneBoundaryController : MonoBehaviour, IRoomExitGate
    {
        private const float BoundaryEpsilon = 0.001f;

        [Header("Scene References")]
        [Tooltip("Solid wall on the entrance side. Room-facing edge = collider max X.")]
        [SerializeField] private Collider2D _entranceWall;

        [Tooltip("After the fight trigger, camera left edge moves to this wall so the hallway drops out of frame. Leave empty for rooms with no pre-fight corridor.")]
        [SerializeField] private Collider2D _fightEntranceWall;

        [Tooltip("Solid wall on the exit side. Room-facing edge = collider min X. Blocks leaving until the room is cleared.")]
        [SerializeField] private Collider2D _exitWall;

        [Tooltip("Optional. Exit trigger for choice UI. If empty, the exit wall defines the locked right edge.")]
        [SerializeField] private TransitionTrigger _exitTrigger;

        [Header("Boundary Tuning")]
        [Tooltip("Physical entrance-wall clearance beyond an outside spawn. Does not expand the camera frame.")]
        [Min(0f)] [SerializeField] private float _entrancePadding = 0.75f;

        [Header("Camera")]
        [Tooltip("When true, confiner slowing is removed so the player sits on the screen edge at a wall.")]
        [SerializeField] private bool _pinPlayerToScreenEdge = true;

        [Tooltip("How quickly look-ahead fades out as the player approaches a wall.")]
        [Min(0.01f)] [SerializeField] private float _lookAheadFadeDistance = 3f;

        [Tooltip("How long the camera frame eases open after a cleared wall unlocks. Room entry still cuts.")]
        [Min(0f)] [SerializeField] private float _boundExpandDuration = 1.05f;

        [Tooltip("Extra camera travel above/below layout colliders so drops and high ledges stay on-screen.")]
        [Min(0f)] [SerializeField] private float _verticalPadding = 10f;

        [Header("Exit Camera Feedback")]
        [Tooltip("Temporary additive rightward look distance when the exit becomes available.")]
        [Min(0f)] [SerializeField] private float _cameraCueDistance = 3f;

        [Tooltip("Total ease-out-and-return duration for the exit camera cue.")]
        [Min(0.05f)] [SerializeField] private float _cameraCueDuration = 0.65f;

        private static SceneBoundaryController s_activeBinder;
        private static BoxCollider2D s_sharedBounds;
        private static Vector2 s_originalBoundsSize;
        private static Vector2 s_originalBoundsOffset;
        private static bool s_capturedOriginalBounds;
        private static CinemachineConfiner2D[] s_confiners;
        private static float[] s_originalSlowingDistances;

        private CinemachineConfiner2D[] _confinerExtensions;
        private BoxCollider2D _cameraBounds;
        private CameraLookAhead _cameraLookAhead;
        private float _lockedLeftWorld;
        private float _lockedRightWorld;
        private float _stageLeftWorld;
        private bool _capturedStageLeft;
        private float _currentLeftWorld;
        private float _currentRightWorld;
        private bool _initialized;
        private Collider2D _stageExitWall;
        private bool _searchedStageExitChild;
        private bool _usesAuthoredStageExitChild;
        private Coroutine _boundSlideRoutine;
        private float _slideLeftFrom;
        private float _slideLeftTo;
        private float _slideRightFrom;
        private float _slideRightTo;

        public bool IsExitAvailable { get; private set; }

        public TransitionTrigger ExitTrigger => _exitTrigger;

        public Collider2D ExitWall => IsExitAvailable && _stageExitWall != null
            ? _stageExitWall
            : _exitWall;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// Retarget locked walls and camera to one chamber inside a multi-room combat layout.
        /// Does not unlock the stage exit trigger.
        /// </summary>
        public bool BindChamberWalls(Collider2D entranceWall, Collider2D exitWall)
        {
            _entranceWall = entranceWall;
            _exitWall = exitWall;
            return ApplyLockedWalls(useExitTriggerFallback: false);
        }

        /// <summary>
        /// Shrinks the camera to the fight arena. Call after the player has walked in
        /// and can no longer return to a pre-fight hallway.
        /// </summary>
        public bool LockCameraToFightEntrance()
        {
            if (_fightEntranceWall == null || !Initialize())
            {
                return false;
            }

            _entranceWall = _fightEntranceWall;
            if (!ApplyLockedWalls(useExitTriggerFallback: false))
            {
                return false;
            }

            _stageLeftWorld = _lockedLeftWorld;
            ApplyHorizontalBounds(_lockedLeftWorld, _currentRightWorld, force: true);
            return true;
        }

        /// <summary>
        /// Later chambers keep cleared rooms walkable. Only the forward wall seals.
        /// Does not retarget the camera; walking in is the same follow as the rest of the stage.
        /// </summary>
        public void EnterLaterChamber(Collider2D backWall, Collider2D forwardWall)
        {
            if (!Initialize())
            {
                return;
            }

            OpenDivider(backWall);
            if (backWall != null)
            {
                _entranceWall = backWall;
            }

            _exitWall = forwardWall;
            EnsureSolidWall(forwardWall);
            if (!RoomBoundaryMath.TryResolveLockedX(
                    _entranceWall,
                    _exitWall,
                    null,
                    out _,
                    out float right)
                && forwardWall != null)
            {
                right = forwardWall.bounds.min.x;
            }

            _lockedRightWorld = Mathf.Max(_lockedRightWorld, right);
        }

        /// <summary>
        /// Opens the current divider so the player can walk toward the next chamber.
        /// Does not roll RouteOffer or unlock the layout exit trigger.
        /// Camera left stays at the stage start so previously cleared rooms stay on-screen.
        /// </summary>
        public void OpenWalkTo(float rightWorldX, Collider2D forwardDivider, Collider2D backDivider = null)
        {
            if (!Initialize())
            {
                return;
            }

            OpenDivider(forwardDivider);
            OpenDivider(backDivider);

            float right = Mathf.Max(_currentRightWorld, rightWorldX);
            ApplyHorizontalBounds(CameraLeft(), right, force: false);
        }

        /// <summary>
        /// Lets the player walk through a cleared divider. Do not pass the stage start wall.
        /// </summary>
        public void OpenDivider(Collider2D dividerWall)
        {
            if (dividerWall == null)
            {
                return;
            }

            dividerWall.enabled = true;
            dividerWall.isTrigger = true;
        }

        /// <summary>
        /// Hide the stage exit wall so the leave walk can pass the door.
        /// Call only when the player starts leaving the room, not on room clear.
        /// </summary>
        public void OpenExitForLeave()
        {
            if (!Initialize())
            {
                return;
            }

            CaptureStageExitWallIfNeeded();
            HideLeaveWall(_stageExitWall);
            if (_exitWall != _stageExitWall)
            {
                HideLeaveWall(_exitWall);
            }
        }

        private static void HideLeaveWall(Collider2D wall)
        {
            if (wall == null)
            {
                return;
            }

            wall.enabled = false;
            wall.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (s_activeBinder != this)
            {
                return;
            }

            StopBoundSlide();
            RestoreSharedCamera();
            s_activeBinder = null;
            if (_cameraLookAhead != null)
            {
                _cameraLookAhead.ClearHorizontalConfine();
            }
        }

        public void PrepareForIntro(EntrancePoint entrance)
        {
            if (!Initialize() || entrance == null)
            {
                return;
            }

            float introLeft = Mathf.Min(CameraLeft(), entrance.SpawnPosition.x - _entrancePadding);
            // Let the player enter from offscreen without moving the room's camera edge.
            AlignEntranceWall(introLeft);
        }

        public void CompleteIntro()
        {
            if (!Initialize())
            {
                return;
            }

            AlignEntranceWall(_lockedLeftWorld);
        }

        /// <summary>
        /// Expands the exit-side playable edge and wall. The card-leave walk no longer
        /// calls this; it stops before the current ExitWall instead of walking past it.
        /// </summary>
        public void PrepareForExit(Vector3 exitTarget)
        {
            if (!Initialize())
            {
                return;
            }

            float exitRight = Mathf.Max(_currentRightWorld, exitTarget.x);
            ApplyHorizontalBounds(CameraLeft(), exitRight, force: false);
            if (ShouldSlideExitWallOnUnlock())
            {
                AlignExitWall(exitRight);
            }
        }

        public void SetExitAvailable(bool available, bool playFeedback = true)
        {
            if (!Initialize())
            {
                return;
            }

            CaptureStageExitWallIfNeeded();
            bool changed = IsExitAvailable != available;
            IsExitAvailable = available;

            if (available)
            {
                PromoteStageExitWall();
            }

            float targetRight = available ? ResolveUnlockedRight() : _lockedRightWorld;

            ApplyHorizontalBounds(CameraLeft(), targetRight, force: false);
            if (available)
            {
                if (ShouldSlideExitWallOnUnlock())
                {
                    AlignExitWall(targetRight);
                }
            }
            else if (!IsSafeZoneLayout())
            {
                AlignExitWall(_lockedRightWorld);
            }

            if (changed && available && playFeedback && _cameraLookAhead != null)
            {
                _cameraLookAhead.PlayDirectionalCue(_cameraCueDistance, _cameraCueDuration);
            }
        }

        private float ResolveUnlockedRight()
        {
            if (_exitTrigger != null && _exitTrigger.ExitCollider != null)
            {
                return _exitTrigger.ExitCollider.bounds.max.x;
            }

            return _lockedRightWorld;
        }

        private bool Initialize()
        {
            if (_exitTrigger != null)
            {
                _exitTrigger.SetExitGate(this);
            }

            CaptureStageExitWallIfNeeded();
            TryAssignWallsFromStage();
            CaptureStageExitWallIfNeeded();
            if (_initialized)
            {
                if (_cameraBounds == null)
                {
                    ResolveCameraDependencies();
                }

                return true;
            }

            return ApplyLockedWalls(useExitTriggerFallback: true);
        }

        private bool ApplyLockedWalls(bool useExitTriggerFallback)
        {
            if (_exitTrigger != null)
            {
                _exitTrigger.SetExitGate(this);
            }

            Collider2D rightFallback = useExitTriggerFallback && _exitTrigger != null
                ? _exitTrigger.ExitCollider
                : null;

            if (!RoomBoundaryMath.TryResolveLockedX(
                    _entranceWall,
                    _exitWall,
                    rightFallback,
                    out _lockedLeftWorld,
                    out _lockedRightWorld))
            {
                if (!_initialized)
                {
                    Debug.LogError(
                        $"[SceneBoundaryController] '{name}' needs an entrance wall plus an exit wall or TransitionTrigger.",
                        this);
                }

                return false;
            }

            EnsureSolidWall(_entranceWall);
            EnsureSolidWall(_exitWall);

            bool firstLock = !_initialized;
            if (firstLock)
            {
                ResolveCameraDependencies();
                CaptureSharedCameraIfNeeded();
                s_activeBinder = this;
                _initialized = true;

                if (_cameraBounds == null)
                {
                    Debug.LogWarning($"[SceneBoundaryController] '{name}' found no BoxCollider2D used by CinemachineConfiner2D. Physical walls will still block.", this);
                }
            }
            else if (_cameraBounds == null)
            {
                ResolveCameraDependencies();
            }

            IsExitAvailable = false;
            if (!_capturedStageLeft)
            {
                _stageLeftWorld = _lockedLeftWorld;
                _capturedStageLeft = true;
            }

            ApplyHorizontalBounds(CameraLeft(), _lockedRightWorld, force: firstLock);
            AlignEntranceWall(_lockedLeftWorld);
            AlignExitWall(_lockedRightWorld);
            return true;
        }

        private float CameraLeft()
        {
            return _capturedStageLeft ? _stageLeftWorld : _lockedLeftWorld;
        }

        private void TryAssignWallsFromStage()
        {
            if (_entranceWall != null && _exitWall != null)
            {
                return;
            }

            CombatStageController stage = GetComponent<CombatStageController>()
                ?? GetComponentInParent<CombatStageController>();
            if (stage == null && transform.root != null)
            {
                stage = transform.root.GetComponentInChildren<CombatStageController>(true);
            }

            if (stage == null || !stage.TryGetChamberWalls(0, out Collider2D left, out Collider2D right))
            {
                return;
            }

            if (_entranceWall == null)
            {
                _entranceWall = left;
            }

            if (_exitWall == null)
            {
                _exitWall = right;
            }
        }

        private void CaptureStageExitWallIfNeeded()
        {
            if (!_searchedStageExitChild)
            {
                _searchedStageExitChild = true;
                _stageExitWall = FindDirectChildStageExitWall();
                _usesAuthoredStageExitChild = _stageExitWall != null;
            }

            if (_stageExitWall == null)
            {
                _stageExitWall = _exitWall;
            }
        }

        private Collider2D FindDirectChildStageExitWall()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || !RoomBoundaryMath.IsStageExitWallName(child.name))
                {
                    continue;
                }

                Collider2D collider = child.GetComponent<Collider2D>();
                if (collider != null)
                {
                    return collider;
                }
            }

            return null;
        }

        private void PromoteStageExitWall()
        {
            CaptureStageExitWallIfNeeded();
            if (_stageExitWall == null)
            {
                return;
            }

            if (_exitWall != null && _exitWall != _stageExitWall)
            {
                OpenDivider(_exitWall);
            }

            EnsureSolidWall(_stageExitWall);
            _exitWall = _stageExitWall;
        }

        private bool ShouldSlideExitWallOnUnlock()
        {
            return RoomBoundaryMath.ShouldSlideExitWallOnUnlock(ResolveCurrentZoneMode(), _usesAuthoredStageExitChild);
        }

        private bool IsSafeZoneLayout()
        {
            return ResolveCurrentZoneMode() == ZoneMode.SafeZone;
        }

        private static ZoneMode ResolveCurrentZoneMode()
        {
            if (RoomLayoutHost.Instance != null && RoomLayoutHost.Instance.ActiveRoom != null)
            {
                return RoomLayoutHost.Instance.ActiveRoom.ZoneMode;
            }

            ZoneStateProvider provider = Resources.Load<ZoneStateProvider>("CombatSO/ZoneStateProvider");
            return provider != null ? provider.CurrentMode : ZoneMode.CombatZone;
        }

        private static void EnsureSolidWall(Collider2D wall)
        {
            if (wall == null)
            {
                return;
            }

            wall.enabled = true;
            wall.isTrigger = false;
        }

        private void ResolveCameraDependencies()
        {
            _confinerExtensions = Object.FindObjectsByType<CinemachineConfiner2D>(FindObjectsSortMode.None);
            s_confiners = _confinerExtensions;
            for (int i = 0; i < _confinerExtensions.Length; i++)
            {
                if (_confinerExtensions[i] != null
                    && _confinerExtensions[i].BoundingShape2D is BoxCollider2D box)
                {
                    _cameraBounds = box;
                    s_sharedBounds = box;
                    break;
                }
            }

            _cameraLookAhead = Object.FindFirstObjectByType<CameraLookAhead>();
        }

        private void CaptureSharedCameraIfNeeded()
        {
            if (_cameraBounds != null && !s_capturedOriginalBounds)
            {
                s_originalBoundsSize = _cameraBounds.size;
                s_originalBoundsOffset = _cameraBounds.offset;
                s_capturedOriginalBounds = true;
            }

            if (_confinerExtensions == null || s_originalSlowingDistances != null)
            {
                return;
            }

            s_originalSlowingDistances = new float[_confinerExtensions.Length];
            for (int i = 0; i < _confinerExtensions.Length; i++)
            {
                CinemachineConfiner2D confiner = _confinerExtensions[i];
                s_originalSlowingDistances[i] = confiner != null ? confiner.SlowingDistance : 0f;
            }
        }

        private static void RestoreSharedCamera()
        {
            if (s_sharedBounds != null && s_capturedOriginalBounds)
            {
                s_sharedBounds.size = s_originalBoundsSize;
                s_sharedBounds.offset = s_originalBoundsOffset;
            }

            if (s_confiners != null && s_originalSlowingDistances != null)
            {
                int count = Mathf.Min(s_confiners.Length, s_originalSlowingDistances.Length);
                for (int i = 0; i < count; i++)
                {
                    if (s_confiners[i] != null)
                    {
                        s_confiners[i].SlowingDistance = s_originalSlowingDistances[i];
                        if (s_sharedBounds != null && s_confiners[i].BoundingShape2D == s_sharedBounds)
                        {
                            s_confiners[i].InvalidateBoundingShapeCache();
                        }
                    }
                }
            }
        }

        private void ApplyHorizontalBounds(float leftWorld, float rightWorld, bool force)
        {
            if (rightWorld <= leftWorld + BoundaryEpsilon)
            {
                Debug.LogError($"[SceneBoundaryController] Invalid horizontal bounds: left={leftWorld:F2}, right={rightWorld:F2}.", this);
                return;
            }

            bool opensOutward = rightWorld > _currentRightWorld + BoundaryEpsilon
                || leftWorld < _currentLeftWorld - BoundaryEpsilon;
            if (!force && _boundExpandDuration > 0f && opensOutward)
            {
                _slideLeftFrom = _currentLeftWorld;
                _slideRightFrom = _currentRightWorld;
                _slideLeftTo = leftWorld;
                _slideRightTo = rightWorld;
                if (_boundSlideRoutine != null)
                {
                    StopCoroutine(_boundSlideRoutine);
                }

                _boundSlideRoutine = StartCoroutine(SlideBoundsOpen());
                return;
            }

            StopBoundSlide();
            CommitHorizontalBounds(leftWorld, rightWorld, force);
        }

        private void StopBoundSlide()
        {
            if (_boundSlideRoutine == null)
            {
                return;
            }

            StopCoroutine(_boundSlideRoutine);
            _boundSlideRoutine = null;
        }

        private System.Collections.IEnumerator SlideBoundsOpen()
        {
            float duration = Mathf.Max(0.05f, _boundExpandDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                CommitHorizontalBounds(
                    Mathf.Lerp(_slideLeftFrom, _slideLeftTo, t),
                    Mathf.Lerp(_slideRightFrom, _slideRightTo, t),
                    force: true);
                yield return null;
            }

            CommitHorizontalBounds(_slideLeftTo, _slideRightTo, force: true);
            _boundSlideRoutine = null;
        }

        private void CommitHorizontalBounds(float leftWorld, float rightWorld, bool force)
        {
            bool changed = force
                || !Mathf.Approximately(_currentLeftWorld, leftWorld)
                || !Mathf.Approximately(_currentRightWorld, rightWorld);

            _currentLeftWorld = leftWorld;
            _currentRightWorld = rightWorld;

            if (_cameraLookAhead != null)
            {
                _cameraLookAhead.SetHorizontalConfine(leftWorld, rightWorld, _lookAheadFadeDistance);
            }

            if (_cameraBounds == null || !changed)
            {
                ApplyPinToScreenEdge();
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
            ApplyVerticalBounds(boundsTransform, ref size, ref offset);
            _cameraBounds.size = size;
            _cameraBounds.offset = offset;

            ApplyPinToScreenEdge();
            InvalidateConfinerCaches();
        }

        private void ApplyVerticalBounds(Transform boundsTransform, ref Vector2 size, ref Vector2 offset)
        {
            Transform searchRoot = transform.parent != null ? transform.parent : transform;
            Collider2D[] layoutColliders = searchRoot.GetComponentsInChildren<Collider2D>(true);
            if (!RoomBoundaryMath.TryEncapsulateVertical(layoutColliders, out float bottomWorld, out float topWorld))
            {
                return;
            }

            float paddedBottom = bottomWorld - _verticalPadding;
            float paddedTop = topWorld + _verticalPadding;
            float sampleX = boundsTransform.position.x;
            float bottomLocal = boundsTransform.InverseTransformPoint(new Vector3(sampleX, paddedBottom, 0f)).y;
            float topLocal = boundsTransform.InverseTransformPoint(new Vector3(sampleX, paddedTop, 0f)).y;
            float localMin = Mathf.Min(bottomLocal, topLocal);
            float localMax = Mathf.Max(bottomLocal, topLocal);
            size.y = localMax - localMin;
            offset.y = (localMin + localMax) * 0.5f;
        }

        private void ApplyPinToScreenEdge()
        {
            if (!_pinPlayerToScreenEdge || _confinerExtensions == null)
            {
                return;
            }

            for (int i = 0; i < _confinerExtensions.Length; i++)
            {
                if (_confinerExtensions[i] != null)
                {
                    _confinerExtensions[i].SlowingDistance = 0f;
                }
            }
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!RoomBoundaryMath.TryResolveLockedX(
                    _entranceWall,
                    _exitWall,
                    _exitTrigger != null ? _exitTrigger.ExitCollider : null,
                    out float left,
                    out float right))
            {
                return;
            }

            Vector3 origin = transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(left, origin.y - 8f, 0f), new Vector3(left, origin.y + 8f, 0f));
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(new Vector3(right, origin.y - 8f, 0f), new Vector3(right, origin.y + 8f, 0f));
        }
#endif
    }
}
