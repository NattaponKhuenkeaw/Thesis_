# Impact Analysis: Deterministic Player Movement Refactor

**Refactor Name:** Deterministic player motor and ground/air ownership  
**Author:** Codex  
**Date:** 2026-07-24  
**Status:** Approved for implementation; human E2E feel approval remains

## Refactor Goal

Replace coupled acceleration, proximity-grounding, and airborne gravity policies with a small state-driven motor whose output is predictable from input, current FSM state, and authored action motion.

## Current Failure Mechanism

`PlayerMovement.IsGroundedForActionSelection()` returns grounded while a descending player is merely within `groundedActionProbeDistance` of a surface. `PlayerStateMachine.RouteAttackInput()` trusts that result and can therefore enter `PlayerAttackState` before contact. Because `PlayerAirAttackState` is the state that changes movement locking, the incorrectly selected ground action can also expose movement behavior that does not match the animation.

The resolver adds a second ownership failure: it derives `IsAirCombo` only from an action declared as `Air`, so a `Both` action cast while falling becomes ground-owned. Standalone actions are also selected without first enforcing their declared Ground/Air context. The refactor must capture the cast context for `Both` actions and context-filter standalone actions before condition matching.

Air attack motion is additionally coupled across:

- seven `AirborneGravityPolicy` values,
- current vertical velocity,
- `Rigidbody2D.gravityScale`,
- an apex threshold,
- acceleration/deceleration,
- gravity-scaled airborne drift.

The resulting position is difficult to predict from state and frame data alone.

## Proposed Runtime Rules

| State / situation | Horizontal rule | Vertical rule | Action context |
|---|---|---|---|
| Grounded locomotion | Input directly selects `-moveSpeed`, `0`, or `moveSpeed` | Downward velocity is zeroed on valid contact | Ground |
| Jump startup | Current accepted horizontal input; no acceleration | Hold at zero until the authored startup frame, then assign `jumpSpeed` | Airborne from jump commitment |
| Jump rising / falling | Input directly selects air speed | Motor decelerates the rise, then accelerates quickly toward terminal fall speed | Air |
| Edge / drop falling | Input directly selects air speed | Begin near zero vertical speed and accelerate gently toward the same terminal fall speed | Air |
| Airborne attack | Input gate explicitly allows or blocks direct horizontal control | Suspend base vertical motion while the air action is active; only authored MotionData may move the player | Air until landing |
| Ground-started Both action with lift | Combat state decides whether locomotion input is locked | Authored MotionData may move the body | Captured Ground context remains unchanged |
| Landing | Resolve only from valid collision contact; zero downward speed and let the physics contact solve the final surface position | Exit air action before ground input routing | Ground on contact |
| Drop-through | Ignore only the selected one-way collider until the player collider has cleared it | Force airborne/falling state | Air |

## MovementProfile Target

Keep only values that change the locomotion result and can reasonably vary per character:

| Keep | Replace / remove |
|---|---|
| `moveSpeed` | four acceleration/deceleration fields |
| deterministic `jumpSpeed` | impulse-named `jumpForce` |
| deterministic `riseDeceleration` | rise/apex/fall gravity multipliers and apex threshold |
| `jumpFallAcceleration` | gravity multipliers and apex thresholds |
| `neutralFallAcceleration` | implicit Rigidbody fall from edge/drop |
| `maximumFallSpeed` | vertical-speed movement penalty and competing terminal-speed rules |
| `maxJumpCount` | profile jump-buffer duration that runtime does not read |
| `maxAirDashCount` | post-attack gravity grace that runtime does not read |
| frame-based jump startup | proximity-grounded probe distance and velocity threshold |
| frame-based coyote window | per-phase airborne gravity policy |
| frame-based landing recovery | gravity-scaled airborne drift |

Attack facing lock-on remains behaviorally intact during this pass but should not be treated as movement tuning. Moving it to combat configuration is a possible later cleanup, not part of this refactor.

## State Ownership

