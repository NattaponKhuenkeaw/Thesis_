# Impact Analysis: Deterministic Player Movement Refactor

**Refactor Name:** Deterministic player motor and ground/air ownership  
**Author:** Codex  
**Date:** 2026-07-24; amended 2026-07-27
**Status:** Implemented and compile-verified; human E2E feel approval remains

## Refactor Goal

Replace coupled acceleration, proximity-grounding, and airborne gravity policies with a state-driven motor whose output is predictable from input, authored action motion, a captured action input context, and a committed action output context. Dash must front-load its response, brake under authored authority, and return control exactly at its Cancel frame.

## Current Failure Mechanism

`PlayerMovement.IsGroundedForActionSelection()` returns grounded while a descending player is merely within `groundedActionProbeDistance` of a surface. `PlayerStateMachine.RouteAttackInput()` trusts that result and can therefore enter `PlayerAttackState` before contact. Because `PlayerAirAttackState` is the state that changes movement locking, the incorrectly selected ground action can also expose movement behavior that does not match the animation.

The resolver adds a second ownership failure: it derives `IsAirCombo` only from an action declared as `Air`, so a `Both` action cast while falling becomes ground-owned. Standalone actions are also selected without first enforcing their declared Ground/Air context. The refactor must capture the cast context for `Both` actions and context-filter standalone actions before condition matching.

Action motion was additionally coupled across:

- seven `AirborneGravityPolicy` values,
- current vertical velocity,
- `Rigidbody2D.gravityScale`,
- an apex threshold,
- acceleration/deceleration,
- gravity-scaled airborne drift.

The resulting position is difficult to predict from state and frame data alone. Physical landing also reset an air combo and played landing feedback while its action was still active. Dash separately returned walking control on Recovery, even when the authored Cancel frame was later, producing a weak tail and an animation/control mismatch.

## Proposed Runtime Rules

| State / situation | Horizontal rule | Vertical rule | Action context |
|---|---|---|---|
| Grounded locomotion | Input directly selects `-moveSpeed`, `0`, or `moveSpeed` | Downward velocity is zeroed on valid contact | Ground |
| Jump startup | Current accepted horizontal input; no acceleration | Hold at zero until the authored startup frame, then assign `jumpSpeed` | Airborne from jump commitment |
| Jump rising / falling | Input directly selects air speed | Motor decelerates the rise, then accelerates quickly toward terminal fall speed | Air |
| Edge / drop falling | Input directly selects air speed | Begin near zero vertical speed and accelerate gently toward the same terminal fall speed | Air |
| Active combat action | Input gate explicitly allows or blocks direct horizontal control | Suspend base vertical movement for Startup, Active, Recovery, and Cancel; only authored MotionData may move the player | Captured Input Context remains locked |
| Ground-input action with authored lift | Combat state decides whether locomotion input is locked | Authored MotionData may lift or return the body without competing gravity | Ground input remains captured; output is independently authored |
| Physical contact during an action | No locomotion reclassification | Update collision/resources, suppress landing feedback and combo reset | Explicit `Just Landed` triggers may observe contact; normal context does not change |
| Ground output | Resolve before the next state/action/switch | Snap the authored ground-check level to valid ground and clear landing lock | Ground |
| Air output while supported | Resolve Air first, then reconcile after action states have ended | Replay landing feedback/recovery after action completion | Air -> Landing -> Ground |
| Dash | Locked authored burst through Active; locked smooth brake through Recovery; locomotion on Cancel | Ground dash stays vertically stable; Air Dash Recovery resumes normal fall | Captured at Dash entry |
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
| Ground/air action selection | Physical overlap, proximity ray, velocity threshold, and combat authority | FSM context captured once when the action is selected |
| Action completion/cancel context | Implicit physical state or legacy ground-to-air flag | `CombatAction.outputContext`, committed before the next route |
| Ground-started action lift | `GroundStateProvider.StickyGrounded` | Captured input context plus independently authored output context |
| Combat-action suspension | `AirborneGravityPolicy` evaluated by `CombatPlayerState`, applied through `PlayerCombat`, then consumed by `PlayerMovement` | One rule for every active combat action: MotionData owns vertical action motion |
| Landing during combat | Physical `OnLand` could reset combo/play landing immediately | Contact is recorded; feedback/reset are deferred until Air-output reconciliation |
| Dash control handoff | First Recovery frame | Authored Cancel frame |
| Horizontal responsiveness | Multiple acceleration curves and attenuation multipliers | Direct target velocity |
| Jump launch | Rigidbody impulse | Direct vertical velocity assignment |
| Landing | Overlap radius and delayed Update transition | Fixed-step contact transition, then FSM Landing |

## Affected Files and Blast Radius

| File | Change | Risk |
|---|---|---|
| `PlayerMovement.cs` | Simplify motor/contact/jump, integrate action motion, output snap/reconciliation, and dash curve | High |
| `MovementProfile.cs` and three profile assets | Remove obsolete fields and migrate values | Medium |
| `GroundStateProvider.cs` | Remove general-purpose authority overrides or reduce it to contact history | High |
| `PlayerStateMachine.cs` | Route action context from explicit state/contact | High |
| `CombatPlayerState.cs` | Capture input/output context, commit output on completion/cancel, and remove landing interruption | High |
| `PlayerCombat.cs` / `ICharacterCombat.cs` | Remove airborne gravity bridge methods | Medium |
| `PlayerFallingState.cs` | Allow valid air attack routing while falling | Medium |
| `PlayerAirAttackState.cs` | Declare the one air-action movement rule | Medium |
| `TeamLoader.cs` | Snap a successful switch only when the committed action output is Ground | Medium |
| `PlayerJumpStartupState.cs` | Use frame timing and deterministic launch | Low |
| `Player.prefab` | Remove orphaned serialized movement fields if necessary | Medium |
| Human movement E2E plan | Validate action-state and feel invariants without adding/running script tests | Medium |

