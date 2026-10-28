#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Artist preview using an isolated unsaved scene and the public visual sampling API.</summary>
public sealed class LightningPlungePreviewWindow : EditorWindow
{
    private PreviewContext _preview;
    private float _frame;
    private bool _playing;
    private double _lastTime;
    private float PreviewEndFrame => _preview != null ? _preview.EndFrame
        : MainLightningPlungeAuthoring.StartupFrames + MainLightningPlungeAuthoring.ActiveFrames + MainLightningPlungeAuthoring.RecoveryFrames;
    private const string CaptureRoot = "Docs_JJK/3_Outputs/Reports/MainLightningPlunge/";
    private static EnemyController _hitPreviewTarget;

    [MenuItem("Tools/Project/Combat/Capture Main Plunge Actual Hit (Play Mode)")]
    public static void CaptureActualHit()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Enter Play mode to capture the actual damage frame."); return; }
        HitFeedbackEvents.FeedbackRequested -= OnActualHit;
        HitFeedbackEvents.FeedbackRequested += OnActualHit;
        var player = TeamLoader.Instance?.playerCharacter;
        var profile = player?.characterObject?.qteSwap;
        if (profile == null || !profile.TryResolveIntro(player.characterObject.actionResolverTable, out var action, out var step)) return;
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (!enemy.IsTargetable) continue;
            if (!player.Movement.TryFindQtePlungePlacement(enemy.transform.position,
                profile.plunge.targetStartOffset, player.Movement.FacingDirection, out var placement)) continue;
            player.StateMachine.QteIntroState.Prepare(new QtePreparedEntry(action, step, enemy, QteEntryMode.LightningPlunge, placement));
            player.StateMachine.ChangeState(player.StateMachine.QteIntroState);
            TeamLoader.Instance.QteSwaps?.PlayIntroHandoff();
            _hitPreviewTarget = enemy;
            EditorApplication.update -= SampleHitPreview;
            EditorApplication.update += SampleHitPreview;
            Debug.Log($"Actual-hit artist capture targeting {enemy.name}; readiness/search radius bypassed for this temporary Play-mode preview.");
            return;
        }
        HitFeedbackEvents.FeedbackRequested -= OnActualHit;
        Debug.LogWarning("Actual-hit preview found no enemy with valid floor placement.");
    }

    private static void SampleHitPreview()
    {
        var player = TeamLoader.Instance?.playerCharacter;
        if (!Application.isPlaying || player == null || _hitPreviewTarget == null)
        { EditorApplication.update -= SampleHitPreview; return; }
        var step = player.Combat.GetCurrentStepData();
        if (step?.stepName != MainLightningPlungeAuthoring.StepName)
        { EditorApplication.update -= SampleHitPreview; return; }
        if (step.hitData?.hitboxes == null) return;
        foreach (var hitbox in step.hitData.hitboxes)
        {
            if (hitbox is not NormalHitbox hit) continue;
            int frame = player.StateMachine.QteIntroState.CurrentFrame;
            if (frame < step.ActivePhaseStart + hit.startFrameOffset) return;
            EditorApplication.update -= SampleHitPreview;
            // Artist diagnostic: bypass trigger overlap only, retaining the real damage/feedback path.
            player.Combat.hitBoxDamage?.ProcessHit(_hitPreviewTarget);
            return;
        }
    }

    private static void OnActualHit(HitFeedbackRequest request)
    {
        var player = TeamLoader.Instance?.playerCharacter;
        if (player?.Combat.GetCurrentStepData()?.stepName != MainLightningPlungeAuthoring.StepName) return;
        HitFeedbackEvents.FeedbackRequested -= OnActualHit;
        EditorApplication.update -= SampleHitPreview;
        EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying) return;
            EditorApplication.isPaused = true;
            Debug.Log($"Actual plunge hit: tier={request.Tier}, contactVfxSuppressed={request.Hitbox?.suppressContactVfx}");
            var stack = UnityEngine.Rendering.VolumeManager.instance.stack;
            var chromatic = stack.GetComponent<UnityEngine.Rendering.Universal.ChromaticAberration>();
            var flare = stack.GetComponent<UnityEngine.Rendering.Universal.ScreenSpaceLensFlare>();
            Debug.Log($"Actual camera stack: chromatic active={chromatic.active}, intensity={chromatic.intensity.value}; lens flare active={flare.active}, intensity={flare.intensity.value}, chromatic={flare.chromaticAbberationIntensity.value}");
            foreach (var volume in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
                Debug.Log($"Actual volume: {volume.name}, weight={volume.weight}, profile={volume.sharedProfile?.name}, global={volume.isGlobal}");
            CaptureLiveNamed(request.Hitbox?.suppressContactVfx == true ? "contact-clean" : "contact-before");
        };
    }

    [MenuItem("Tools/Project/Combat/Preview Main Lightning Plunge")]
    public static void Open() => GetWindow<LightningPlungePreviewWindow>("Lightning Plunge");

    [MenuItem("Tools/Project/Combat/Capture Main Lightning Plunge Phases")]
    public static void Capture()
    {
        Directory.CreateDirectory(CaptureRoot);
        using (var preview = new PreviewContext())
            foreach (int frame in new[] { 0, 1, 2, 3, 6, 14, 28, 42, 55, 72, 90, 120 })
            { preview.Sample(frame); preview.Save(CaptureRoot + "phase-" + frame.ToString("D3") + ".png"); }
        Debug.Log("Lightning plunge visual phases captured in " + CaptureRoot);
    }

    [MenuItem("Tools/Project/Combat/Capture Main Lightning Plunge Motion")]
    public static void CaptureMotion()
    {
        string folder = Path.Combine(Path.GetTempPath(), "MainPlungeMotionPreview");
        Directory.CreateDirectory(folder);
        using (var preview = new PreviewContext())
            for (int frame = 0; frame <= preview.EndFrame; frame += 3)
            {
                preview.Sample(frame);
                preview.Save(Path.Combine(folder, "motion-" + frame.ToString("D3") + ".png"), false);
            }
        Debug.Log("Lightning plunge motion frames captured in " + folder);
    }

    [MenuItem("Tools/Project/Combat/Preview Current Main QTE (Play Mode)")]
    public static void PreviewLive()
    {
        PlayerCharacter player = TeamLoader.Instance?.playerCharacter;
        if (!Application.isPlaying || player == null || player.characterObject?.qteSwap?.entryMode != QteEntryMode.LightningPlunge)
        { Debug.LogWarning("Live plunge preview: enter Play mode with Main selected."); return; }
        var profile = player.characterObject.qteSwap;
        if (!profile.TryResolveIntro(player.characterObject.actionResolverTable, out var action, out var step)) return;
        var target = player.Combat.FindNearestEnemyTarget(action.GetTargetSearchRadius());
        if (target == null || !player.Movement.TryFindQtePlungePlacement(target.transform.position,
            profile.plunge.targetStartOffset, player.Movement.FacingDirection, out var placement))
        { Debug.LogWarning("Live plunge preview: no nearby enemy with a safe placement."); return; }
        var entry = new QtePreparedEntry(action, step, target, QteEntryMode.LightningPlunge, placement);
        player.StateMachine.QteIntroState.Prepare(entry);
        player.StateMachine.ChangeState(player.StateMachine.QteIntroState);
        TeamLoader.Instance.QteSwaps?.PlayIntroHandoff();
        Debug.Log("Live plunge preview started through the QTE state/motor with saved intro presentation. Readiness and the team transaction are bypassed for this artist preview.");
    }

    [MenuItem("Tools/Project/Combat/Continue Current QTE (Play Mode)")]
    public static void ContinueLive() => TeamLoader.Instance?.playerCharacter?.StateMachine?.RouteAttackInput();

    [MenuItem("Tools/Project/Combat/QTE Preview - Next Teammate (Play Mode)")]
    public static void NextTeammate()
    { if (Application.isPlaying) Debug.Log("Manual QTE preview swap: " + TeamLoader.Instance?.SwitchNext()); }

    [MenuItem("Tools/Project/Combat/QTE Preview - Current Skill (Play Mode)")]
    public static void CurrentSkill()
    { if (Application.isPlaying) TeamLoader.Instance?.playerCharacter?.StateMachine?.RouteSkillInput(); }

    [MenuItem("Tools/Project/Combat/QTE Preview - Swap To Main (Play Mode)")]
    public static void SwapToMain()
    {
        var team = TeamLoader.Instance;
        if (!Application.isPlaying || team == null) return;
        for (int i = 0; i < team.CharacterCount; i++)
            if (team.GetCharacterAtIndex(i)?.qteSwap?.entryMode == QteEntryMode.LightningPlunge)
            {
                bool switched = team.SwitchToIndex(i);
                Debug.Log($"Manual QTE handoff: switched={switched}, state={team.playerCharacter.StateMachine.CurrentStateId}");
                return;
            }
    }

    [MenuItem("Tools/Project/Combat/Step Plunge Preview Frame (Play Mode)")]
    public static void StepLive() { if (Application.isPlaying) EditorApplication.Step(); }

    [MenuItem("Tools/Project/Combat/Resume Plunge Preview (Play Mode)")]
    public static void ResumeLive() { if (Application.isPlaying) EditorApplication.isPaused = false; }

    [MenuItem("Tools/Project/Combat/Capture Current QTE View (Play Mode)")]
    public static void CaptureLive() => CaptureLiveNamed("live");

    private static void CaptureLiveNamed(string label)
    {
        var camera = Camera.main;
        var player = TeamLoader.Instance?.playerCharacter;
        if (!Application.isPlaying || camera == null || player == null) return;
        Directory.CreateDirectory(CaptureRoot);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        string path = CaptureRoot + label + "-" + player.StateMachine.QteIntroState.CurrentFrame.ToString("D3") + ".png";
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); Debug.Log("QTE view captured: " + path);
        }
        finally { camera.targetTexture = previousTarget; RenderTexture.active = previousActive; target.Release(); DestroyImmediate(target); DestroyImmediate(image); }
    }

    [MenuItem("Tools/Project/Combat/Inspect Current Plunge (Play Mode)")]
    public static void InspectLive()
    {
        var player = TeamLoader.Instance?.playerCharacter;
        if (player == null || !Application.isPlaying) return;
        var team = TeamLoader.Instance;
        Debug.Log($"QTE preview team: current={team.CurrentCharacter?.name}, count={team.CharacterCount}, cooldown={team.RemainingCooldown}, inputLocked={Project.UI.InputLockManager.IsLocked}");
        for (int i = 0; i < team.CharacterCount; i++)
        {
            var character = team.GetCharacterAtIndex(i);
            bool ready = team.QteSwaps.TryGetWindow(character, out float remaining, out _);
            Debug.Log($"QTE preview slot {i}: {character?.name}, alive={team.IsCharacterAlive(character)}, ready={ready}, remaining={remaining}");
        }
        var renderer = player.Animator.GetComponent<SpriteRenderer>();
        var camera = Camera.main;
        if (camera != null) Debug.Log($"Plunge camera: position={camera.transform.position}, size={camera.orthographicSize}, actorViewport={camera.WorldToViewportPoint(player.transform.position + Vector3.up)}");
        Debug.Log($"Plunge preview: state={player.StateMachine.CurrentStateId}, frame={player.StateMachine.QteIntroState.CurrentFrame}, step={player.Combat.GetCurrentStepData()?.stepName}, body={player.Movement.RB.position}, ground={player.Movement.GroundState.IsGrounded}, hidden={renderer?.forceRenderingOff}, capturedMotion={player.Movement.HasCapturedWorldMotion}");
        var effect = player.GetComponentInChildren<LightningPlungeVFX>(true);
        if (effect != null) Debug.Log($"Plunge particles: sparks={effect.impactSparks.particleCount}, debris={effect.debris.particleCount}, residual={effect.residualSparks.particleCount}, sample={effect.SampledFrame}");
    }

    private void OnEnable() { _lastTime = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
    private void OnDisable() { EditorApplication.update -= Tick; _preview?.Dispose(); _preview = null; }
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_playing) { _frame += (float)(now - _lastTime) * 60f; if (_frame >= PreviewEndFrame) _playing = false; Repaint(); }
        _lastTime = now;
    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Main QTE — action-frame visual preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Tune CharacterObject_main > QTE Swap > Plunge > Visuals. This preview has no gameplay damage or camera handoff.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(_playing ? "Pause" : "Play")) { if (_frame >= PreviewEndFrame) _frame = 0f; _playing = !_playing; }
            if (GUILayout.Button("Restart")) { _frame = 0f; _preview?.Restart(); }
            if (GUILayout.Button("Capture phases")) Capture();
        }
        _frame = EditorGUILayout.Slider("Action frame", _frame, 0f, PreviewEndFrame);
        Rect area = GUILayoutUtility.GetRect(320f, 720f, 180f, 600f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (Event.current.type != EventType.Repaint) return;
        try { _preview ??= new PreviewContext(); _preview.Sample(_frame); GUI.DrawTexture(area, _preview.texture, ScaleMode.ScaleToFit); }
        catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
    }

    private sealed class PreviewContext : IDisposable
    {
        private readonly Scene _scene;
        private readonly Camera _camera;
        private readonly LightningPlungeVFX _effect;
        private readonly LightningPlungeVfxSettings _settings;
        private readonly SpriteRenderer _actor;
        private readonly Sprite _landed, _standing;
        private readonly Material _actorMaterial;
        private readonly Material _groundMaterial;
        private readonly Sprite _floorSprite;
        private readonly float _landedOffset, _standingOffset;
        private readonly float _startHeight;
        private readonly AttackStepData _step;
        public readonly RenderTexture texture;
        public int EndFrame => Mathf.Max(_step.TotalFrames, _effect.PresentationEndFrame(_step.ActivePhaseStart));

        public PreviewContext()
        {
            var main = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset");
            if (main?.qteSwap?.plunge?.prefab == null) throw new InvalidOperationException("Author Main Lightning Plunge first.");
            _settings = main.qteSwap.plunge.visuals;
            _startHeight = main.qteSwap.plunge.targetStartOffset.y;
            _step = main.qteSwap.GetIntroComboTree(main.actionResolverTable).rootNode.stepData;
            _scene = EditorSceneManager.NewPreviewScene();
            texture = new RenderTexture(1280, 720, 24); texture.Create();
            _camera = Host("Plunge Preview Camera").AddComponent<Camera>();
            _camera.scene = _scene; _camera.enabled = false; _camera.orthographic = true;
            _camera.orthographicSize = 6.8f; _camera.transform.position = new Vector3(0f, 4.4f, -10f);
            _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = new Color(0.09f, 0.085f, 0.13f);
            _camera.targetTexture = texture;
            var effectObject = UnityEngine.Object.Instantiate(main.qteSwap.plunge.prefab.gameObject);
            SceneManager.MoveGameObjectToScene(effectObject, _scene);
            _effect = effectObject.GetComponent<LightningPlungeVFX>();
            var previewShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Project/Resources/FieldEffects/FieldEffectUnlit.shader");
            _actorMaterial = new Material(previewShader);
            var floor = Host("Floor").AddComponent<SpriteRenderer>();
            _floorSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);
            floor.sprite = _floorSprite;
            _groundMaterial = new Material(previewShader);
            _groundMaterial.SetColor("_Color", new Color(0.16f, 0.15f, 0.21f));
            floor.sharedMaterial = _groundMaterial;
            floor.transform.localScale = new Vector3(30f, 0.12f, 1f); floor.transform.position = Vector3.down * 0.08f;
            floor.sortingOrder = -10;
            _actor = Host("Main landing and standing pose").AddComponent<SpriteRenderer>();
            _actor.sharedMaterial = _actorMaterial;
            _actor.sortingLayerName = _settings.sortingLayer; _actor.sortingOrder = _settings.sortingOrder - 1;
            _landed = FirstSprite(MainLightningPlungeAuthoring.Root + "main_qte_lightning_plunge_actor.anim");
            _standing = FirstSprite(MainDimensionSlashAuthoring.MainPath + "animation/main_Idle.anim");
            _landedOffset = -MainLightningPlungeAuthoring.VisibleFootY(_landed);
            _standingOffset = -MainLightningPlungeAuthoring.VisibleFootY(_standing);
            Restart();
        }
        public void Restart()
        {
            _effect.Begin(_settings, new Vector3(0f, _startHeight, 0f), Vector3.zero, Vector2.up, 17);
            _effect.SetPresentationCamera(_camera);
        }
        public void Sample(float frame)
        {
            if (!_effect.IsPlaying) Restart();
            float standingFrame = _step.startupFrames + _step.activeFrames + MainLightningPlungeAuthoring.LandingHoldFrames + 1;
            _actor.enabled = frame >= _step.ActivePhaseStart;
            _actor.sprite = frame < standingFrame ? _landed : _standing;
            _actor.transform.position = Vector3.up * (frame < standingFrame ? _landedOffset : _standingOffset);
            float progress = Mathf.Clamp01(frame / Mathf.Max(1, _step.startupFrames));
            _camera.transform.position = new Vector3(0f, Mathf.Max(_startHeight * (1f - progress * progress * progress) + 1f, 4.4f), -10f);
            _effect.Sample(frame, _step.ActivePhaseStart, EndFrame);
            _camera.Render();
        }
        public void Save(string path, bool log = true)
        {
            if (log) Debug.Log($"Plunge phase capture: frame={_effect.SampledFrame}, sparks={_effect.impactSparks.particleCount}, debris={_effect.debris.particleCount}, residual={_effect.residualSparks.particleCount}");
            var previous = RenderTexture.active;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            try { RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        }
        private GameObject Host(string name) { var host = new GameObject(name); SceneManager.MoveGameObjectToScene(host, _scene); return host; }
        private static Sprite FirstSprite(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                if (binding.type == typeof(SpriteRenderer)) return AnimationUtility.GetObjectReferenceCurve(clip, binding)[0].value as Sprite;
            return null;
        }
        public void Dispose()
        {
            _effect.Stop();
            _camera.targetTexture = null;
            EditorSceneManager.ClosePreviewScene(_scene);
            texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(_actorMaterial);
            UnityEngine.Object.DestroyImmediate(_groundMaterial);
            UnityEngine.Object.DestroyImmediate(_floorSprite);
        }
    }
}
#endif
