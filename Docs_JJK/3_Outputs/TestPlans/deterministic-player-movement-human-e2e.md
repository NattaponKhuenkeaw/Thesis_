---
slug: "deterministic-player-movement-refactor"
status: ready-for-human-e2e
source: manual
gdd_tags:
  - "combat"
owner: "human"
human_checkpoint: required
next_agent: "human"
blocked_by: []
---

# Human E2E: Deterministic Player Movement

## Purpose

Verify that movement and combat context feel deterministic under real input. This is the gameplay acceptance test; no automated test substitutes for this pass.

References:

- `Docs_JJK/3_Outputs/Specs/deterministic-player-movement-refactor-task-card.md`
- `Docs_JJK/3_Outputs/Specs/deterministic-player-movement-refactor-impact-analysis.md`
- `Docs_JJK/3_Outputs/ADRs/003-deterministic-player-state-owned-movement.md`
- GDD `@tag:combat`

## Setup

- Scene: `Assets/Project/Scenes/Test_Playground.unity`
- Run each input-sensitive case once with keyboard and once with controller.
- Use a character with a ground normal attack, an air normal attack, a ground-started action with authored vertical MotionData, an action authored with Air output, double jump, and air dash.
- Do not tune values during the first pass. Record the failure and reproduction first.

## Pass Rule

The pass is based on state and action consistency, not only animation appearance:

- A ground action may begin only after valid floor/platform contact.
- A combat action keeps its captured input context until natural completion or a legal cancel; physical contact cannot interrupt or reclassify it.
- The outgoing action's authored output is committed before the next action, locomotion state, or team switch resolves.
- Repeating the same input sequence from the same position produces the same movement and action route.
- InputGate and authored MotionData may intentionally restrict or move the player; ambient Rigidbody gravity or acceleration may not create a competing result.

Reference timing targets from the supplied *BlazBlue Entropy Effect* recordings:

- Jump launch begins after 3-4 logic frames (50-67 ms at 60 Hz).
- The baseline jump rises for about 0.36 seconds, has a short apex, then accelerates into a faster descent.
- Edge/drop descent begins close to zero vertical speed and accelerates more gently than a jump-arc descent.
- Starting descent values are `jumpFallAcceleration = 95`, `neutralFallAcceleration = 40`, and `maximumFallSpeed = 28`.
- Dash starting curve: `1.18x` peak on the first Active frame, ease-out settlement to `1.00x` during the first `22%` of Active, then a smooth Recovery brake to `0.08x` immediately before Cancel.

## Core Scenarios

### 1. Direct horizontal control

- [ ] From Idle, press left or right: speed changes immediately to the profile move speed.
- [ ] Release input: horizontal movement stops on the next physics step, with no coast.
- [ ] Reverse direction: velocity changes directly, with no acceleration ramp.
- [ ] Repeat on the ground and in normal airborne movement.
- [ ] During an action, horizontal input follows that action's InputGate exactly.

### 2. Jump and edge transition

- [ ] Repeat a standing jump five times; startup timing, launch height, and fall arc are visually consistent.
- [ ] During the internal jump-startup frames the player remains Ground; `PlayerJumpState` publishes Airborne only on the exact launch frame.
- [ ] Walk off an edge without jumping; state becomes Falling and never briefly returns to a ground action context.
- [ ] Press jump just after leaving the edge; coyote jump works only within its authored frame window.
- [ ] Double jump restarts the same jump session, clears existing downward momentum, and produces the full authored launch speed.
- [ ] Double jump consumes one air jump and its resource resets only after landing.
- [ ] Once upward velocity ends, the jump session accelerates downward every fixed step without an apex hang or an instant terminal-speed snap.
- [ ] The jump session reaches the ground noticeably faster than an equal-height neutral edge/drop fall.

### 3. Falling attack-spam regression

