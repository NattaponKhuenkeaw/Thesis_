using UnityEngine;
using CombatCore;
using System.Collections;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public SpriteRenderer spriteRenderer;
    [SerializeField] private MonoBehaviour combatHandler;

    // =========================================================
    // Movement Profile (data-driven, assigned by PlayerCharacter)
    // =========================================================
    [HideInInspector] public MovementProfile profile;

    /// <summary>Safe profile accessor with fallback defaults.</summary>
    private MovementProfile P => profile != null ? profile : FallbackProfile;
    private static MovementProfile _fallback;
    private static MovementProfile FallbackProfile =>
        _fallback != null ? _fallback : (_fallback = ScriptableObject.CreateInstance<MovementProfile>());

    // =========================================================
    // Dash Settings
    // =========================================================
    [Header("Dash Settings")]
    [HideInInspector] public DashData dashData;

    // =========================================================
    // Dash Visuals
    // =========================================================
    [Header("Dash Visuals")]
    public bool enableDashAfterimage = true;
    public Color dashAfterimageColor = new Color(0.1f, 0.5f, 1f, 0.8f);
    public float dashAfterimageLifetime = 0.3f;
    public float dashAfterimageSpawnInterval = 0.05f;

    // =========================================================
    // Layer Settings
    // =========================================================
    [Header("Layer Settings")]
    public string playerLayerName = "Player";
    public string enemyLayerName = "Enemy";
    public string oneWayPlatformLayerName = "OneWayPlatform";

    // =========================================================
    // Physics Checks
    // =========================================================
    [Header("Physics Checks")]
    public Transform groundCheck;
    public LayerMask groundLayer;

    // =========================================================
    // Private Runtime Data
    // =========================================================
    private Rigidbody2D rb;
    private PlayerStateMachine fsm;
    private ICharacterCombat combat;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isHoldingDown;
    private bool jumpHeld;
    private float originalGravityScale;
    private float horizontalInput;
    private float jumpBufferedUntil = -999f;
    private float coyoteUntil = -999f;
    private int jumpsUsed;
    private int airDashesUsed;

    // GC-free ground check (Optimization 1)
    private Collider2D[] groundHitBuffer = new Collider2D[5];
    private ContactFilter2D groundContactFilter;

    // Float timers replacing coroutines (Optimization 3)
    private float landingEndTime;
    private float jumpStartupEndTime;
    private float lastAfterimageTime;
    private float lastFacingChangeTime = -999f;

    // Frame-data driven dash
    private int dashCooldownFramesRemaining;
    private int currentDashFrame = 0;
    private float dashLogicTimeCounter = 0f;
    private float dashDir = 1f;

    // Attack motion
    private float attackMotionVelocityX;
    private float attackMotionVelocityY;
    private Coroutine attackMotionCoroutine;
    private Coroutine plungeAfterimageCoroutine;
    private bool isAttackMotionActive;
    private bool useUnscaledAttackMotion;

    // Drop-down
    private Coroutine dropDownCoroutine;
    private Collider2D currentGroundCollider;
    private Collider2D ignoredGroundCollider;
    private Collider2D[] solidColliders;

    // Post-attack gravity
    private float postAttackGraceTimer;
    private bool wasLockingAirCombo;
    private bool pendingGroundJumpTakeoff;
    private float pendingGroundJumpRefundAt;

    // Layer caches
    private int playerLayer;
    private int enemyLayer;
    private int oneWayPlatformLayer;

    // Combat cooldown tracking
    private bool attackFacingLockOnCancelledForCurrentAttack;
    private bool wasAttackLockOnActive;
    private bool hasPerformedInitialLockOnForCurrentStep;
    private int lastObservedComboStep = -1;

    [HideInInspector] public int maxJumpOverride = -1;
    [HideInInspector] public int maxAirDashOverride = -1;

    private int MaxJumps => maxJumpOverride >= 0 ? maxJumpOverride : Mathf.Max(1, P.maxJumpCount);
    private int MaxAirDashes => maxAirDashOverride >= 0 ? maxAirDashOverride : Mathf.Max(0, P.maxAirDashCount);

    // =========================================================
    // Computed State
    // =========================================================
    public bool IsDashing => fsm != null && fsm.CurrentStateId == PlayerStateId.Dashing;
    public bool IsJumping => Time.time < jumpStartupEndTime;
    public bool IsDroppingDown => dropDownCoroutine != null;
    public bool IsLanding => Time.time < landingEndTime;
    public float HorizontalInput => horizontalInput;
    public float VerticalVelocity => rb != null ? rb.linearVelocity.y : 0f;
    public int FacingDirection => transform.eulerAngles.y > 90f ? -1 : 1;

    // =========================================================
    // Events
    // =========================================================
    public event Action OnJumpStart;
    public event Action OnDoubleJump;
    public event Action OnDashStart;
    public event Action OnDashComplete;
    public event Action OnLand;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        fsm = GetComponent<PlayerStateMachine>();
        combat = combatHandler as ICharacterCombat;
        if (combat == null) combat = GetComponent<ICharacterCombat>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        originalGravityScale = rb.gravityScale;
        playerLayer = LayerMask.NameToLayer(playerLayerName);
        enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        oneWayPlatformLayer = LayerMask.NameToLayer(oneWayPlatformLayerName);
        solidColliders = Array.FindAll(GetComponentsInChildren<Collider2D>(), c => c != null && !c.isTrigger);
        SetPlayerEnemyCollisionIgnored(true);

        int cachedGroundMask = GetGroundCheckMask();
        groundContactFilter = new ContactFilter2D();
        groundContactFilter.SetLayerMask(cachedGroundMask);
        groundContactFilter.useLayerMask = true;
        groundContactFilter.useTriggers = false;
    }

    void FixedUpdate()
    {
        UpdateFacing();
        CheckGround();
        ResolvePendingGroundJumpTakeoff();
        TryConsumeBufferedJump();

        if (dashCooldownFramesRemaining > 0) dashCooldownFramesRemaining--;

        if (IsDashing) ProcessDashFrame();
        HandleVerticalPhysics();
        ApplyHorizontalMovement();
    }

    // =========================================================
    // Public API
    // =========================================================
    public void SetMoveInput(Vector2 input) => horizontalInput = Mathf.Clamp(input.x, -1f, 1f);
    
    [Obsolete("Use SetMoveInput instead")]
    public void Move(Vector2 input) => SetMoveInput(input);

    public void Jump()
    {
        if (IsDashing && !IsInDashRecovery()) return;
        if (isGrounded || Time.time <= coyoteUntil) landingEndTime = -999f;
        jumpBufferedUntil = Time.time + P.jumpBufferTime;
        TryConsumeBufferedJump();
    }

    public void Dash(float inputX)
    {
        if (CanStartGroundDash()) { StopAttackMotion(); StartDash(inputX); return; }
        if (CanStartAirDash()) { StopAttackMotion(); airDashesUsed++; combat?.ResetAirComboCooldown(); StartDash(inputX); }
    }

    private void StartDash(float inputX)
    {
        OnDashStart?.Invoke();
        dashDir = GetDashDirection(inputX);
        currentDashFrame = 0;
        dashLogicTimeCounter = 0f;
        lastAfterimageTime = -dashAfterimageSpawnInterval;
    }

    public void SetDownInput(bool isPressed) => isHoldingDown = isPressed;
    public bool IsDownInputHeld() => isHoldingDown;
    public void SetJumpHeld(bool isPressed) => jumpHeld = isPressed;
    public bool IsGrounded() => isGrounded;

    public bool IsGroundedForActionSelection()
    {
        if (IsJumping || rb.linearVelocity.y > P.airborneActionVelocityThreshold) return false;
        if (isGrounded) return true;
        return rb.linearVelocity.y <= 0f && IsNearGroundForActionSelection();
    }

    public bool IsAgainstWall()
    {
        float dir = FacingDirection;
        Vector2 originLower = rb.position + Vector2.up * 0.5f;
        Vector2 originUpper = rb.position + Vector2.up * 1.5f;
        Vector2 direction = Vector2.right * dir;
        LayerMask wallMask = groundLayer;
        if (playerLayer != -1) wallMask &= ~(1 << playerLayer);
        if (enemyLayer != -1) wallMask &= ~(1 << enemyLayer);
        return Physics2D.Raycast(originLower, direction, 1.2f, wallMask) || Physics2D.Raycast(originUpper, direction, 1.2f, wallMask);
    }

    public bool TryDropDown()
    {
        if (IsDroppingDown) return false;
        if (!CanDropDown()) RefreshGroundStateForDropDown();
        if (!CanDropDown()) return false;
        coyoteUntil = -999f;
        jumpBufferedUntil = -999f;
        dropDownCoroutine = StartCoroutine(DropDownRoutine(currentGroundCollider));
        return true;
    }

    public void ForceFaceDirectionFromInput(Vector2 input)
    {
        if (Mathf.Abs(input.x) < 0.1f) return;
        SetFacingDirection(input.x > 0 ? 1 : -1, false);
    }

    public void StartPlungeMotion(float downwardSpeed)
    {
        StopAttackMotion();
        float speed = downwardSpeed > 0 ? downwardSpeed : P.jumpForce;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -speed);
        if (enableDashAfterimage) plungeAfterimageCoroutine = StartCoroutine(PlungeAfterimageRoutine());
    }

    public void StartDashCooldown() => dashCooldownFramesRemaining = dashData != null ? dashData.cooldownFrames : 0;

    public void ApplyAttackMovement(MotionKeyframe keyframe)
    {
        StopAttackMotion();
        if (keyframe != null) attackMotionCoroutine = StartCoroutine(AttackMovementCoroutine(keyframe));
    }

    public void SetUseUnscaledAttackMotion(bool useUnscaled) => useUnscaledAttackMotion = useUnscaled;
    public void CancelAttackMotion() => StopAttackMotion();

    public bool CanDash() => CanStartGroundDash() || CanStartAirDash();
    public bool CanJump() => !IsJumping && (IsGroundJumpAvailable() || CanUseAirJump()) && !(IsDashing && !IsInDashRecovery());

    // =========================================================
    // Private Logic
    // =========================================================
    private void CheckGround()
    {
        int hitCount = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundContactFilter, groundHitBuffer);
        currentGroundCollider = GetBestGroundCollider(groundHitBuffer, hitCount);
        isGrounded = currentGroundCollider != null;
        if (isGrounded) coyoteUntil = Time.time + P.coyoteTime;
        if (isGrounded && !wasGrounded) { jumpsUsed = 0; airDashesUsed = 0; combat?.ResetAirComboCooldown(); OnLand?.Invoke(); if (!ShouldSuppressLandingFeedback()) landingEndTime = Time.time + P.landingLagTime; }
        wasGrounded = isGrounded;
    }

    private int GetGroundCheckMask() => groundLayer.value | (oneWayPlatformLayer >= 0 ? 1 << oneWayPlatformLayer : 0);

    private Collider2D GetBestGroundCollider(Collider2D[] hits, int hitCount)
    {
        Collider2D best = null; float bestTop = float.MinValue; bool bestOneWay = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D h = hits[i]; if (h == null || h.isTrigger || (ignoredGroundCollider != null && h == ignoredGroundCollider)) continue;
            float top = h.bounds.max.y; bool oneWay = oneWayPlatformLayer >= 0 && h.gameObject.layer == oneWayPlatformLayer;
            if (best == null || top > bestTop + 0.001f || (Mathf.Abs(top - bestTop) <= 0.02f && oneWay && !bestOneWay)) { best = h; bestTop = top; bestOneWay = oneWay; }
        }
        return best;
    }

    private void TryConsumeBufferedJump()
    {
        if (Time.time > jumpBufferedUntil || IsDashing || IsJumping || !CanStartBufferedJump()) return;
        StopAttackMotion(); ConsumeJump(); jumpBufferedUntil = -999f; StartCoroutine(JumpCoroutine());
    }

    private bool CanStartBufferedJump() => IsGroundJumpAvailable() || CanUseAirJump();
    private bool IsGroundJumpAvailable() => jumpsUsed == 0 && (isGrounded || Time.time <= coyoteUntil);
    private bool CanUseAirJump() => jumpsUsed < MaxJumps;

    private void ConsumeJump()
    {
        if (IsGroundJumpAvailable()) { jumpsUsed = 1; pendingGroundJumpTakeoff = true; pendingGroundJumpRefundAt = Time.time + Mathf.Max(0.05f, P.jumpStartupTime + (Time.fixedDeltaTime * 2f)); }
        else { jumpsUsed++; pendingGroundJumpTakeoff = false; OnDoubleJump?.Invoke(); }
        coyoteUntil = -999f;
    }

    private void ResolvePendingGroundJumpTakeoff()
    {
        if (!pendingGroundJumpTakeoff || !isGrounded || Time.time < pendingGroundJumpRefundAt) return;
        jumpsUsed = 0; pendingGroundJumpTakeoff = false; coyoteUntil = Time.time + P.coyoteTime;
    }

    private void HandleVerticalPhysics()
    {
        if (IsDashing || isAttackMotionActive) return;
        if (combat != null && combat.ShouldLockAirComboInPlace()) { rb.gravityScale = 0f; rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); wasLockingAirCombo = true; postAttackGraceTimer = 0f; return; }
        if (wasLockingAirCombo) { postAttackGraceTimer = P.postAttackGravityGraceDuration; wasLockingAirCombo = false; }
        float targetG = originalGravityScale;
        if (isGrounded) { rb.gravityScale = targetG; postAttackGraceTimer = 0f; return; }
        if (postAttackGraceTimer > 0f) { targetG *= (1f - (postAttackGraceTimer / Mathf.Max(0.001f, P.postAttackGravityGraceDuration))); postAttackGraceTimer -= Time.fixedDeltaTime; }
        if (rb.linearVelocity.y > P.apexVelocityThreshold) targetG *= jumpHeld ? P.riseGravityMultiplier : P.lowJumpGravityMultiplier;
        else if (isHoldingDown && rb.linearVelocity.y < 0) targetG *= P.fastFallMultiplier;
        else if (rb.linearVelocity.y < -P.apexVelocityThreshold) targetG *= P.fallGravityMultiplier;
        else targetG *= P.apexGravityMultiplier;
        rb.gravityScale = targetG;
        if (rb.linearVelocity.y < -P.maxFallSpeed) rb.linearVelocity = new Vector2(rb.linearVelocity.x, -P.maxFallSpeed);
    }

    private void SetPlayerEnemyCollisionIgnored(bool ignored) { if (playerLayer != -1 && enemyLayer != -1) Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, ignored); }

    private void StopAttackMotion()
    {
        if (attackMotionCoroutine != null) { StopCoroutine(attackMotionCoroutine); attackMotionCoroutine = null; }
        if (plungeAfterimageCoroutine != null) { StopCoroutine(plungeAfterimageCoroutine); plungeAfterimageCoroutine = null; }
        isAttackMotionActive = false; attackMotionVelocityX = 0f; attackMotionVelocityY = 0f; useUnscaledAttackMotion = false;
    }

    private bool CanStartGroundDash() => dashCooldownFramesRemaining <= 0 && !IsJumping && !IsLanding && (isGrounded || Time.time <= coyoteUntil);
    private bool CanStartAirDash() => dashCooldownFramesRemaining <= 0 && !IsJumping && !IsLanding && !isGrounded && Time.time > coyoteUntil && airDashesUsed < MaxAirDashes;
    private bool CanDropDown() => isGrounded && currentGroundCollider != null && oneWayPlatformLayer >= 0 && currentGroundCollider.gameObject.layer == oneWayPlatformLayer;
    private void RefreshGroundStateForDropDown() { int hitCount = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundContactFilter, groundHitBuffer); currentGroundCollider = GetBestGroundCollider(groundHitBuffer, hitCount); isGrounded = currentGroundCollider != null; }
    private bool ShouldSuppressLandingFeedback() => combat != null && combat.ShouldSuppressLandingFeedback();

    private bool IsNearGroundForActionSelection()
    {
        if (groundCheck == null || P.groundedActionProbeDistance <= 0f) return false;
        RaycastHit2D hit = Physics2D.Raycast(groundCheck.position, Vector2.down, P.groundedActionProbeDistance, GetGroundCheckMask());
        return hit.collider != null && !hit.collider.isTrigger && (ignoredGroundCollider == null || hit.collider != ignoredGroundCollider);
    }

    private float GetDashDirection(float inputX) => Mathf.Abs(inputX) > 0.1f ? (inputX > 0 ? 1f : -1f) : FacingDirection;

    private bool IsMovementLocked()
    {
        if (fsm == null || fsm.CurrentState == null) return false;
        if (fsm.CurrentState.LocksMovement) { if (fsm.CurrentStateId == PlayerStateId.Attacking && combat != null && combat.CanMoveAfterAttack()) return false; return true; }
        return IsLanding;
    }

    private void ApplyHorizontalMovement()
    {
        if (rb == null || IsDashing) return;
        if (isAttackMotionActive) { rb.linearVelocity = new Vector2(attackMotionVelocityX, attackMotionVelocityY); return; }
        if (IsMovementLocked()) { rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }
        float targetX = horizontalInput * P.moveSpeed * (isGrounded ? 1f : P.airSpeedMultiplier);
        float accel = Mathf.Abs(targetX) < 0.01f ? (isGrounded ? P.groundDeceleration : P.airDeceleration) : (isGrounded ? P.groundAcceleration : P.airAcceleration);
        if (Mathf.Abs(rb.linearVelocity.x) > 0.01f && Mathf.Sign(targetX) != Mathf.Sign(rb.linearVelocity.x) && !isGrounded) accel *= 1.15f;
        rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, targetX, accel * Time.fixedDeltaTime), rb.linearVelocity.y);
    }

    private void UpdateFacing()
    {
        UpdateAttackFacingLockOnState();
        if (TryHandleAttackFacingLockOn()) return;
        bool canFace = fsm != null && (fsm.CurrentState is FrameDataPlayerState fd ? fd.AllowsFacing() : !fsm.CurrentState.LocksFacing);
        if (canFace) { if (horizontalInput > 0.01f) SetFacingDirection(1, true); else if (horizontalInput < -0.01f) SetFacingDirection(-1, true); }
    }

    private void UpdateAttackFacingLockOnState()
    {
        bool attacking = combat != null && combat.IsAttacking();
        int step = combat != null ? combat.GetComboStep() : -1;
        if (!attacking) { attackFacingLockOnCancelledForCurrentAttack = false; wasAttackLockOnActive = false; hasPerformedInitialLockOnForCurrentStep = false; lastObservedComboStep = -1; return; }
        if (!wasAttackLockOnActive || step != lastObservedComboStep) { attackFacingLockOnCancelledForCurrentAttack = false; hasPerformedInitialLockOnForCurrentStep = combat != null && combat.IsExecutingAutoChainStep; }
        if (Mathf.Abs(horizontalInput) >= P.attackFacingCancelInputThreshold) attackFacingLockOnCancelledForCurrentAttack = true;
        wasAttackLockOnActive = true; lastObservedComboStep = step;
    }

    private bool TryHandleAttackFacingLockOn()
    {
        if (hasPerformedInitialLockOnForCurrentStep || (combat != null && combat.IsExecutingAutoChainStep)) return false;
        if (!P.enableAttackFacingLockOn || attackFacingLockOnCancelledForCurrentAttack || combat == null || !combat.IsAttacking() || CameraManager.instance == null || Mathf.Abs(horizontalInput) >= P.attackFacingCancelInputThreshold) return false;
        EnemyController target = CameraManager.instance.GetNearestEngagedEnemy(transform.position);
        if (target == null) return false;
        float dx = target.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= 0.05f) return false;
        SetFacingDirection(dx >= 0f ? 1 : -1, false); hasPerformedInitialLockOnForCurrentStep = true; return true;
    }

    private void SetFacingDirection(int dir, bool respectCD)
    {
        if ((dir == 1 || dir == -1) && FacingDirection != dir && (!respectCD || Time.time - lastFacingChangeTime >= 0.2f))
        {
            transform.eulerAngles = new Vector3(0, dir == 1 ? 0 : 180, 0);
            lastFacingChangeTime = Time.time;
        }
    }

    private IEnumerator JumpCoroutine()
    {
        jumpStartupEndTime = Time.time + P.jumpStartupTime; OnJumpStart?.Invoke();
        yield return new WaitForSeconds(P.jumpStartupTime);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); rb.AddForce(Vector2.up * P.jumpForce, ForceMode2D.Impulse);
    }

    private void ProcessDashFrame()
    {
        if (dashData == null) return;
        dashLogicTimeCounter += Time.fixedDeltaTime; int target = Mathf.RoundToInt(dashLogicTimeCounter * FrameDataConstants.LOGIC_FPS);
        while (currentDashFrame < target) { currentDashFrame++; if (currentDashFrame == dashData.startupFrames + dashData.activeFrames + 1) StartDashCooldown(); if (currentDashFrame >= dashData.startupFrames + dashData.activeFrames + dashData.recoveryFrames) { EndDash(); return; } }
        rb.gravityScale = 0f;
        if (currentDashFrame <= dashData.startupFrames) rb.linearVelocity = Vector2.zero;
        else if (currentDashFrame <= dashData.startupFrames + dashData.activeFrames) { float t = (float)(currentDashFrame - dashData.startupFrames) / Mathf.Max(1, dashData.activeFrames); rb.linearVelocity = new Vector2(dashDir * dashData.dashSpeed * (1f - t) * (1f - t), 0f); if (enableDashAfterimage && dashLogicTimeCounter - lastAfterimageTime >= dashAfterimageSpawnInterval) { SpawnAfterimage(); lastAfterimageTime = dashLogicTimeCounter; } }
        else rb.linearVelocity = Vector2.zero;
    }

    private void EndDash() { rb.gravityScale = originalGravityScale; OnDashComplete?.Invoke(); }
    public void StopDashPhysics() => rb.gravityScale = originalGravityScale;
    public bool IsInvincible() => IsDashing && dashData != null && currentDashFrame <= dashData.startupFrames + dashData.activeFrames;
    public bool IsInDashRecovery() => IsDashing && dashData != null && currentDashFrame > dashData.startupFrames + dashData.activeFrames;

    private IEnumerator PlungeAfterimageRoutine()
    {
        float last = -dashAfterimageSpawnInterval, elapsed = 0f;
        while (!isGrounded) { if (elapsed - last >= dashAfterimageSpawnInterval) { SpawnAfterimage(); last = elapsed; } elapsed += Time.deltaTime; yield return null; }
        plungeAfterimageCoroutine = null;
    }

    public void SpawnUltimateBurst(int count, float interval) => StartCoroutine(UltimateBurstRoutine(count, interval));
    private IEnumerator UltimateBurstRoutine(int count, float interval) { for (int i = 0; i < count; i++) { SpawnAfterimage(); yield return new WaitForSeconds(interval); } }

    public void SpawnAfterimage()
    {
        if (spriteRenderer == null) return;
        GameObject ghost = new GameObject("DashGhost"); ghost.transform.position = transform.position; ghost.transform.rotation = transform.rotation; ghost.transform.localScale = transform.localScale;
        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>(); sr.sprite = spriteRenderer.sprite; sr.color = dashAfterimageColor; sr.flipX = spriteRenderer.flipX; sr.flipY = spriteRenderer.flipY; sr.sortingLayerID = spriteRenderer.sortingLayerID; sr.sortingOrder = spriteRenderer.sortingOrder - 1;
        StartCoroutine(FadeAndDestroyGhost(ghost, sr, dashAfterimageColor, dashAfterimageLifetime));
    }

    private IEnumerator FadeAndDestroyGhost(GameObject g, SpriteRenderer sr, Color c, float l)
    {
        float e = 0f; while (e < l) { if (sr == null) break; sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, 0f, e / l)); e += Time.deltaTime; yield return null; }
        if (g != null) Destroy(g);
    }

    private IEnumerator AttackMovementCoroutine(MotionKeyframe k)
    {
        isAttackMotionActive = true; float dir = FacingDirection; Vector2 start = rb.position, target = start;
        switch (k.taskType) { case MotionTaskType.Relative: target = start + new Vector2(k.position.x * dir, k.position.y); break; case MotionTaskType.Absolute: target = start + (Vector2)k.position; break; case MotionTaskType.SnapToEnemy: Transform s = GetSnapTarget(8f); if (s != null) { Vector2 toE = (Vector2)s.position - start; target = (Vector2)s.position - (toE.normalized * 1.2f); } else target = start + new Vector2(k.position.x * dir, k.position.y); break; }
        float dur = Mathf.Max(0, k.endFrame - k.startFrame) / (float)FrameDataConstants.LOGIC_FPS;
        if (dur <= 0) rb.position = target;
        else { float e = 0f; while (e < dur) { float delta = useUnscaledAttackMotion ? Time.unscaledDeltaTime : Time.fixedDeltaTime; e += delta; float t = Mathf.Clamp01(e / dur); Vector2 cur = Vector2.Lerp(start, target, TweenEase(0, 1, t, k.easing)); if (useUnscaledAttackMotion) { rb.position = cur; yield return null; } else { Vector2 d = cur - rb.position; attackMotionVelocityX = d.x / Time.fixedDeltaTime; attackMotionVelocityY = d.y / Time.fixedDeltaTime; yield return new WaitForFixedUpdate(); } } }
        StopAttackMotion();
    }

    private float TweenEase(float f, float t, float time, LeanTweenType e)
    {
        switch (e) { case LeanTweenType.linear: return LeanTween.linear(f, t, time); case LeanTweenType.easeInQuad: return LeanTween.easeInQuad(f, t, time); case LeanTweenType.easeOutQuad: return LeanTween.easeOutQuad(f, t, time); case LeanTweenType.easeInOutQuad: return LeanTween.easeInOutQuad(f, t, time); case LeanTweenType.easeInCubic: return LeanTween.easeInCubic(f, t, time); case LeanTweenType.easeOutCubic: return LeanTween.easeOutCubic(f, t, time); case LeanTweenType.easeInOutCubic: return LeanTween.easeInOutCubic(f, t, time); case LeanTweenType.easeInSine: return LeanTween.easeInSine(f, t, time); case LeanTweenType.easeOutSine: return LeanTween.easeOutSine(f, t, time); case LeanTweenType.easeInOutSine: return LeanTween.easeInOutSine(f, t, time); default: return Mathf.Lerp(f, t, time); }
    }

    private Transform GetSnapTarget(float r)
    {
        if (CameraManager.instance != null) { EnemyController e = CameraManager.instance.GetNearestEngagedEnemy(transform.position); if (e != null && Vector2.Distance(transform.position, e.transform.position) <= r) return e.transform; }
        Collider2D[] res = new Collider2D[10]; int count = Physics2D.OverlapCircle(transform.position, r, new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = 1 << enemyLayer }, res);
        Transform n = null; float c = float.MaxValue; for (int i = 0; i < count; i++) { EnemyController ec = res[i].GetComponentInParent<EnemyController>(); if (ec != null && ec.IsAlive) { float d = Vector2.Distance(transform.position, res[i].transform.position); if (d < c) { c = d; n = res[i].transform; } } }
        return n;
    }

    private IEnumerator DropDownRoutine(Collider2D t)
    {
        if (t == null || solidColliders == null) { dropDownCoroutine = null; yield break; }
        ignoredGroundCollider = t; SetDropDownCollision(t, true); isGrounded = false; currentGroundCollider = null; if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, -4f));
        yield return new WaitForSeconds(P.dropDownDuration); float r = Time.time + 0.5f; while (t != null && !HasClearedDropDownCollider(t) && Time.time < r) yield return null;
        SetDropDownCollision(t, false); if (ignoredGroundCollider == t) ignoredGroundCollider = null; dropDownCoroutine = null;
    }

    private void SetDropDownCollision(Collider2D t, bool i) { if (t != null && solidColliders != null) foreach (var c in solidColliders) if (c != null) Physics2D.IgnoreCollision(c, t, i); }

    private bool HasClearedDropDownCollider(Collider2D t)
    {
        if (t == null || solidColliders == null) return true;
        float b = t.bounds.min.y, p = float.MinValue; bool f = false; foreach (var c in solidColliders) if (c != null) { p = Mathf.Max(p, c.bounds.max.y); f = true; }
        return !f || p <= b - 0.02f;
    }
}
