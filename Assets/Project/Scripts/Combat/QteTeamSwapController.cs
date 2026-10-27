using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class QteTeamSwapController : MonoBehaviour
{
    [Header("QTE Window")]
    [Min(0f)] public float windowSeconds = 5f;
    public bool reduceUiMotion;

    [Header("Intro Handoff Slow Motion")]
    [Tooltip("Starting value: strong slowdown as the new character and camera take over. Set duration to zero to disable.")]
    [Range(0.01f, 0.99f)] public float introTimeScale = 0.08f;
    [Tooltip("Duration in real seconds, independent of combat time.")]
    [Min(0f)] public float introSlowMotionSeconds = 0.24f;

    [Header("Intro Camera Impact")]
    [Min(0f)] public float introCameraSizeReduction = 1f;
    [Tooltip("Rendered frames for an instant camera-size hit followed by a smooth return. Starting value: 60.")]
    [Range(1, 120)] public int introCameraImpactFrames = 60;

    private TeamLoader _team;
    private CharacterObject _source;
    private EnemyController _target;
    private float _expiresAt = float.NegativeInfinity;
    private AudioSource _audio;
    private readonly Collider2D[] _targetHits = new Collider2D[64];

    public static QteTeamSwapController For(TeamLoader team) => team != null ? team.QteSwaps : null;

    private void Awake()
    {
        _team = GetComponent<TeamLoader>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
    }

    private void OnEnable()
    {
        CombatEventHub.Shared.DamageResolved -= HandleDamage;
        CombatEventHub.Shared.DamageResolved += HandleDamage;
        SceneManager.activeSceneChanged += HandleSceneChanged;
        if (_team != null)
        {
            _team.OnTeamChanged -= Clear;
            _team.OnCurrentCharacterChanged -= Clear;
            _team.OnCharacterHealthUpdated -= HandleHealth;
            _team.OnTeamChanged += Clear;
            _team.OnCurrentCharacterChanged += Clear;
            _team.OnCharacterHealthUpdated += HandleHealth;
        }
    }

    private void OnDisable()
    {
        CombatEventHub.Shared.DamageResolved -= HandleDamage;
        SceneManager.activeSceneChanged -= HandleSceneChanged;
        if (_team != null)
        {
            _team.OnTeamChanged -= Clear;
            _team.OnCurrentCharacterChanged -= Clear;
            _team.OnCharacterHealthUpdated -= HandleHealth;
        }
        Clear();
    }

    private void HandleSceneChanged(Scene previous, Scene next) => Clear();
    private void HandleHealth(CharacterObject character, float current, float maximum)
    {
        if (character == _source && current <= 0f) Clear();
    }

    public void Clear()
    {
        _source = null;
        _target = null;
        _expiresAt = float.NegativeInfinity;
    }

    public bool TryGetWindow(CharacterObject incoming, out float remaining, out float total)
    {
        total = windowSeconds;
        remaining = Mathf.Max(0f, _expiresAt - Time.time);
        return isActiveAndEnabled && _team != null && _source != null && _team.CurrentCharacter == _source
            && incoming != null && incoming != _source && incoming.qteSwap != null && incoming.qteSwap.enabled
            && remaining > 0f && _team.ContainsCharacter(incoming) && _team.IsCharacterAlive(incoming)
            && _team.IsCharacterAlive(_source);
    }

    private void HandleDamage(DamageResolvedEvent hit)
    {
        CombatExecutionContext execution = hit.execution;
        if (_team == null || execution == null || !execution.IsOnField || execution.isQteIntro
            || execution.owner != _team.playerCharacter?.Combat
            || hit.sourceCharacter != _team.CurrentCharacter || hit.damageDealt <= 0f
            || hit.descriptor.delivery != DamageDelivery.Primary || hit.provenance.tags != CombatDamageTag.None)
            return;

        execution.ConfirmHit();
        QteSwapDefinition profile = hit.sourceCharacter.qteSwap;
        if (profile == null || !profile.MatchesTrigger(hit.actionId)) return;
        bool wasReady = _source == hit.sourceCharacter && Time.time < _expiresAt;
        _source = hit.sourceCharacter;
        _target = hit.target;
        _expiresAt = Time.time + Mathf.Max(0f, windowSeconds);
        if (!wasReady && profile.readySfx != null && _audio != null) _audio.PlayOneShot(profile.readySfx);
    }

    public bool CanHandoffTo(int index)
    {
        if (_team == null || !_team.CanSwitchToIndex(index)) return false;
        if (!TryGetWindow(_team.GetCharacterAtIndex(index), out _, out _) || !CanUsePreparedTransition()) return false;
        CharacterObject incoming = _team.GetCharacterAtIndex(index);
        if (!incoming.qteSwap.TryResolveIntro(incoming.actionResolverTable, out CombatAction action, out _)) return false;
        PlayerCharacter player = _team.playerCharacter;
        EnemyController target = ResolveTarget(action);
        if (target == null || player.Movement == null) return false;
        return TryPlace(incoming, out _);
    }

    public bool CanUsePreparedTransition()
    {
        PlayerCharacter player = _team != null ? _team.playerCharacter : null;
        PlayerStateMachine fsm = player != null ? player.StateMachine : null;
        if (player == null || player.IsDead || fsm == null || !fsm.IsGroundedContext
            || Project.UI.InputLockManager.IsLocked || Time.timeScale <= 0f || _team.IsSwitchOnCooldown
            || player.Combat?.HasGrabbedEnemy() == true) return false;
        if (fsm.CurrentStateId == PlayerStateId.Idle || fsm.CurrentStateId == PlayerStateId.Running) return true;
        if (fsm.CurrentStateId != PlayerStateId.Attacking && fsm.CurrentStateId != PlayerStateId.Skill
            && fsm.CurrentStateId != PlayerStateId.Ultimate && fsm.CurrentStateId != PlayerStateId.QteIntro) return false;
        AttackStepData step = player.Combat?.GetCurrentStepData();
        return step != null && !step.autoChainToNext && player.Combat.CurrentExecution?.HasConfirmedHit == true;
    }

    public bool TryPrepare(int index, out CombatAction action, out AttackStepData step,
        out EnemyController target, out Vector2 position, out int facing)
    {
        action = null;
        step = null;
        target = null;
        position = default;
        facing = 1;
        if (!CanHandoffTo(index)) return false;
        CharacterObject incoming = _team.GetCharacterAtIndex(index);
        if (!incoming.qteSwap.TryResolveIntro(incoming.actionResolverTable, out action, out step)) return false;
        PlayerCharacter player = _team.playerCharacter;
        target = ResolveTarget(action);
        if (target == null || player.Movement == null) return false;
        if (!TryPlace(incoming, out position)) return false;
        float delta = target.transform.position.x - position.x;
        facing = Mathf.Abs(delta) > 0.001f ? (delta > 0f ? 1 : -1) : player.Movement.FacingDirection;
        return true;
    }

    public void PlayIntroHandoff()
    {
        if (Application.isPlaying && _team != null && _team.playerCharacter != null)
        {
            CameraManager.instance?.ResetLookAheadForCharacterSwap(_team.playerCharacter.Movement?.RB, introSlowMotionSeconds);
            CameraManager.instance?.PlayQteIntroImpact(introCameraSizeReduction, introCameraImpactFrames);
        }
        if (Application.isPlaying && introSlowMotionSeconds > 0f)
            GameFreezeManager.Instance?.PlaySlowMotion(introTimeScale, introSlowMotionSeconds);
    }

    private EnemyController ResolveTarget(CombatAction action)
    {
        PlayerCharacter player = _team.playerCharacter;
        float radius = action.GetTargetSearchRadius();
        if (IsValidTarget(_target, player.transform.position, radius)) return _target;
        // This query also drives portrait feedback, so avoid the allocating combat convenience query.
        var filter = new ContactFilter2D();
        filter.SetLayerMask(player.Combat.enemyLayerMask);
        filter.useTriggers = true;
        int count = Physics2D.OverlapCircle(player.transform.position, radius, filter, _targetHits);
        EnemyController nearest = null;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            EnemyController candidate = _targetHits[i] != null ? _targetHits[i].GetComponentInParent<EnemyController>() : null;
            if (!IsValidTarget(candidate, player.transform.position, radius)) continue;
            Vector2 delta = candidate.transform.position - player.transform.position;
            float score = delta.sqrMagnitude;
            if (delta.x * player.Movement.FacingDirection < 0f) score += radius * radius;
            if (score >= bestScore) continue;
            bestScore = score;
            nearest = candidate;
        }
        return nearest;
    }

    private bool TryPlace(CharacterObject incoming, out Vector2 position)
    {
        PlayerCharacter player = _team.playerCharacter;
        float desiredX = player.transform.position.x - player.Movement.FacingDirection * incoming.qteSwap.entryOffset;
        return player.Movement.TryFindQteGroundPosition(desiredX, out position);
    }

    private static bool IsValidTarget(EnemyController target, Vector2 playerPosition, float radius)
    {
        return target != null && target.IsAlive && target.IsTargetable
            && ((Vector2)target.transform.position - playerPosition).sqrMagnitude <= radius * radius;
    }
}
