---
slug: main-dimension-slash
status: needs-human
source: manual
gdd_tags: [combat, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Procedural Dimension Slash — Review Build

Updated2026-10-28 following the user's playtest feedback, scale/layer correction and typo correction. This revision replaces the first version's sprite barrage. Critical hits remain enabled; no per-hit critical-policy feature was added.

## Result

- Almost full-screen purple cuts, fitted to the current orthographic camera. Each ribbon independently reveals from one end, carries a luminous leading edge, then contracts in thickness/length and fades. Timings, dimensions, locations, angles and reveal directions vary per execution. A private seed keeps decorative randomness separate from Unity's gameplay RNG.
- The31 decorative ribbons comprise19 broad cuts plus12 supporting seams.25 use the existing generic VFX sorting (`WorldUI`,999);6 use the higher foreground pass (`WorldUI`,12000). This is19.35%, the nearest whole-ribbon split to20%. The split is distributed across the sequence. Layer names, orders, share, coverage, dimensions and lifetimes are editable on the action event. No global sorting-layer changes were needed.
- Actor anticipation/lunge → hidden barrage → return above the field using verified `New_sprite/Ch1_Jump.PNG` poses → plunge → the existing Dimension Cut animation/timing/copied MotionData. The initial trial capture revealed an old placeholder plunge-charge sprite; it was replaced before the attached final preview.
- Exactly19 barrage Primary contacts, then one final Primary contact. All20 are Low impact, keep Darkness/Void damage and permit crits. The hitbox's new default-false `suppressImpactFeedback` bypasses global hitstop and its critical minimum, so the existing hitstop-triggered camera impulse is not invoked. Generic contact sparks are separately suppressed. Other actions retain their defaults.
- The Passive4 / five-paid-Dimension-Cuts / one-use timed Skill gate is retained. The final cut is a second node of the same SkillAction; it consumes no additional cached cut and cannot increment the gate.

This uses two mesh-renderer passes and a procedural shader, rather than another baked slash flipbook or free-running Particle System. Per-ribbon shape/timing travels in UV channels; a frame property samples all ribbons. Stepped spatial sampling retains pixel edges, while scaled fractional-frame sampling keeps motion continuous between logic updates. The effect restores prior actor visibility on state exit, node replacement, invalidated execution and disable. No post-processing/bloom feature, package, build or camera-setting change is required for the rendered glow.

See the [reference study and starting values](revision-design.md). This is our interpretation of the supplied footage, not a claim about BBEEx's internal implementation.

## Concrete Preview

- [Updated silent live gameplay](dimension-slash-procedural-silent.mp4): two directions at actual captured cadence.
- [Phase board](procedural-phase-board.png).
- [Runtime trace](Runtime/runtime-report.json), [compact summary](verification-summary.json).
- [Automated tests](tests.xml).
- [File preservation](preservation-check.json) against [preflight](preflight.json).

The footage is real Unity rendering with the actual Player, enemy collider, damage pipeline, camera and authored input sequence. It has no recorded game audio. Raw readback replaces synchronous PNG encoding during play to reduce capture overhead; `build_review.py` converts those captures and preserves their measured timing in the review video. The readbacks are disposable validation data, not game assets.

## Verification

**22/22 focused MainDimensionSlashTests pass.** These include the existing gate/resource/QTE/ownership checks plus19+1 damage-budget windows, Low-impact suppression, final clip/MotionData reuse, forced crit retaining damage without global freeze, repeatable preview seed/visibility restoration, two sorting passes with25/6 triangle groups, and field bounds exceeding visible width/height.

Six live casts (both directions at requested30/60/120 FPS caps) each record:

-20 accepted Primary contacts:19 `DimensionSlash` contacts at24,27,...78, then one `DimensionSlashFinalCut` contact.
- Readiness after five paid cuts, one consumption, natural completion and restored actor visibility.
- Minimum `timeScale`1 throughout the recorded action. Critical contacts are present in the trace. No camera envelope or ScreenImpulse event is authored for this action.
-31 visual ribbons with6 foreground ribbons. Actor is hidden in the barrage interval and visibly returns before the copied final cut.

Separate live checks pass: pause preserves the ready timer, expiry resolves ordinary Skill, startup damage releases ownership, and disabling the player while the actor is hidden restores its prior renderer visibility.

Capture observed roughly25–37 FPS depending on requested cap. These are **not measured60/120 FPS performance passes**. The effect is procedurally interpolated, but recording/editor overhead still limits the supplied video. Final human smoothness review should include normal gameplay without capture. Profiling crowds, GPU overdraw, wider/narrower room layouts and alternate aspect ratios remains part of the larger QA matrix.

### Selected regression suites

| Fixture | Pass / total |
| --- | --- |
| MainDimensionSlashTests |22 /22 |
| CombatDamageTypeTests |18 /18 |
| AbilityUIFeedbackTests |7 /7 |
| ShadowMeterTests |61 /69 |
| PlayerActionInterruptTests |9 /10 |
| QteTeamSwapTests |20 /22 |
| Total |137 /148 |

The11 failing assertions are retained in raw XML, not reported as green:

- Eight current `ShadowMeterTests.CommittedStepRetargetsAfterCutWithoutAnIdleMovementTick` cases throw at line526. The pre-existing uncommitted fixture calls `GetField("movement", NonPublic)` although `PlayerCombat.movement` is public; the lookup is null before the assertion. This revision does not modify that fixture, PlayerCombat facing code or PlayerMovement.
- The existing startup-hitstun EditMode fixture still leaves built-in states uninitialized and expects null. The initialized real-prefab startup-damage check passes.
- Two QTE placement assertions still expect1-unit offsets while current authored Main/CameraGirl offsets differ. Main/CameraGirl QTE content is outside this revision and was not rewritten to satisfy stale expectations. Shared-session CameraGirl edits are retained.

No fresh pre-revision green baseline for those11 cases was established; this report classifies their inspected causes and does not claim the entire suite passed.

## Authoring and Preservation

Use `Tools/Project/Combat/Author Main Dimension Slash` to rebuild the action/prepare clip/material; it deliberately replaces this new action's manual tuning. The existing action editor now edits and scrubs the procedural event with the same renderer/evaluator. `Tools/Project/Combat/Capture Dimension Slash Runtime` runs the isolated capture and restores the original scene. Its temporary `playModeStartScene` override is restored afterward; this prevents unrelated retained-scene managers entering the simulation. No scene is saved automatically except the dedicated validation scene.

New code: `ProceduralSlashFieldSettings`, `ProceduralSlashField`, and `DimensionSlashRibbon.shader`. Integration appends one action-event enum value, adds scoped state ownership, and adds default-false direct-hit feedback flags. No crit-policy or resolver algorithm change is included.

The original Test_Playground is restored/loaded with22 roots. It received an additional shared-session save at00:38 Bangkok, including camera/activation values, and is currently clean in the editor. Those current scene edits are retained; its hash is therefore recorded as changed rather than claimed to match preflight. Inventory/progression, package and build/physics/timing hashes still match this revision's preflight. Existing dirty changes—including Main movement/facing and shared-session CameraGirl content—are retained. The new work is uncommitted on `codex/dimension-slash-procedural`; no mixed-owner commit, PR, push, merge or team publication was performed.

The concrete revision is ready for the user's visual/rhythm review. Full art/game-feel acceptance and the broader performance/transition/crowd matrix are not implied by these checks.