- [ ] Walk off a platform without jumping and spam normal attack throughout the fall.
- [ ] Before contact, every accepted attack resolves through the air action route; no ground attack starts.
- [ ] If no air action/resource is available, input is rejected or buffered according to the existing combat rule; it does not fall back to a ground action.
- [ ] Air-action vertical suspension and authored MotionData remain repeatable while attack is spammed.
- [ ] On real contact, the current air action and animation continue; no combo reset, cooldown loss, Landing animation, or Ground route occurs mid-action.
- [ ] The player cannot move freely under a ground-attack animation caused by a stale ground/air context.
- [ ] From Falling, an airborne-only Skill and Ultimate can resolve when their normal resource/cooldown conditions pass.
- [ ] With Kyo, use the air Skill, land before its seven-second cooldown ends, and press Skill repeatedly; the ground Skill remains rejected and the shared cooldown UI continues counting down.
- [ ] Repeat in reverse: use Kyo's ground Skill, jump before cooldown ends, and confirm the air Skill remains rejected.
- [ ] Confirm conditional Skill variants explicitly authored with zero cooldown can still resolve during their valid passive/follow-up window.
- [ ] Start a ground-input attack, move beyond an edge during the action, and confirm ordinary route conditions continue to observe the captured Ground input context.
- [ ] Author/use a `Require Just Landed` trigger and confirm it observes contact during the locked action without changing that action's Ground/Air context.
- [ ] Repeat at least ten platform drops and ten jump descents.

### 4. Landing and one-way platforms

- [ ] Land on solid ground at center and near both edges; landing occurs once and downward speed becomes zero.
- [ ] Land on a one-way platform at center and near both edges; the player does not hover, snap early, or select a ground action before contact.
- [ ] On valid one-way contact, `GroundC` aligns exactly to the platform collider's top surface; the body is not left embedded.
- [ ] Spam attack during each landing case; the falling attack remains air-context until contact.
- [ ] Hold down to drop through a one-way platform; collision is restored only after the player clears it.
- [ ] Down+jump produces no upward launch: the player enters Falling in the same logic step with approximately zero initial vertical speed.
- [ ] Walking off an edge and dropping through produce the same gentle neutral-fall acceleration.
- [ ] While dropping through, only the supporting platform is ignored; a lower one-way platform can still catch and snap the player.
- [ ] Jump from the edge of a one-way platform; coyote behavior matches a solid-platform edge.

### 5. Combat-authored vertical motion

- [ ] Start an air attack while rising, at apex, and while falling; base vertical motion suspends for the complete authored action.
- [ ] When the air action ends without landing, the previously latched jump/neutral fall acceleration resumes from a stable state.
- [ ] MotionData X/Y displacement is repeatable and is not altered by Rigidbody acceleration.
- [ ] A ground-started `Both` action with authored lift remains on its captured ground combo route for its complete lifetime.
- [ ] The same `Both` action started in the air remains on its captured air route through physical landing, completion, or cancel.
- [ ] Ground-only standalone actions never resolve from a falling or jumping state.
- [ ] `MatchInput` output preserves the context captured when the action began.
- [ ] A Ground-output action cancelled/completed while visually airborne snaps to valid ground before the next action or locomotion state and does not add a Landing lock.
- [ ] During the Recovery switch window, switching characters out of a visually airborne Ground-output action is available and leaves the incoming character grounded with no carried action motion.
- [ ] A Ground-input/Air-output action remains Ground during execution, then enters Air/Falling only after completion or accepted cancel.
- [ ] An Air-output action that is already physically supported resolves Air first, then visibly/statefully performs `Landing -> Idle/Move`.
- [ ] Cancel each output case into Attack, Skill, Ultimate, Dash, and Jump where the interrupt policy permits; the incoming action resolves against the outgoing output, not stale input context.

### 6. Dash and resource regression