| Concern | Current owner | Proposed owner |
|---|---|---|
| Physical grounded contact | Overlap circle plus `GroundStateProvider` authority overrides | Actual collider contact maintained by `PlayerMovement` |
| Ground/air action selection | Physical overlap, proximity ray, velocity threshold, and combat authority | FSM movement state at action entry plus actual landing contact |
| Ground-started action lift | `GroundStateProvider.StickyGrounded` | Captured combat action context |
| Air action suspension | `AirborneGravityPolicy` evaluated by `CombatPlayerState`, applied through `PlayerCombat`, then consumed by `PlayerMovement` | Single AirborneAttacking motor rule |
| Horizontal responsiveness | Multiple acceleration curves and attenuation multipliers | Direct target velocity |
| Jump launch | Rigidbody impulse | Direct vertical velocity assignment |
| Landing | Overlap radius and delayed Update transition | Fixed-step contact transition, then FSM Landing |

## Affected Files and Blast Radius

| File | Change | Risk |
|---|---|---|
| `PlayerMovement.cs` | Simplify motor, contact detection, jump launch, and action motion integration | High |
| `MovementProfile.cs` and three profile assets | Remove obsolete fields and migrate values | Medium |
| `GroundStateProvider.cs` | Remove general-purpose authority overrides or reduce it to contact history | High |
| `PlayerStateMachine.cs` | Route action context from explicit state/contact | High |
| `CombatPlayerState.cs` | Capture action context and remove gravity-policy plumbing | High |
| `PlayerCombat.cs` / `ICharacterCombat.cs` | Remove airborne gravity bridge methods | Medium |
| `PlayerFallingState.cs` | Allow valid air attack routing while falling | Medium |
| `PlayerAirAttackState.cs` | Declare the one air-action movement rule | Medium |
| `PlayerJumpStartupState.cs` | Use frame timing and deterministic launch | Low |
| `Player.prefab` | Remove orphaned serialized movement fields if necessary | Medium |
| New movement tests | Lock action-selection and state-transition invariants | Low |

## Incremental Implementation Order

1. Remove predictive near-ground action selection and fix falling attack routing.
2. Replace jump impulse and horizontal acceleration with direct velocity rules.
3. Replace attack gravity policy with the single AirborneAttacking rule.
4. Trim `MovementProfile`, migrate its three assets, and remove unused methods after reference checks.
5. Compile, inspect the Unity console, and perform the human E2E script.

## Risks and Mitigations

- **Existing attacks may rely on air-attack hover curves.** Preserve the current visible intent as a single suspension rule, then playtest recovery timing before changing action frame data.
- **Ground-authored MotionData can lift the collider.** Keep Ground as a captured combat context until the action ends unless `allowGroundToAirTransition` is authored.
- **One-way platform contacts can be sensitive to collider setup.** Test platform center, platform edge, upward pass-through, and down+jump drop-through separately.
- **Character feel can shift when acceleration is removed.** Preserve each profile's existing maximum move and jump speeds as migration starting values; evaluate response before tuning.
- **Dirty scene and character assets exist in the worktree.** Do not modify or revert unrelated scene, animation, character, progression, font, or build-setting changes.

## Game-Design Evaluation

- **Response:** Stronger; input maps immediately to an explicit speed or action state.
- **Clarity:** Stronger; attack context changes only on action entry or real landing.
- **Satisfaction:** Neutral in code; existing animation/VFX/audio remain unchanged.
- **Fit:** Targets a fighting-game-like state commitment with predictable air suspension.
- **Motivation:** Not applicable to this refactor.

## Playtest Script

1. **New-player test:** Walk, stop, reverse, jump, and land; confirm every input produces an immediately readable state.
2. **Stress test:** Fall from a ledge and spam attack before, during, and one frame after landing; no ground attack may begin before contact.
3. **Air-action test:** Attack while rising and falling; confirm the same suspension rule starts from both paths and recovery always returns to falling.
4. **One-way test:** Land at center and edge, jump upward through it, then use down+jump to drop through without re-snapping.
5. **Ground-lift test:** Use a Ground/Both attack with vertical MotionData; its combo context remains Ground, and it exits to air locomotion if the action ends without contact.
6. **Abuse test:** Spam air attack, dash, jump, and direction changes; confirm no indefinite hover unless the authored combo itself is still active.
7. **Readability test:** Watch without input display and identify Ground, JumpStartup, Rising/Falling, AirborneAttacking, and Landing transitions.

## Starting-Value Policy

No final tuning values are approved by this document. Migration starts from current authored maximum move speeds, jump launch speeds, jump-startup timing, coyote timing, and landing recovery. Pass criteria are deterministic state selection and repeatable trajectory first; subjective tuning follows the human playtest checkpoint.
