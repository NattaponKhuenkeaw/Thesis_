---
slug: responsive-movement-flow
status: needs-human
source: manual
gdd_tags:
  - platform-input
  - combat-scope
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Responsive Movement Flow: Implementation Result

Implemented the design approved with "lgtm" on 2026-11-03 within the existing state-owned direct-velocity motor.

## Behavior

- All three movement profiles use immediate ground/double-jump launch and zero landing movement recovery.
- All nine character DashData definitions use zero Startup. Enabled absolute Cancel frames shift by the removed Startup, preserving their Recovery-relative boundary.
- Ground and air Dash Recovery return steering, facing, and normal gravity. Neutral A/D no longer holds vertical velocity at zero. Following the user's animation bug report, an unfinished neutral Dash clip can finish independently of gameplay Dash; new movement/actions may replace it immediately.
- Dash remains unavailable throughout its existing state timeline; actual exit still starts the unchanged authored cooldown once. Active curve, burst duration/speed, Recovery duration, action interrupt categories, and air-dash resource policy retain their existing authorities.
- Ordinary authorized facing reversals have no extra turnaround cooldown.
- Valid buffered landing Jump/Dash can execute after contact/resources and feedback publish. The Landing state no longer imposes an extra zero-recovery motor tick.
- Jump remains fixed-arc and press-triggered. No jump cutting, hold-height, glide, or SlowFalling system was added.

## Validation

- Unity 6000.3.4f1 compiled runtime and editor assemblies; no C# compiler errors were returned by the console query.
- Twelve isolated native checks passed, including three tests using Goenitz's real Animator controller, with zero failures: [results](native-checks.json). The two neutral animation cases failed before the fix: [reproduction](dash-animation-before-fix.json).
- Twelve tuning-field checks passed: [asset preservation](asset-preservation.json). Goenitz's concurrently changed dash speed of 35 was preserved; the task's asset writer changed only jump/landing timing and dash startup/cancel fields.
- Scoped diff whitespace check passed.
- Temporary preview-scene validation did not replace/save the current scene or operate the live player. It supplies scripted contact state and explicitly advances the real Goenitz Animator; it does not establish room collision behavior, rendered sprite quality, controller parity, or subjective feel.

## Review and Remaining Work

Code review checked immediate-launch double commitment, first motor tick before dash frame advancement, separation of continuous input gates from discrete cancel permissions, landing feedback ordering, and cooldown idempotence. The native checks cover these state/motor outcomes. Subjective feel and the collision/presentation scenarios in [the playtest plan](../../TestPlans/responsive-movement-flow-test-plan.md) remain for human review.

The existing `core_character` checkout was reused, preserving unrelated work. No scene, global physics/build/dependency setting, commit, merge, or publication was performed by this task.
