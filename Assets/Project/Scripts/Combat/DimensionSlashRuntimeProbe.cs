#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Project.Progression;
using UnityEngine;

[AddComponentMenu("")]
[DefaultExecutionOrder(10000)]
public sealed class DimensionSlashRuntimeProbe : MonoBehaviour
{
    public PlayerCharacter player;
    public EnemyController target;
    public Camera captureCamera;
    public CharacterObject characterSource;
    public bool captureFrames = true;
    public bool captureRawFrames = true;

    [Serializable] public sealed class Contact
    {
        public int frame;
        public float damage;
        public float realtime;
        public bool critical;
        public string actionId;
        public string delivery;
        public string step;
        public Vector3 playerPosition;
        public Vector3 targetPosition;
    }
    [Serializable] public sealed class Run
    {
        public int requestedFps;
        public string actionId;
        public string stepSequence;
        public int requestedFacing;
        public int renderFrames;
        public float observedFps;
        public bool armedAfterFive;
        public bool consumed;
        public bool ended;
        public bool cameraReleased;
        public string error;
        public List<Contact> contacts = new List<Contact>();
        public List<Contact> otherDamage = new List<Contact>();
        public int capturedFrames;
        public float duration;
        public float targetHpBefore;
        public float targetHpAfter;
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public List<Frame> frames = new List<Frame>();
        public float minimumTimeScale = 1f;
        public bool actorRestored;
    }
    [Serializable] public sealed class Frame
    {
        public int logicFrame;
        public float realtime;
        public float cameraSize;
        public string actorSprite;
        public string step;
        public bool actorHidden;
        public float presentationFrame;
        public int seed;
        public int foregroundCuts;
        public int totalCuts;
        public Vector3 visualOrigin;
        public string state;
        public bool attackBuffered;
        public string availableNormal;
    }
    [Serializable] public sealed class Report
    {
        public string startedUtc;
        public List<Run> runs = new List<Run>();
        public bool finished;
        public string error;
        public bool pausePreservedWindow;
        public bool expiredWindowFallsBack;
        public bool startupDamageReleasesCamera;
        public bool hiddenActorRestoredOnDisable;
        public bool executionWindowOpened;
        public bool executionRecoveryNaAdvanced;
        public bool executionConsumed;
        public bool executionPreservedCachedCuts;
        public bool executionExpired;
        public string executionInputPhase;
        public string executionStateAfterInput;
        public int executionDepthAfterInput;
    }

    private const string Slash = "main.skill.dimension_slash";
    private const string Count = "main.dimension_slash.cut_count";
    private const string Ready = "main.dimension_slash.ready";
    private const string Cut = "main.dash.dimension_cut";
    private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs_JJK/3_Outputs/Reports/MainDimensionSlash/Revision3/Runtime/")) + Path.DirectorySeparatorChar;
    private const string Execution = "ExecutionBreakdown";
    private string _recordedActionId = Slash;
    private readonly Report _report = new Report();
    private Run _run;
    private bool _recording;
    private RenderTexture _render;
    private Texture2D _pixels;
    private int _previousLogicFrame = -1;
    private int _originalFps;
    private int _originalVsync;
    private bool _originalBackground;
    private CharacterObject _character;
    private CharacterProgressionState _progression;
    private SpriteRenderer _actorRenderer;
    private ProceduralSlashField _field;

    private void Awake()
    {
        Directory.CreateDirectory(Output);
        _report.startedUtc = DateTime.UtcNow.ToString("o"); Write();
    }

    private IEnumerator Start()
    {
        yield return null; // Let the real prefab Start methods finish first.
        IEnumerator routine = Capture();
        while (true)
        {
            object next;
            try
            {
                if (!routine.MoveNext()) break;
                next = routine.Current;
            }
            catch (Exception exception)
            {
                _report.error = exception.ToString(); Write();
                Debug.LogException(exception);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }
            yield return next;
        }
    }

