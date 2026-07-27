---
slug: "deterministic-player-movement-refactor"
status: ready-for-human-e2e
source: manual
gdd_tags:
  - "combat"
owner: "human"
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Deterministic Player Movement Refactor

## Player-Facing Goal

Make player movement read like a fighting-game controller: authored action motion has deterministic authority, each combat step keeps a stable Ground/Air context while executing, legal next-step boundaries route from current physical context, authored output decides final/cancel state, and Dash preserves its earlier curve while returning grounded control on the first Recovery frame.

## Source

- Origin: Manual prompt
- Requested by: User
- Reference feel: *BlazBlue Entropy Effect*

## GDD Reference

- `@tag:combat` - Every executing step captures Ground/Air context, legal next-step boundaries re-sample physical context, and MatchInput/Ground/Air output is committed only at completion or accepted cancel.

## Type

- [ ] Feature
- [x] Bug fix
- [x] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Player movement motor and physical ground contact
- Movement profile data
- Player FSM ground/air action selection
- Combat-action input/output context authoring and routing
- Combat landing feedback/reconciliation
- Dash motion curve, phase authority, and animation handoff

### Files To Inspect First

- `Assets/Project/Scripts/Combat/PlayerMovement.cs`
- `Assets/Project/Scripts/Character/MovementProfile.cs`
- `Assets/Project/Scripts/Player/GroundStateProvider.cs`
- `Assets/Project/Scripts/Player/PlayerStateMachine.cs`
- `Assets/Project/Scripts/Player/States/CombatPlayerState.cs`
- `Assets/Project/Scripts/Player/States/PlayerFallingState.cs`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`

### Out of Scope

- New movement abilities, attacks, enemy behavior, or animation assets
- Choosing special Ground-to-Air output values for unnamed character actions without designer direction
- Changing combo routes beyond correcting their state contract
- Tuning final game feel without a human playtest
- Adding or running automated/script tests, per the user's explicit instruction

## Acceptance Criteria

The corrective feel implementation is complete. Unchecked gameplay criteria intentionally remain open until the keyboard/controller human E2E pass.

- [x] Ground and air horizontal movement set a target speed directly; no acceleration, deceleration, vertical-speed attenuation, or gravity-scaled drift remains.
- [x] Jump launch sets a deterministic vertical speed rather than applying an impulse.
- [x] One `PlayerJumpState` owns startup, launch, rise, apex, descent, and internal double-jump restart; legacy JumpStartup/JumpRising classes are removed.
- [x] Jump startup remains grounded and horizontally motor-locked while directional input buffers; the exact launch frame restores horizontal response and changes the centralized environment context to airborne.
- [x] Once Jump startup is accepted, it owns animation immediately; landing notification or deferred action-output reconciliation cannot replace it with Landing, while the jump resource and upward velocity are committed on the authored launch frame.
- [x] Double jump clears accumulated vertical/fall momentum, repeats the authored motor-locked startup commitment, then applies the second deterministic launch.
- [x] The player motor owns its vertical acceleration instead of changing `Rigidbody2D.gravityScale` by movement or attack phase.
- [x] Rising velocity decelerates explicitly; descent accelerates toward one terminal speed using a jump-arc or neutral-fall session.
- [x] Every active combat action suspends base vertical locomotion for its complete authored duration; MotionData remains the only action-motion authority.
- [x] A falling player remains airborne until a real valid ground contact occurs.
- [ ] Spamming attack while falling never resolves a ground attack; it resolves an available air action or remains buffered/rejected.
- [x] Physical landing cannot interrupt an active combat action, reset its combo, or steal its action animation.
- [ ] One-way platform and edge landings resolve from collision contact, zero downward speed, and enter Landing exactly once.
- [ ] Valid one-way contact snaps the authored `GroundC` level to the platform top without embedding the body.
- [x] Every executing combat step captures its Ground/Air input context and does not reclassify it from physical contact or support loss mid-step.
- [x] Every combat action exposes an authored output context (`MatchInput`, `Ground`, or `Air`) in both serialized data and the action editor.
- [x] Ground-started Both actions preserve Ground context during the running step; Air-started Both and standalone steps preserve/enforce Air context.
- [x] Natural completion and accepted cancels commit output context before locomotion, a different action, or character-switch cleanup resolves.
- [x] Ground output stops outgoing MotionData, resolves a fresh physical floor point even when the cached ground sample is stale, silently snaps to it, clears landing lock and same-tick `JustLanded`, and owns that correction for the remainder of the same fixed step so pre-simulation contacts cannot immediately republish Air. It never inserts Landing/Falling or a landing animation, and hands state/animation authority directly to the accepted next action or locomotion; Grounded is forced immediately if a stale Landed/Fall transition was queued.
- [x] Team Switch is accepted only from authored Ground context. A successful switch always performs the same silent Ground correction for the incoming character, even when outgoing Ground-action MotionData has temporarily lifted the shared Rigidbody. Because switching runs between physics simulations, the correction owns the next fixed-step ground sample so stale pre-snap contacts cannot create a real Air/Landing session. It routes directly to Idle/Move without Fall, Landing, or landing lag.
- [x] A legal Jump cancel from a physically airborne `MatchInput` or explicit Ground output validates against the pending ground-resource reset, then silently snaps and launches in the same transition without publishing `JustLanded`, entering Landing/Falling, or waiting through another startup hold. Ordinary jumps retain their authored startup.
- [x] Air output resolves Air first; if physically supported, it then replays Landing and reaches Idle/Move only after the action has ended.
- [x] Explicit `Require Just Landed` trigger conditions can observe physical contact without changing the locked action context.
- [x] Falling accepts airborne normal, skill, and ultimate resolution through the centralized environment context.
- [x] Ground and air variants of the same non-zero-cooldown Skill slot share cooldown availability; landing cannot bypass an air-skill cooldown by resolving the ground variant.
- [x] A ground-input step that leaves an edge keeps executing and its ordinary action conditions continue to observe captured Ground context until the legal next-step boundary.
- [x] At a legal next-step boundary, unchanged cause-aware context continues the current combo tree; genuine support-loss context resolves the same category and executes the matching action's exact root step.
- [x] Only genuine floor/platform support loss changes a Ground action to Air; positive-Y authored MotionData remains Ground-owned across boundaries.
- [x] Context-change routing bypasses root-route/depth shortcuts, and no matching contextual action ends combat into physical locomotion.
- [x] Final natural `MatchInput` completion preserves the executing step's captured input context. Genuine support-loss re-routing remains a legal next-step resolver decision rather than rewriting authored output; explicit Ground/Air output still overrides completion and accepted cancels.
- [x] `autoChainToNext` resolves before player input in Recovery and Cancel, including a Cancel window authored on the first Recovery frame.
- [x] Goenitz follow-up Ultimate reaches the authored `Recover` node after `Exit` without asset retiming.
- [x] Unused movement fields and methods are removed after project-wide reference checks.
- [x] Existing character movement profiles migrate without missing references.
- [x] Human E2E plan covers the state/action-selection rules.
- [ ] Works on keyboard and controller input.
- [ ] Does not break dash, double jump, drop-through, action MotionData, or character stat speed overrides.
- [ ] Dash cannot self-chain from spam and observes its complete post-exit cooldown.
- [x] Dash restores the prior full-speed-to-late-deceleration Active curve; Recovery no longer applies a separate authored brake.
- [x] Ground Dash returns normal locomotion/facing on its first Recovery frame and stays open through the late Cancel phase, with no final relock.
- [x] Air Dash Recovery keeps horizontal locomotion locked; neutral A/D keeps gravity locked, while any A/D input releases normal falling without adding walk speed.
- [x] Player physics accepts only scalar horizontal input. W/S remain available only to explicit directional action conditions and Down+Jump drop-through.
- [x] Dash animation remains authoritative through Startup/Active and Air Dash Recovery; Ground Dash Recovery returns Run/Idle on the same frame that control returns.
- [x] Every executed combat step restarts its resolved animation exactly once, including click, auto-chain, conditional, and same-state transitions.
- [x] Logic-frame animation synchronization targets only the resolved action state and runs only for looping Active phases.
- [x] Follows `RULES_AND_POLICY.md`.

## Human Checkpoints

- [x] Design/game-feel direction supplied by user
- [x] Architecture approval
- [ ] Playtest feel approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/deterministic-player-movement-refactor-impact-analysis.md`
