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
    private CharacterObject[] _windowTeam;
    private int _windowTeamCount;
    private int _sourceFieldSession;
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
            _team.OnTeamChanged -= HandleTeamChanged;
            _team.OnCurrentCharacterChanged -= HandleCurrentCharacterChanged;
            _team.OnCharacterHealthUpdated -= HandleHealth;
            _team.OnTeamChanged += HandleTeamChanged;
            _team.OnCurrentCharacterChanged += HandleCurrentCharacterChanged;
            _team.OnCharacterHealthUpdated += HandleHealth;
        }
    }

    private void OnDisable()
    {
        CombatEventHub.Shared.DamageResolved -= HandleDamage;
        SceneManager.activeSceneChanged -= HandleSceneChanged;
        if (_team != null)
        {
            _team.OnTeamChanged -= HandleTeamChanged;
            _team.OnCurrentCharacterChanged -= HandleCurrentCharacterChanged;
            _team.OnCharacterHealthUpdated -= HandleHealth;
        }
        Clear();
    }

    private void HandleSceneChanged(Scene previous, Scene next) => Clear();
    private void HandleCurrentCharacterChanged()
    {
        if (_source != null && (_team.CurrentCharacter != _source
            || _team.playerCharacter?.Combat?.FieldSession != _sourceFieldSession)) Clear();
    }

    private void HandleTeamChanged()
    {
        if (_source == null) return;
        // TeamChanged also reports HP/stat/buff refreshes; only roster changes invalidate readiness.
        if (_windowTeam == null || _team.CharacterCount != _windowTeamCount) { Clear(); return; }
        for (int i = 0; i < _windowTeam.Length; i++)
            if (_team.GetCharacterAtIndex(i) != _windowTeam[i]) { Clear(); return; }
        HandleCurrentCharacterChanged();
    }
    private void HandleHealth(CharacterObject character, float current, float maximum)
    {
        if (character == _source && current <= 0f) Clear();
    }

    public void Clear()
    {
        _source = null;
        _target = null;
        _expiresAt = float.NegativeInfinity;
        _windowTeam = null;
        _windowTeamCount = 0;
    }

    public bool TryGetWindow(CharacterObject incoming, out float remaining, out float total)
    {
        total = windowSeconds;
        remaining = Mathf.Max(0f, _expiresAt - Time.time);
        return isActiveAndEnabled && _team != null && _source != null && _team.CurrentCharacter == _source
            && _team.playerCharacter?.Combat?.FieldSession == _sourceFieldSession
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
        if (_source != hit.sourceCharacter || _windowTeam == null)
        {
            _windowTeam = _team.GetTeamSnapshot();
            _windowTeamCount = _team.CharacterCount;
        }
        _source = hit.sourceCharacter;
        _sourceFieldSession = execution.fieldSession;
        _target = hit.target;
        _expiresAt = Time.time + Mathf.Max(0f, windowSeconds);
        if (!wasReady && profile.readySfx != null && _audio != null) _audio.PlayOneShot(profile.readySfx);
    }

    public bool CanHandoffTo(int index)
    {
        return TryPrepareEntry(index, out _);
    }

    public bool CanUsePreparedTransition()
    {
        return _team?.playerCharacter?.StateMachine?.CanSwitchCharacter(ignoreCooldown: true) == true;
    }

    public bool TryPrepare(int index, out CombatAction action, out AttackStepData step,
        out EnemyController target, out Vector2 position, out int facing)
    {
        action = null;
        step = null;
        target = null;
        position = default;
        facing = 1;
        if (!TryPrepareEntry(index, out QtePreparedEntry entry)) return false;
        action = entry.action;
        step = entry.step;
        target = entry.target;
        position = entry.placement.startBodyPosition;
        facing = entry.placement.landingFacing;
        return true;
    }

    public bool TryPrepareEntry(int index, out QtePreparedEntry entry)
    {
        entry = default;
        if (_team == null || !_team.CanSwitchToIndex(index, ignoreCooldown: true)
            || !TryGetWindow(_team.GetCharacterAtIndex(index), out _, out _) || !CanUsePreparedTransition()) return false;
        CharacterObject incoming = _team.GetCharacterAtIndex(index);
        if (!incoming.qteSwap.TryResolveIntro(incoming.actionResolverTable, out CombatAction action, out AttackStepData step)) return false;
        PlayerCharacter player = _team.playerCharacter;
        if (player.Movement == null
            || !TryResolvePreparedTarget(incoming, action, out EnemyController target, out QteEntryPlacement placement)) return false;
        entry = new QtePreparedEntry(action, step, target, incoming.qteSwap.entryMode, placement);
        return true;
    }

    private bool TryPlaceForTarget(CharacterObject incoming, EnemyController target, out QteEntryPlacement placement)
        => TryPlaceAtPosition(incoming, target.transform.position, out placement);

    private bool TryPlaceAtPosition(CharacterObject incoming, Vector2 targetPosition, out QteEntryPlacement placement)
    {
        placement = default;
        PlayerCharacter player = _team.playerCharacter;
        if (incoming.qteSwap.entryMode == QteEntryMode.LightningPlunge)
        {
            QtePlungeSettings plunge = incoming.qteSwap.plunge;
            if (plunge == null || plunge.prefab == null || !player.Movement.TryFindQtePlungePlacement(
                targetPosition, plunge.targetStartOffset, player.Movement.FacingDirection, out placement)) return false;
            if (Mathf.Abs(targetPosition.x - placement.landingBodyPosition.x) <= 0.001f)
                placement = new QteEntryPlacement(placement.startBodyPosition, placement.landingBodyPosition,
                    placement.surfacePoint, placement.surfaceNormal, placement.capturedEnemyPosition,
                    placement.support, placement.approachFacing, player.Movement.FacingDirection);
        }
        else
        {
            if (!TryPlace(incoming, out Vector2 position)) return false;
            float delta = targetPosition.x - position.x;
            int facing = Mathf.Abs(delta) > 0.001f ? (delta > 0f ? 1 : -1) : player.Movement.FacingDirection;
            placement = new QteEntryPlacement(position, position, position, Vector2.up,
                targetPosition, null, player.Movement.FacingDirection, facing);
        }
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

    private bool TryResolvePreparedTarget(CharacterObject incoming, CombatAction action,
        out EnemyController target, out QteEntryPlacement placement)
    {
        target = null;
        placement = default;
        PlayerCharacter player = _team.playerCharacter;
        float radius = action.GetTargetSearchRadius();
        bool hasLiveTarget = IsValidTarget(_target, player.transform.position, radius);
        if (hasLiveTarget
            && TryPlaceForTarget(incoming, _target, out placement))
        {
            target = _target;
            return true;
        }
        // This query also drives portrait feedback, so avoid the allocating combat convenience query.
        var filter = new ContactFilter2D();
        filter.SetLayerMask(player.Combat.enemyLayerMask);
        filter.useTriggers = true;
        int count = Physics2D.OverlapCircle(player.transform.position, radius, filter, _targetHits);
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            EnemyController candidate = _targetHits[i] != null ? _targetHits[i].GetComponentInParent<EnemyController>() : null;
            if (!IsValidTarget(candidate, player.transform.position, radius)) continue;
            hasLiveTarget = true;
            Vector2 delta = candidate.transform.position - player.transform.position;
            float score = delta.sqrMagnitude;
            if (delta.x * player.Movement.FacingDirection < 0f) score += radius * radius;
            if (score >= bestScore) continue;
            // A nearer enemy with no safe entry must not hide another usable QTE target.
            if (!TryPlaceForTarget(incoming, candidate, out QteEntryPlacement candidatePlacement)) continue;
            bestScore = score;
            target = candidate;
            placement = candidatePlacement;
        }
        if (target != null) return true;
        // Readiness belongs to the confirmed hit, so losing its victim must not cancel the intro.
        return !hasLiveTarget && TryPlaceAtPosition(incoming, player.transform.position, out placement);
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
