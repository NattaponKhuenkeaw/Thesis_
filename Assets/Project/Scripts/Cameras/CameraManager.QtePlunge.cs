using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public readonly struct QteIntroCameraToken
{
    public readonly int generation;
    public QteIntroCameraToken(int generation) { this.generation = generation; }
}

public partial class CameraManager
{
    private readonly List<QteIntroCameraImpact> _plungeCameras = new List<QteIntroCameraImpact>();
    private CombatExecutionContext _plungeCameraExecution;
    private int _plungeCameraGeneration;
    private float _plungeCameraSize;
    private float _plungeCameraActorOffsetY;
    private float _plungeCameraReturnSeconds;

    public bool TryGetQtePlungeCameraFocus(int generation, out Vector3 focus, out float size)
    {
        focus = default; size = _plungeCameraSize;
        if (!IsQtePlungeCameraValid(generation)) return false;
        focus = _plungeCameraExecution.owner.transform.position + Vector3.up * _plungeCameraActorOffsetY;
        return true;
    }

    public QteIntroCameraToken BeginQtePlungeCamera(CombatExecutionContext execution,
        Vector3 start, Vector3 contact, int impactFrame)
    {
        ReleaseQtePlungeCamera(new QteIntroCameraToken(_plungeCameraGeneration));
        if (!isActiveAndEnabled || execution == null || !execution.IsOnField) return default;
        _plungeCameraGeneration++;
        _plungeCameraExecution = execution;
        Camera output = _brain != null ? _brain.OutputCamera : Camera.main;
        _plungeCameraSize = output != null ? output.orthographicSize : 6.8f;
        var settings = execution.owner.GetComponent<PlayerCharacter>()?.characterObject?.qteSwap?.plunge;
        _plungeCameraActorOffsetY = settings != null ? settings.cameraActorOffsetY : 1.5f;
        _plungeCameraReturnSeconds = settings != null ? Mathf.Max(0.05f, settings.cameraReturnSeconds) : 0.45f;
        ResetLookAheadForCharacterSwap(execution.owner.GetComponent<PlayerMovement>()?.RB, 0.24f);
        _cameraLookAhead?.SnapToPlayer();
        NotifyPlayerWarp(Vector3.zero);
        _plungeCameras.Clear();
        AddPlungeCamera(_exploreCam, start, contact, impactFrame);
        AddPlungeCamera(_combatCam, start, contact, impactFrame);
        AddPlungeCamera(_staticCam, start, contact, impactFrame);
        AddPlungeCamera(_ultimateCam, start, contact, impactFrame);
        // A handoff must not interpolate the old grounded camera into the new midair lock.
        if (_brain != null) _brain.ActiveBlend = null;
        return new QteIntroCameraToken(_plungeCameraGeneration);
    }

    private void AddPlungeCamera(CinemachineCamera camera, Vector3 start, Vector3 contact, int impactFrame)
    {
        if (camera == null) return;
        var extension = GetQteIntroCameraImpact(camera);
        if (_plungeCameras.Contains(extension)) return;
        _plungeCameras.Add(extension);
        extension.BeginPlunge(this, _plungeCameraGeneration, start, contact, impactFrame, _plungeCameraReturnSeconds);
        camera.PreviousStateIsValid = false;
    }

    public bool IsQtePlungeCameraValid(int generation) => generation == _plungeCameraGeneration
        && _plungeCameraExecution != null && _plungeCameraExecution.IsOnField
        && _plungeCameraExecution.owner.CurrentExecution == _plungeCameraExecution
        && _plungeCameraExecution.owner.IsAttacking();

    public void SampleQtePlungeCamera(QteIntroCameraToken token, float actionFrame)
    {
        if (!IsQtePlungeCameraValid(token.generation)) return;
        foreach (var extension in _plungeCameras)
            if (extension != null) extension.SamplePlunge(token.generation, actionFrame);
    }

    public void ReleaseQtePlungeCamera(QteIntroCameraToken token)
    {
        if (token.generation != _plungeCameraGeneration) return;
        foreach (var extension in _plungeCameras)
            if (extension != null) extension.ReleasePlunge(token.generation);
        _plungeCameraExecution = null;
        // The extension completes its release independently of the old combat execution.
        // Keep composer/group history: invalidating it here causes the exit jump cut.
    }
}