## Incremental Implementation Order

1. Remove predictive near-ground action selection and fix falling attack routing.
2. Replace jump impulse and horizontal acceleration with direct velocity rules.
3. Replace attack gravity policy with the single AirborneAttacking rule.
4. Trim `MovementProfile`, migrate its three assets, and remove unused methods after reference checks.
5. Add action Input/Output Context authoring and commit output on natural completion or accepted cancellation.
6. Move Ground Dash locomotion handoff to Cancel and apply a front-loaded burst plus Recovery brake.
7. Compile both Unity assemblies and perform the human E2E script. No automated/script test is added or run for this task.

## Risks and Mitigations

- **Existing attacks may rely on air-attack hover curves.** Preserve the current visible intent as one full-action suspension rule, then playtest recovery timing before changing action frame data.
- **Ground-authored MotionData can lift the collider.** Keep the input context captured and use `outputContext = Air` only for actions deliberately designed to end airborne.
- **One-way platform contacts can be sensitive to collider setup.** Test platform center, platform edge, upward pass-through, and down+jump drop-through separately.
- **Character feel can shift when acceleration is removed.** Preserve each profile's existing maximum move and jump speeds as migration starting values; evaluate response before tuning.
- **Infinite downward Ground-output snap can cross a large vertical gap.** This is intentional authored Ground-output semantics; validate rooms with stacked platforms and do not assign Ground output to actions meant to remain airborne.
- **Air output may already be physically supported.** Defer reconciliation while any frame-data action owns control, then replay one Landing transition after action authority ends.
- **Dirty scene and character assets exist in the worktree.** Do not modify or revert unrelated scene, animation, character, progression, font, or build-setting changes.

## Game-Design Evaluation

- **Response:** Stronger; action commitment ends at an authored Cancel frame, with no hidden Recovery tail or post-snap Landing lock.
- **Clarity:** Stronger; the action editor exposes `Input Context -> Output Context`, and animation/control switch together at Dash Cancel.
- **Satisfaction:** Stronger; Dash combines front-loaded speed, authored animation, and afterimages, followed by a readable brake rather than a linear glide.
- **Fit:** Targets fighting-game-like commitment: action state remains authoritative until its explicit completion/cancel boundary.
- **Motivation:** Not applicable to this refactor.

## State-Machine Contract

| Property | Contract |
|---|---|
| Entry | Resolver validates Ground/Air/Both against the current FSM selection context and captures that context once. |
| Exit | Natural `Done` or a legal interrupt/cancel commits output before the incoming state resolves. |
| Interruptibility | `InterruptPolicy` is phase-owned; Cancel is a real phase with its own categories and input gate. |
| Chained actions | Steps inside one `CombatAction` keep the same captured context; a different action previews/resolves against the previous action's output. |
| Resource cost | Existing cooldown, Aura Energy, jump, dash, and team-switch costs remain owned by their existing entry/exit rules. |
| Edge cases | Physical landing mid-action, support loss, Ground snap, Air-on-ground reconciliation, same-state Skill/Ultimate replacement, attack cancel, dash/jump cancel, and team switch all follow the same output contract. |

## Playtest Script

1. **New-player test:** Walk, stop, reverse, jump, and land; confirm every input produces an immediately readable state.
2. **Stress test:** Fall from a ledge and spam attack before, during, and one frame after landing; no ground attack may begin before contact.
3. **Air-action test:** Attack while rising and falling, touch ground before the action ends, and confirm the action/animation continues; an Air output then performs Landing, while a Ground output snaps without Landing lock.
4. **One-way test:** Land at center and edge, jump upward through it, then use down+jump to drop through without re-snapping.
5. **Ground-lift test:** Use a Ground/Both attack with vertical MotionData; its captured context remains Ground. Compare `MatchInput`, explicit `Ground`, and explicit `Air` outputs at completion and cancel.
6. **Abuse test:** Spam air attack, dash, jump, and direction changes; confirm no indefinite hover unless the authored combo itself is still active.
7. **Dash test:** Repeat ten ground dashes with held/neutral/opposite input. Control and locomotion animation must return on the exact Cancel frame, never on Recovery, with no surge or hidden lock.
8. **Readability test:** Watch without input display and identify Ground, JumpStartup, Rising/Falling, combat action, output commit, and Landing transitions.

## Starting-Value Policy

No final tuning values are approved by this document. Migration starts from current authored maximum move speeds, jump launch speeds, jump-startup timing, coyote timing, and landing recovery.

Dash starting values:

- Burst peak: `1.18 x dashSpeed`
- Burst settle: first `22%` of Active, ease-out to `1.00 x`
- Recovery brake end: `0.08 x dashSpeed` on the frame before Cancel

Micro test: perform ten dashes each with keyboard and controller using neutral, held-forward, and held-opposite input. Pass when the first frames visibly lead the motion, Recovery brakes continuously under lock, and movement plus locomotion animation return on the exact Cancel frame without a second surge or hitch. If the burst is weak/harsh, adjust peak by `0.04` up/down respectively. If the brake hard-stops or glides, adjust its end ratio by `0.03` up/down. If the initial accent lasts too long/short, adjust settle progress by `0.05` down/up. These are starting-value increments, not standards.
