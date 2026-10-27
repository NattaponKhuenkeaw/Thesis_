using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

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
    private int _actionMotionGeneration;
    private int _actionMotionStartFrame;
    private int _actionMotionFacing;
    private float _actionMotionFrame;

    public bool HasActionCamera => _actionMotionExecution != null && _actionMotionExecution.IsOnField
        && _actionMotionExecution.owner.CurrentExecution == _actionMotionExecution
        && _actionMotionExecution.owner.IsAttacking();

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

    public void SampleActionCamera(ActionCameraToken token, float actionFrame)
    {
        if (token.Generation == 0 || token.Generation != _actionMotionGeneration) return;
        if (!HasActionCamera) { ClearActionCamera(); return; }
        _actionMotionFrame = actionFrame;
    }

    public void ReleaseActionCamera(ActionCameraToken token)
    {
        if (token.Generation != 0 && token.Generation == _actionMotionGeneration) ClearActionCamera();
    }

    public void ClearActionCamera()
    {
        _actionMotionExecution = null;
        _actionMotionSettings = null;
        unchecked { _actionMotionGeneration++; }
        if (_actionMotionGeneration == 0) _actionMotionGeneration = 1;
    }

    public bool TrySampleActionCamera(out float sizeMultiplier, out float viewportOffset)
    {
        sizeMultiplier = 1f;
        viewportOffset = 0f;
        if (!HasActionCamera || _actionMotionSettings == null) return false;
        _actionMotionSettings.Sample(_actionMotionFrame - _actionMotionStartFrame,
            out sizeMultiplier, out viewportOffset);
        viewportOffset *= _actionMotionFacing;
        return true;
    }
}