- [ ] Ground and air dash start at the `1.18x` starting peak, visibly settle to authored `dashSpeed`, and preserve the punch through Active.
- [ ] Recovery braking is continuous from `1.00x` toward `0.08x`; there is no weak start, constant-speed glide, second acceleration, or abrupt stop.
- [ ] Ground Dash remains movement/facing-locked for every Recovery frame.
- [ ] On the exact authored Ground Dash Cancel frame, held A/D immediately produces normal walking speed and can turn the character; there is no hidden post-cancel lock.
- [ ] On the first Air Dash Recovery frame, airborne gravity resumes but held A/D does not add walk speed to the authored air-dash distance.
- [ ] Ground Dash Cancel visibly returns to Run/Idle on the same frame control returns; Dash ActionEnd does not cause a second speed change or hitch.
- [ ] Mash dash throughout startup, active, recovery, and cooldown; only one dash occurs and no second dash is queued.
- [ ] A full post-exit cooldown elapses before another ground dash can start, including characters whose recovery exceeds their cooldown frame value.
- [ ] Dash immediately after walking off an edge resolves as an air dash, not a coyote-ground dash.
- [ ] Air dash consumes one air dash and resets only on landing.
- [ ] Dash, jump, double jump, air attack, and landing can be chained without stuck velocity or a stale state.
- [ ] Character movement-speed stat overrides still change direct horizontal speed.
- [ ] Switching characters during or after movement leaves no carried attack motion or dash velocity.
- [ ] From an air attack, cancel into air dash while rising, near apex, and falling. Dash animation starts immediately and remains visible through Dash; Fall begins only after Dash exits.

### 7. Combat animation ownership

- [ ] Execute each normal combo route by repeated click at least ten times; every new step restarts its authored clip at frame zero.
- [ ] Execute every available auto-chain route at least ten times; no transition keeps replaying the previous step animation.
- [ ] Execute directional/conditional branches repeatedly; the animation always matches the step selected by the resolver.
- [ ] Test two consecutive steps that reuse the same Animator state or clip; the second step visibly restarts instead of continuing the previous normalized time.
- [ ] Test a looping Active step; only its resolved action state is time-synchronized and unrelated Animator layers/states are not replayed.
- [ ] Cancel ground and air attacks into dash, jump, skill, ultimate, and legal basic attack routes; output context commits first and the incoming action animation wins on the same frame.

## Player-Experience Passes

- [ ] New-player pass: movement state is readable without debug tools; the tester can predict whether attack will be ground or air.
- [ ] Stress pass: rapidly alternate left/right, jump, dash, and attack for 60 seconds without a stuck state.
- [ ] Skill pass: repeat a chosen jump-to-air-attack route ten times and confirm comparable spacing and landing timing.
- [ ] Abuse pass: mash attack and direction at platform edges and during landing; no illegal ground attack or free movement appears.
- [ ] Readability pass: animation, physical motion, and accepted input all communicate the same state.
- [ ] Vertical-input isolation: press and hold W, then S, while idle, running, jumping, falling, and dashing. Rigidbody X/Y behavior is identical to neutral input except when an authored directional action condition or Down+Jump drop-through explicitly consumes it.

## Execution Record

| Date | Tester | Input | Build/Commit | Result | Notes |
| --- | --- | --- | --- | --- | --- |
|  |  | Keyboard |  | Not run |  |
|  |  | Controller |  | Not run |  |

## Tuning Guidance After Functional Pass

The current profile values are starting values. Tune only one outcome at a time:

- Horizontal pace: `moveSpeed`
- Jump height/initial rise: `jumpSpeed`
- Rise shape: `jumpSpeed` and `riseDeceleration`
- Jump descent punch: `jumpFallAcceleration`
- Edge/drop weight: `neutralFallAcceleration`
- Long-fall cap: `maximumFallSpeed`
- Input forgiveness: `coyoteFrames`
- Anticipation/commitment: `jumpStartupFrames`
- Landing commitment: `landingRecoveryFrames`

If a scenario fails because the wrong state or action route is selected, treat it as a logic defect rather than compensating with profile tuning.

Starting-value adjustment order:

1. If jump descent still feels lunar, raise `jumpFallAcceleration` by 10-15 without changing launch/rise.
2. If edge/drop feels pulled downward instead of naturally released, lower `neutralFallAcceleration` by 5; if it hangs, raise it by 5-10.
3. Change `maximumFallSpeed` only if long falls remain globally too slow or too fast after both early curves feel correct.
4. For Dash, change one starting knob at a time after ten repeated inputs:
   - Weak first-frame response: raise peak by `0.04`; harsh/teleport-like response: lower it by `0.04`.
   - Recovery hard-stops: raise brake-end ratio by `0.03`; Recovery glides too far: lower it by `0.03`.
   - Initial accent lasts too long: lower settle progress by `0.05`; it vanishes too quickly: raise it by `0.05`.
