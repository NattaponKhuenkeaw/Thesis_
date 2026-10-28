using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using CombatCore;

public readonly struct ActionCameraToken
{
    public int Generation { get; }
    public ActionCameraToken(int generation) => Generation = generation;
}

public partial class CameraManager
{
    private readonly List<CinemachineCamera> _actionMotionCameras = new List<CinemachineCamera>();
    private CombatExecutionContext _actionMotionExecution;
    private ActionCameraMotionSettings _actionMotionSettings;
    private ScreenImpulseCameraSettings _screenImpulseCameraSettings;
    private Vector3 _actionCameraLockedPosition;
    private int _actionMotionGeneration;
    private int _actionMotionStartFrame;
    private int _actionMotionFacing;
    private float _actionMotionFrame;
    private int _timedCameraStartUnityFrame;

    public bool HasActionCamera => _actionMotionExecution != null && _actionMotionExecution.IsOnField
        && (_screenImpulseCameraSettings != null
            || (_actionMotionExecution.owner.CurrentExecution == _actionMotionExecution
                && _actionMotionExecution.owner.IsAttacking()));

    public ActionCameraToken BeginActionCamera(CombatExecutionContext execution,
        ActionCameraMotionSettings settings, int eventStartFrame, int capturedFacing)
    {
        ClearActionCamera();
        if (!isActiveAndEnabled || execution == null || !execution.IsOnField || execution.isQteIntro
            || settings == null || _isTemporarilyFocused || _focusReturnCoroutine != null)
            return default;

        _actionMotionExecution = execution;
        _actionMotionSettings = settings;
        _actionMotionStartFrame = eventStartFrame;
        _actionMotionFrame = eventStartFrame;
        _actionMotionFacing = capturedFacing >= 0 ? 1 : -1;
        GetComponentsInChildren(true, _actionMotionCameras);
        for (int i = 0; i < _actionMotionCameras.Count; i++)
        {
            CinemachineCamera camera = _actionMotionCameras[i];
            var extension = camera.GetComponent<ActionCameraMotionExtension>();
            if (extension == null) extension = camera.gameObject.AddComponent<ActionCameraMotionExtension>();
            var confiner = camera.GetComponent<CinemachineConfiner2D>();
            extension.Configure(this, confiner, camera == _staticCam);
        }
        return new ActionCameraToken(_actionMotionGeneration);
    }

    public ActionCameraToken BeginScreenImpulseCamera(CombatExecutionContext execution,
        ScreenImpulseCameraSettings settings, int eventStartFrame, int capturedFacing = 1)
    {
        if (settings == null || !settings.enabled) return default;
        Camera output = _brain != null ? _brain.OutputCamera : Camera.main;
        if (output == null || (_ultimateCam != null && _ultimateCam.Priority.Value >= _activePriority))
            return default;

        Vector3 capturedPosition = output.transform.position;
        ActionCameraToken token = BeginActionCamera(execution, new ActionCameraMotionSettings(), eventStartFrame, capturedFacing);
        if (token.Generation == 0) return default;
        _screenImpulseCameraSettings = settings;
        _actionCameraLockedPosition = capturedPosition;
        _timedCameraStartUnityFrame = Time.frameCount;
        return token;
    }

    private void Update()
    {
        if (_screenImpulseCameraSettings == null) return;
        if (!HasActionCamera) { ClearActionCamera(); return; }
        var stateMachine = _actionMotionExecution.owner.playerCharacter.StateMachine;
        if (stateMachine != null && (stateMachine.CurrentStateId == PlayerStateId.Hitstun
            || stateMachine.CurrentStateId == PlayerStateId.Dead))
        {
            ClearActionCamera();
            return;
        }
        // Scaled gameplay time preserves hitstop, pause and slow motion without being cut short by a step.
        if (Time.frameCount == _timedCameraStartUnityFrame) return;
        AdvanceTimedCamera(Time.deltaTime);
    }

    public void AdvanceTimedCamera(float deltaSeconds)
    {
        if (_screenImpulseCameraSettings == null) return;
        if (!HasActionCamera) { ClearActionCamera(); return; }
        _actionMotionFrame += Mathf.Max(0f, deltaSeconds) * FrameDataConstants.LOGIC_FPS;
        if (_actionMotionFrame - _actionMotionStartFrame >= _screenImpulseCameraSettings.DurationFrames)
            ClearActionCamera();
    }

    public bool TrySampleScreenImpulseCamera(out float zoomMultiplier, out Vector3 lockedPosition, out float lockWeight)
    {
        zoomMultiplier = 1f;
        lockedPosition = _actionCameraLockedPosition;
        lockWeight = 0f;
        if (!HasActionCamera || _screenImpulseCameraSettings == null) return false;
        float frame = _actionMotionFrame - _actionMotionStartFrame;
        float weight = _screenImpulseCameraSettings.SampleWeight(frame);
        zoomMultiplier = Mathf.Lerp(1f, Mathf.Max(0.01f, _screenImpulseCameraSettings.zoomMultiplier), weight);
        // The lock is a cut even when the lens has a gradual transition.
        if (_screenImpulseCameraSettings.enabled && _screenImpulseCameraSettings.jumpCutLock && frame >= 0f)
        {
            int holdEnd = Mathf.Max(0, _screenImpulseCameraSettings.transitionInFrames)
                + Mathf.Max(0, _screenImpulseCameraSettings.holdFrames);
            lockWeight = frame <= holdEnd ? 1f : weight;
        }
        return true;
    }

    public void SampleActionCamera(ActionCameraToken token, float actionFrame)
    {
        if (token.Generation == 0 || token.Generation != _actionMotionGeneration) return;
        if (!HasActionCamera) { ClearActionCamera(); return; }
        if (_screenImpulseCameraSettings != null) return;
        _actionMotionFrame = actionFrame;
        if (_actionMotionSettings != null
            && actionFrame - _actionMotionStartFrame >= _actionMotionSettings.DurationFrames)
        {
            ClearActionCamera();
        }
    }

    public void ReleaseActionCamera(ActionCameraToken token, bool allowTimedCompletion = false)
    {
        if (allowTimedCompletion && _screenImpulseCameraSettings != null) return;
        if (token.Generation != 0 && token.Generation == _actionMotionGeneration) ClearActionCamera();
    }

    public void ClearActionCamera()
    {
        _actionMotionExecution = null;
        _actionMotionSettings = null;
        _screenImpulseCameraSettings = null;
        unchecked { _actionMotionGeneration++; }
        if (_actionMotionGeneration == 0) _actionMotionGeneration = 1;
    }

    public bool TrySampleActionCamera(out float sizeMultiplier, out float viewportOffset)
    {
        sizeMultiplier = 1f;
        viewportOffset = 0f;
        if (!HasActionCamera || _actionMotionSettings == null) return false;
        if (_screenImpulseCameraSettings != null)
        {
            float weight = _screenImpulseCameraSettings.SampleWeight(_actionMotionFrame - _actionMotionStartFrame);
            viewportOffset = Mathf.Clamp(_screenImpulseCameraSettings.horizontalViewportOffset, -1f, 1f)
                * weight * _actionMotionFacing;
            return true;
        }
        _actionMotionSettings.Sample(_actionMotionFrame - _actionMotionStartFrame,
            out sizeMultiplier, out viewportOffset);
        viewportOffset *= _actionMotionFacing;
        return true;
    }
}
