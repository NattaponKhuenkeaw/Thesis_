---
slug: main-meter-max-cancel
status: approved
source: manual
gdd_tags: [combat, characters, platform-input, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main meter, QTE reward, Skill follow-up and MAX CANCEL

Workflow: `/implement-feature`. Source: active user goal in this chat, 2026-10-29.
Previous swap-fallback work was progress but does not verify the requested QTE intro.

## Requirements and design

1. Author per-action accepted-hit meter gains in CharacterObject, outside universal resolver/passive gain lists.
2. Execution Breakdown gives 5/hit. Starting values: Dimension Cut/Slash and Jailbreak Punisher 1/hit;
   unlisted actions (including other Ultimates) 0. Explicit rules preserve Ground/Air 2/hit, Attack3 4, Attack6 6, and Dark Chain 8.
   Test: Execution Breakdown is the strongest named generator per hit; low actions must not dominate
   its meter role in a single-target rotation. If they do, lower those rules before raising Execution gain.
3. Main's committed QTE intro grants a runtime tag and one additive cached cut. Spending it uses
   the existing manual Dimension Cut path and contributes toward the five-cut Dimension Slash unlock.
   Rejected preparation/ordinary swap grants nothing. Consumption removes the marker; no expiration
   or extra stack cap is invented for the existing cached resource.
4. Move the existing five-second Dimension Slash availability/action from Ultimate to Skill.
   Preserve the current combo payload, stable action id and references; update UI gates/authoring/tests.
5. Investigate CameraGirl Skill/EX → Main: prove preparation, commitment, intro execution and landing;
   preserve safe ordinary fallback when no valid QTE is possible. Check terrain and target changes.
6. Recovery of an Ultimate accepts a different Ultimate (MAX CANCEL), obeying target/action conditions,
   its own cooldown, resource/AE cost, hold/tap selection and authored opt-out. Source cooldown must
   not veto the distinct destination. Startup/Active and rejected requests do not cancel or show feedback.
7. Show a transient gold MAX CANCEL label and horizontal red/gold accent inspired by the supplied
   reference. Use the next action's existing audio; no new sound asset is invented.

Response/clarity: readiness must lead to the correct action; MAX CANCEL fires only after commitment.
Fit/satisfaction: quick readable fighting-game callout, preserving the underlying attack presentation.
Motivation: Execution Breakdown becomes Main's distinct refill route.
State exit/cleanup: existing action cleanup owns old motion, hitboxes, invulnerability, camera and freezes;
new ultimate restarts Startup. No active-phase bypass or same-action reset loop.

## Implementation boundaries

Extend CharacterObject with serializable meter gain rules and QteSwapDefinition with a committed
entry grant. Reuse CharacterPassiveController's counter state, multipliers and cap. Reuse the
Ultimate state's existing replacement resolver, with scoped source-cooldown exclusion.
No dependency/build/CI/secret changes, publishing or merge. User request authorizes these mechanics;
final tuning, architecture review and live feel remain human checkpoints.

## Verification

Add meaningful rule, grant/consumption, Skill window, QTE session and MAX CANCEL success/failure
regressions; audit serialized payload/reference preservation; compile runtime and Editor assemblies
in isolated Temp outputs. Execute focused Unity tests if an accessible live runner is available.
Keep the full goal active until every requirement is verified; compilation alone is insufficient.

## Current evidence — 2026-10-29

The user narrowed Action Type to interrupt policy/MAX CANCEL and Ultimate white hit flash.
Dimension Slash remains Skill input/window and zero AE, with an explicit Ultimate type override.
The broader catalog/executor/cost migration proposal was withdrawn. Existing categories, costs,
input collections and player states remain. See `action-type-input-decoupling-task-card.md`.

Unity is now connected. Native focused tests after the user-requested held-input and presentation/lifetime improvements: **155/155 passed**, including gains/cap/Void multiplier,
QTE intro/landing/reward/fallback, Skill-window consumption, cross-input MAX CANCEL, source
restrictions, zero-AE Skill execution, hit-flash type snapshots and native callout rendering.
A lower-platform QTE probe error and stable-id/display-name meter-rule collision were corrected.
All resolver managed-reference payloads are preserved.

Charged hold Ultimate input now remains buffered while physically held, then returns to the existing short timeout on release. It is consumed once, with all policy/cost gates intact. Eight new keyboard/gesture/FSM cases pass; see `held-ultimate-buffer-task-card.md` for the follow-up `/fix-bug` scope.

The GNF pixel MAX CANCEL sequence and independent spawned VFX lifetimes are implemented, with fifteen additional animation/lifetime/state-exit cases passing. Native preview and exact scope are documented in `max-cancel-presentation-task-card.md` and `../Reports/max-cancel-presentation-validation.md`.

Broader suite: **186/193 passed, 7 failed**, in presentation assumptions and a startup-hitstun test.
These remain unresolved; no baseline execution is claimed. Human gameplay/feel review remains.
The full active goal is not marked complete. Evidence and exact failing names are recorded in
`../Reports/main-meter-max-cancel-validation.md` and its native summary artifact.