    private IEnumerator Capture()
    {
        _originalFps = Application.targetFrameRate; _originalVsync = QualitySettings.vSyncCount;
        _originalBackground = Application.runInBackground;
        Application.runInBackground = true; QualitySettings.vSyncCount = 0;
        Directory.CreateDirectory(Output); _report.startedUtc = DateTime.UtcNow.ToString("o");
        CombatEventHub.Shared.DamageResolved += Damage;
        _character = Instantiate(characterSource);
        _character.actionResolverTable = Instantiate(characterSource.actionResolverTable);
        _progression = ScriptableObject.CreateInstance<CharacterProgressionState>();
        _progression.SetPotentialLevel(_character, PotentialTrack.Passive, 4);
        player.SetCharacter(_character);
        _actorRenderer = player.Animator != null ? player.Animator.GetComponent<SpriteRenderer>() : null;
        player.PassiveController.SetMetaProgression(_progression);
        var input = player.GetComponent<UnityEngine.InputSystem.PlayerInput>(); if (input != null) input.enabled = false;
        var handler = player.GetComponent<InputHandler_Player>(); if (handler != null) handler.enabled = false;
        target.maxHP = target.currentHP = 1000000f; target.ignoreHitReactions = true; target.SetBehaviorAgentActive(false);
        var brain = target.GetComponent<EnemyBehaviorBrain>(); if (brain != null) brain.enabled = false;
        var locomotion = target.GetComponent<EnemyLocomotion>(); if (locomotion != null) locomotion.enabled = false;
        if (target.rb != null) { target.rb.bodyType = RigidbodyType2D.Kinematic; target.rb.constraints = RigidbodyConstraints2D.FreezeAll; }
        _render = new RenderTexture(960, 540, 24); _pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
        yield return new WaitForSeconds(0.5f);
        foreach (int fps in new[] { 30, 60, 120 })
        foreach (int requestedFacing in new[] { 1, -1 })
        {
            Application.targetFrameRate = fps;
            _run = new Run { requestedFps = fps, requestedFacing = requestedFacing, actionId = Slash, stepSequence = "JudgmentCut" }; _report.runs.Add(_run);
            player.Combat.ResetCombo(); player.StateMachine.ChangeState(player.StateMachine.IdleState);
            player.Movement.CancelAttackMotion();
            player.transform.position = new Vector3(0f, 0f, 0f);
            var body = player.GetComponent<Rigidbody2D>(); body.linearVelocity = Vector2.zero;
            target.transform.position = new Vector3(3f * requestedFacing, 0f, 0f);
            if (target.rb != null) target.rb.linearVelocity = Vector2.zero;
            target.currentHP = target.maxHP;
            var runtime = player.PassiveController.RuntimeState;
            runtime.SetCounter(Count, 0); runtime.RemoveTag(Ready); runtime.SetCounter("main.shadow.cached_cuts", 5);
            yield return new WaitForSeconds(0.3f);
            target.transform.position = new Vector3(3f * requestedFacing, player.transform.position.y, 0f);
            Physics2D.SyncTransforms();
            for (int i = 0; i < 5; i++)
            {
                player.Movement.SnapFacingDirection(target.transform.position.x >= player.transform.position.x ? 1 : -1);
                player.StateMachine.RouteAttackInput();
                float until = Time.realtimeSinceStartup + 4f;
                while (Time.realtimeSinceStartup < until)
                {
                    if (player.Combat.CurrentActionId == Cut && player.StateMachine.CurrentState is FrameDataPlayerState state
                        && (state.CurrentPhase == ActionPhase.Recovery || state.CurrentPhase == ActionPhase.Cancel)) break;
                    yield return null;
                }
                if (player.Combat.CurrentActionId != Cut) { _run.error = "Cut did not start at index " + i; break; }
            }
            _run.armedAfterFive = runtime.HasTag(Ready) && runtime.GetCounter("main.shadow.cached_cuts") == 0;
            if (!_run.armedAfterFive)
            {
                _run.error ??= "Five paid cuts did not open readiness."; Write(); continue;
            }
            player.Movement.SnapFacingDirection(target.transform.position.x >= player.transform.position.x ? 1 : -1);
            _run.startPosition = player.transform.position; _run.targetPosition = target.transform.position;
            _recording = true; _previousLogicFrame = -1;
            float start = Time.realtimeSinceStartup;
            _run.targetHpBefore = target.currentHP;
            player.StateMachine.RouteSkillInput();
            _run.consumed = player.Combat.CurrentActionId == Slash && !runtime.HasTag(Ready);
            while (Time.realtimeSinceStartup - start < 5f && player.Combat.CurrentActionId == Slash) yield return null;
            _run.duration = Time.realtimeSinceStartup - start;
            _run.targetHpAfter = target.currentHP;
            _run.observedFps = _run.renderFrames / Mathf.Max(0.001f, _run.duration);
            _run.ended = player.Combat.CurrentActionId != Slash;
            _recording = false;
            _run.cameraReleased = CameraManager.instance == null || !CameraManager.instance.HasActionCamera;
            _run.actorRestored = _actorRenderer == null || !_actorRenderer.forceRenderingOff;
            Write(); yield return new WaitForSeconds(0.2f);
        }
        var gateState = player.PassiveController.RuntimeState;
        player.Combat.ResetCombo(); player.StateMachine.ChangeState(player.StateMachine.IdleState);
        player.Movement.CancelAttackMotion();
        player.transform.position = Vector3.zero; target.transform.position = new Vector3(3f,0,0);
        player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.2f);
        target.transform.position = player.transform.position + Vector3.right * 3f;
        gateState.SetCounter("main.shadow.cached_cuts", 2);
        player.Movement.SnapFacingDirection(1);
        _character.qteSwap.TryResolveIntro(_character.actionResolverTable, out var introAction, out var introStep);
        player.StateMachine.QteIntroState.Prepare(introAction, introStep, target);
        player.StateMachine.ChangeState(player.StateMachine.QteIntroState);
        _run = new Run { requestedFps = 60, requestedFacing = 1, actionId = Cut, stepSequence = Execution };
        _report.runs.Add(_run); _recordedActionId = Cut;
        _recording = true; _previousLogicFrame = -1;
        float executionStart = Time.realtimeSinceStartup;
        _run.targetHpBefore = target.currentHP;
        while (Time.realtimeSinceStartup - executionStart < 4f
            && player.StateMachine.QteIntroState.CurrentFrame < introStep.RecoveryPhaseStart) yield return null;
        _report.executionWindowOpened = player.Combat.GetCurrentStepData() == introStep;
        _report.executionInputPhase = player.StateMachine.QteIntroState.CurrentPhase.ToString();
        player.StateMachine.RouteAttackInput();
        _report.executionStateAfterInput = player.StateMachine.CurrentStateId.ToString();
        _report.executionDepthAfterInput = player.Combat.GetComboStep();
        _report.executionRecoveryNaAdvanced = player.Combat.GetCurrentStepData()?.stepName == "ExecutionBreakdown";
        _run.consumed = _report.executionConsumed = _report.executionRecoveryNaAdvanced;
        while (Time.realtimeSinceStartup - executionStart < 7f && player.Combat.GetCurrentStepData()?.stepName == "ExecutionBreakdown") yield return null;
        _recording = false; _run.duration = Time.realtimeSinceStartup - executionStart;
        _run.observedFps = _run.renderFrames / Mathf.Max(0.001f, _run.duration);
        _run.ended = player.Combat.GetCurrentStepData()?.stepName != "ExecutionBreakdown";
        _run.targetHpAfter = target.currentHP;
        _run.actorRestored = _actorRenderer == null || !_actorRenderer.forceRenderingOff;
        _run.cameraReleased = CameraManager.instance == null || !CameraManager.instance.HasActionCamera;
        _report.executionPreservedCachedCuts = gateState.GetCounter("main.shadow.cached_cuts") == 2;
        player.Combat.ResetCombo(); player.StateMachine.ChangeState(player.StateMachine.IdleState);
        player.StateMachine.QteIntroState.Prepare(introAction, introStep, target);
        player.StateMachine.ChangeState(player.StateMachine.QteIntroState);
        yield return new WaitForSeconds(2.7f);
        _report.executionExpired = player.StateMachine.CurrentState != player.StateMachine.QteIntroState
            && player.Combat.GetCurrentStepData()?.stepName != "ExecutionBreakdown";
        Write();
        gateState.SetTag(Ready, 1.5f);
        gateState.TryGetTagTiming(Ready, out float beforePause, out _);
        Time.timeScale = 0f; yield return new WaitForSecondsRealtime(0.2f);
        gateState.TryGetTagTiming(Ready, out float afterPause, out _);
        _report.pausePreservedWindow = Mathf.Abs(beforePause - afterPause) < 0.01f;
        Time.timeScale = 1f; yield return new WaitForSeconds(1.6f);
        var fallback = new ActionResolver();
        fallback.ResolveInitial(new InputContext { isGrounded = true, passiveController = player.PassiveController,
            runtimeState = gateState, actionAvailabilityProvider = player }, _character.actionResolverTable, AttackCategory.Skill);
        _report.expiredWindowFallsBack = !gateState.HasTag(Ready) && fallback.ActiveCombatAction?.actionId == "main.skill.dark_chain";
        player.Combat.ResetCombo(); player.StateMachine.ChangeState(player.StateMachine.IdleState);
        gateState.SetTag(Ready, 1.5f); player.StateMachine.RouteSkillInput();
        yield return new WaitForFixedUpdate();
        player.TakeDamage(1f, IncomingAttackContext.FromEnemy(target, EnemyAttackProperty.Normal));
        _report.startupDamageReleasesCamera = player.StateMachine.CurrentState == player.StateMachine.HitstunState
            && (CameraManager.instance == null || !CameraManager.instance.HasActionCamera);
        player.Combat.ResetCombo(); player.StateMachine.ChangeState(player.StateMachine.IdleState);
        gateState.SetTag(Ready, 1.5f); player.StateMachine.RouteSkillInput();
        float stopAt = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < stopAt
            && !(_actorRenderer != null && _actorRenderer.forceRenderingOff)) yield return null;
        bool wasHidden = _actorRenderer != null && _actorRenderer.forceRenderingOff;
        player.gameObject.SetActive(false);
        _report.hiddenActorRestoredOnDisable = wasHidden && !_actorRenderer.forceRenderingOff;
        _report.finished = true; Write();
        UnityEditor.EditorApplication.isPlaying = false;
    }

    private void Damage(DamageResolvedEvent damage)
    {
        if (!_recording || _run == null || damage.target != target) return;
        var contact = new Contact
        {
            frame = player.StateMachine.CurrentState is FrameDataPlayerState state ? state.CurrentFrame : -1,
            damage = damage.damageDealt, critical = damage.isCriticalHit, realtime = Time.realtimeSinceStartup,
            actionId = damage.actionId, delivery = damage.descriptor.delivery.ToString(),
            step = player.Combat.GetCurrentStepData()?.stepName,
            playerPosition = player.transform.position, targetPosition = target.transform.position
        };
        if (damage.actionId == _recordedActionId && damage.descriptor.delivery == DamageDelivery.Primary
            && (_run.stepSequence != Execution || contact.step == Execution)) _run.contacts.Add(contact);
        else _run.otherDamage.Add(contact);
    }

    private void LateUpdate()
    {
        if (!_recording || _run == null) return;
        _run.renderFrames++;
        _run.minimumTimeScale = Mathf.Min(_run.minimumTimeScale, Time.timeScale);
        if (!captureFrames || captureCamera == null || _render == null) return;
        int frame = player.StateMachine.CurrentState is FrameDataPlayerState state ? state.CurrentFrame : -1;
        if (frame == _previousLogicFrame || frame < 0) return;
        _previousLogicFrame = frame;
        string folder = Output + (_run.stepSequence == Execution ? "execution_" : "") + _run.requestedFps + "fps_" + _run.requestedFacing + "/"; Directory.CreateDirectory(folder);
        RenderTexture previous = RenderTexture.active;
        RenderTexture oldTarget = captureCamera.targetTexture;
        captureCamera.targetTexture = _render; captureCamera.Render(); RenderTexture.active = _render;
        _pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); _pixels.Apply();
        if (captureRawFrames)
            File.WriteAllBytes(folder + "frame" + _run.capturedFrames.ToString("D4") + ".rgb", _pixels.GetRawTextureData());
        else
            File.WriteAllBytes(folder + "frame" + _run.capturedFrames.ToString("D4") + ".png", _pixels.EncodeToPNG());
        _run.capturedFrames++;
        var sprite = _actorRenderer;
        if (_field == null || !_field.IsPlaying)
            foreach (var candidate in player.GetComponentsInChildren<ProceduralSlashField>())
                if (candidate.IsPlaying) { _field = candidate; break; }
        player.TryResolveActionForContext(AttackCategory.Normal, out CombatAction availableNormal, out _, requireAvailability: true);
        _run.frames.Add(new Frame { logicFrame = frame, realtime = Time.realtimeSinceStartup,
            state = player.StateMachine.CurrentStateId.ToString(), attackBuffered = player.StateMachine.InputBuffer.HasAttackBuffer,
            availableNormal = availableNormal?.actionId,
            cameraSize = captureCamera.orthographicSize, actorSprite = sprite != null && sprite.sprite != null ? sprite.sprite.name : "",
            step = player.Combat.GetCurrentStepData()?.stepName, actorHidden = sprite != null && sprite.forceRenderingOff,
            presentationFrame = _field != null ? _field.SampledFrame : 0f, seed = _field != null ? _field.ActiveSeed : 0,
            foregroundCuts = _field != null ? _field.ForegroundSlashCount : 0, totalCuts = _field != null ? _field.TotalSlashCount : 0,
            visualOrigin = _field != null && _field.VisualRoot != null ? _field.VisualRoot.position : Vector3.zero });
        captureCamera.targetTexture = oldTarget; RenderTexture.active = previous;
    }

    private void Write() => File.WriteAllText(Output + "runtime-report.json", JsonUtility.ToJson(_report, true));

    private void OnDestroy()
    {
        CombatEventHub.Shared.DamageResolved -= Damage;
        Application.targetFrameRate = _originalFps; QualitySettings.vSyncCount = _originalVsync;
        Application.runInBackground = _originalBackground;
        if (_render != null) { _render.Release(); Destroy(_render); }
        if (_pixels != null) Destroy(_pixels);
        if (_progression != null) Destroy(_progression);
        if (_character != null) { Destroy(_character.actionResolverTable); Destroy(_character); }
    }
}
#endif
