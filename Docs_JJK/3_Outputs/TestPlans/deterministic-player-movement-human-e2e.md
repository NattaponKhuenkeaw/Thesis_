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
- Use a character with a ground normal attack, an air normal attack, a ground-started action with authored vertical MotionData if available, double jump, and air dash.
- Do not tune values during the first pass. Record the failure and reproduction first.

## Pass Rule

The pass is based on state and action consistency, not only animation appearance:

- A ground action may begin only after valid floor/platform contact.
- An air action remains air-authoritative until it ends or real landing interrupts it.
- Repeating the same input sequence from the same position produces the same movement and action route.
- InputGate and authored MotionData may intentionally restrict or move the player; ambient Rigidbody gravity or acceleration may not create a competing result.

Reference timing targets from the supplied *BlazBlue Entropy Effect* recordings:

- Jump launch begins after 3-4 logic frames (50-67 ms at 60 Hz).
- The baseline jump rises for about 0.36 seconds, has a short apex, then accelerates into a faster descent.
- Edge/drop descent begins close to zero vertical speed and accelerates more gently than a jump-arc descent.
- Starting descent values are `jumpFallAcceleration = 95`, `neutralFallAcceleration = 40`, and `maximumFallSpeed = 28`.
- Dash holds full authored speed through the first 55% of active frames, then decelerates late to 15% speed.

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
- [ ] On real contact, the current air action is interrupted before any ground action can begin.
- [ ] The player cannot move freely under a ground-attack animation caused by a stale ground/air context.
- [ ] From Falling, an airborne-only Skill and Ultimate can resolve when their normal resource/cooldown conditions pass.
- [ ] With Kyo, use the air Skill, land before its seven-second cooldown ends, and press Skill repeatedly; the ground Skill remains rejected and the shared cooldown UI continues counting down.
- [ ] Repeat in reverse: use Kyo's ground Skill, jump before cooldown ends, and confirm the air Skill remains rejected.
- [ ] Confirm conditional Skill variants explicitly authored with zero cooldown can still resolve during their valid passive/follow-up window.
- [ ] Start a ground attack, move beyond an edge during the action, and confirm the action continues while subsequent conditions observe Airborne context.
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
- [ ] A ground-started `Both` action with authored lift remains on its ground combo route.
- [ ] The same `Both` action started in the air remains on its air-owned route through landing or completion.
- [ ] Ground-only standalone actions never resolve from a falling or jumping state.
- [ ] An action explicitly authored to transition ground-to-air changes context only after losing contact.

### 6. Dash and resource regression

- [ ] Ground and air dash start at full authored `dashSpeed`, hold the punch through early active frames, and decelerate visibly near the end.
- [ ] Dash deceleration is continuous; there is no weak start, constant-speed glide, or abrupt full-speed stop into recovery.
- [ ] On the first Ground Dash Recovery frame, held A/D immediately produces normal walking speed and can turn the character; there is no dead-control tail.
- [ ] On the first Air Dash Recovery frame, airborne gravity resumes but held A/D does not add walk speed to the authored air-dash distance.
- [ ] Ground Dash Recovery has no second acceleration/surge after the dash curve decelerates.
- [ ] Ground Dash Recovery can visibly return to Run/Idle according to current input; Dash ActionEnd does not cause a second speed change or hitch.
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
- [ ] Cancel ground and air attacks into dash, jump, skill, and ultimate; the incoming action animation wins on the same frame.

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
4. If dash loses punch too early, move its deceleration start later; if it hard-stops, raise its end-speed ratio slightly.
