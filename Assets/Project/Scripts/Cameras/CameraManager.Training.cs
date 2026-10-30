using Unity.Cinemachine;
using UnityEngine;

public partial class CameraManager
{
    private bool _trainingMode;
    private CinemachineCamera _normalCombatCam;
    private CinemachineCamera _trainingCam;

    public void SetTrainingMode(bool active, CinemachineCamera camera)
    {
        if (active)
        {
            if (camera == null) return;
            if (!_trainingMode) _normalCombatCam = _combatCam;
            _trainingMode = true;
            _trainingCam = camera;
            if (_normalCombatCam != null) _normalCombatCam.Priority = _inactivePriority;
            _combatCam = camera;
            float size = camera.Lens.OrthographicSize;
            var framing = camera.GetComponent<CinemachineGroupFraming>();
            if (framing != null) framing.OrthoSizeRange = new Vector2(size, size);
            CacheCombatGroupFraming();
            if (_combatTargetGroup != null) camera.Follow = _combatTargetGroup.transform;
            EnsurePlayerInTargetGroup();
            _isInCombat = true;
            SetCombatMode();
        }
        else if (_trainingMode)
        {
            if (_trainingCam != null) _trainingCam.Priority = _inactivePriority;
            _trainingMode = false;
            _combatCam = _normalCombatCam;
            _normalCombatCam = _trainingCam = null;
            CacheCombatGroupFraming();
            ExitCombatMode();
        }
    }
}
