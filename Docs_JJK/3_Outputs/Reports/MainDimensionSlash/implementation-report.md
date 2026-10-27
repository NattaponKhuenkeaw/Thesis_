---
slug: main-dimension-slash
status: in-review
source: manual
gdd_tags: [combat, visual-audio, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main Dimension Slash Implementation Review

Implemented 2026-10-27 after the user's design and architecture approvals. This is a playable first implementation for art/game-feel review, not a claim that the full38-case QA/performance matrix is accepted.

## Result

Main keeps the existing kit and gains the separate `main.skill.dimension_slash` SkillAction. At Passive Lv4, five accepted paid manual `main.dash.dimension_cut` root starts open a1.5 scaled-second Skill opportunity. The fifth start resets accumulation. Extra cuts while ready neither bank nor refresh it. A successful Dimension Slash start consumes readiness once. QTE intro, stale/missing execution, wrong owner/action/step and locked progression do not qualify. No additional AE/cached-cut cost or Dark Chain cooldown reset is introduced.

The finite84-frame step has six accelerating contacts at12/20/27/33/38/42, a quieter compression interval, and a giant final contact at54. Damage allocation is8% per burst and52% final, with action multiplier5; these are starting balance values. It uses Main's existing Dimension Cut damage type (`ElementType.Darkness`, the current Void-facing kit path) and retains that system's existing DOT behavior. Authored contact holds are2 frames on first/last burst and6 on final; the existing critical minimum can add holds. Actor, VFX and audio are independent tracks. There is no new teleport or invulnerability policy.

The new actor clip contains12 generated poses matching the regular white-jacket New_sprite direction. A dedicated exact-name Animator state at speed0 and the default-false `syncAnimationToActionFrames` step option make this clip follow the action clock. Captures verify final release `Actor_06`, camera peak and contact at logic54. Existing clips keep their current playback path.

The camera event uses generation tokens, captured execution/facing, an allocation-free envelope evaluator and a final Cinemachine extension. The peak is a4% size reduction with a small directional accent; it returns to base by66. Final corrected viewports are clamped for enabled axis-aligned BoxCollider2D bounds; unsupported geometry suppresses the contribution. Static-camera translation is suppressed. State exit, replacement, field invalidation, temporary focus, Ultimate, room change and disable release/suppress ownership through the existing manager/state paths.

Eight native PNG atlases and eight clips are imported under `Assets/Project/Character/Hero/test_0_Main/DimensionSlash/`. They use Point filtering, no mipmaps/compression, PPU26.25, binary alpha and at most18 opaque palette colors. Separate pooled VFX entries160–166 obey hitstop and action cleanup. Supporting trails were thinned after capture to stop them obscuring succeeding attacks. Existing confirmed-hit feedback and Main action SFX are reused.

## Review Artifacts and Reproduction

- [Live Unity footage, silent](dimension-slash-gameplay-silent.mp4): both target directions. Captured timestamps preserve observed gameplay cadence, hitstop and capture overhead. It is not a synthetic60fps animation or an audio recording.
- [Captured phase board](dimension-slash-gameplay-contact-sheet.png).
- [Runtime trace](Runtime/runtime-report.json): per-contact logic frame, damage/crit/time/positions; separately attributed other damage/DOT; target HP before/after; per-captured-frame actor sprite and camera size; readiness, consumption, completion, cleanup, pause/expiry/startup-damage checks.
- [Automated NUnit output](main-dimension-slash-tests.xml).
- [Compact final verification summary](verification-summary.json).
- [Pixel audit](pixel-import-audit.json), [source/import manifest](asset-production-manifest.md), [art brief/source notes](art-direction-prompt.md).
- [Protected-file checks](runtime-preservation-check.json) against [preflight](runtime-preflight.json).

In Unity, `Tools/Project/Combat/Capture Dimension Slash Runtime` creates an isolated additive validation scene using the real Player/Enemy_Dummy/Camera prefabs, Main clones and temporary Passive4 progression. It seeds five cached cuts, routes five real Attack inputs at legal recovery boundaries, then routes Skill. It runs both directions for requested30/60/120 FPS caps, logs actual observed rendering, records images, checks scaled pause/expiry and real startup damage, exits Play and restores the original scene root activation states. The target is a durable kinematic collider with AI/reactions disabled for repeatability. Player motion, actual collider queries, damage pipeline, FSM, VFX and camera remain live. This does not certify moving targets or crowds.

`Tools/Project/Combat/Validate Dimension Slash` runs the selected isolated fixtures. `Tools/Project/Combat/Build Dimension Slash Pixel Assets` reproducibly rebuilds this action's generated-source assets and authored data; running it replaces manual tuning of this new action. The build uses Unity Editor APIs and installs no tools or packages. The saved validation scene can be inspected separately.

## Executed Checks

| Fixture | Passed / total |
| --- | --- |
| MainDimensionSlashTests |19 /19 |
| ShadowMeterTests |61 /61 |
| CombatDamageTypeTests |18 /18 |
| AbilityUIFeedbackTests |7 /7 |
| PlayerActionInterruptTests |9 /10 |
| QteTeamSwapTests |20 /22 |
| Total |134 /137 |

New coverage includes Passive0–3 exclusion; five-count/reset; provenance/QTE/action/step/dead/stale exclusions; no bank/refresh/double consumption; real FSM five-charge spend and Skill routing; ordinary cooldown coexistence; finite seven-window budget/phases; camera rescaling, corrected viewport bounds, unsupported-room fallback, stale-token replacement and QTE/field invalidation.

The final live run records seven accepted Primary contacts at12/20/27/33/38/42/54 in each of six casts. All six open readiness after five paid cuts, consume once, end, and release camera. Separate `element-dot` / Dot events account for the additional HP loss; total recorded damage agrees with the durable target's HP change within0.026 HP (the target starts near1,000,000 HP, so float precision is visible). These ticks are not counted as extra slash contacts. Pause preserves readiness; scaled expiry returns the ordinary Dark Chain resolver result; actual startup damage enters hitstun and releases the camera. Unity reports zero errors after this capture.

These runs requested30/60/120 render caps but PNG readback/encoding limited observed rendering: the final trace measures16.9–19.1 FPS; earlier captures measured approximately20–24. They provide real collider/damage evidence under fixed-step catch-up, **not proof of sustained30/60/120 FPS performance**. Contact/frame data must not be relabeled with the requested cap as measured FPS. Cast wall time also includes critical holds and editor overhead.

### Three failing existing assertions

1. `PlayerActionInterruptTests.TakeDamage_DuringStartup_InterruptsIntoHitstun`: its EditMode fixture never initializes the FSM's built-in states, so `fsm.HitstunState` is null and the assertion expects null. The live prefab's initialized FSM passes the startup-damage check. The existing test was not rewritten or production interruption behavior weakened to satisfy it.
2. `QteTeamSwapTests.GroundedIntroPlacementUsesOutgoingOffsetAndMirrors` and `EntryUsesOutgoingFacingEvenWhenEnemyIsBehindAfterDashThrough`: expect a1-unit entry offset while Main's pre-existing authored QTE entry offset is3. This implementation does not change that value or QTE placement. They remain failures in the raw report; their expected data needs a separate fixture correction.

The original fresh79-test baseline covered Shadow Meter and damage only. It did not establish a fresh passing baseline for those three assertions, so the report does not call the whole regression run green.

## Preservation and Self-Review

- Original Test_Playground restored with22 roots; its current loaded state is retained. The final hash check detected saves to Test_Playground and PlayerRunInventory around20:39 Bangkok. The user explicitly confirmed: “Yes, keep the current files.” Those intentional shared-session saves are preserved, not reverted or described as unchanged. See `preservation-acknowledgement.json`.
- Saved meta progression, package manifest, Physics2D settings and TimeManager hashes are unchanged. Build settings also have no diff. No build, dependency or CI configuration is part of the feature.
- Existing dirty workspace changes were retained. Feature work is on `codex/main-dimension-slash`, uncommitted; no mixed-owner commit, push, PR, merge or team publication was performed.
- The new presentation never moves the player or writes the raw MainCamera transform/priority. Runtime sampling has no per-frame collections/LINQ allocations; the Editor-only capture deliberately allocates and is not a performance harness.
- Appended enum values preserve existing serialization. Missing camera/VFX/audio dependencies retain existing safe degradation paths. Direct-clip preview and camera-envelope editing use the custom action editor, Undo/dirty handling and shared evaluator.
- Actor source, VFX source and original New_sprite files remain separate. Pixel numerics and capture inspection do not constitute final artistic acceptance.

## Remaining Acceptance Work

The complete plan remains [the38-case QA matrix](../../TestPlans/main-dimension-slash-test-plan.md). This smoke pass only establishes the narrower evidence above. Before merge/final acceptance:

- Human compare normal-speed rhythm, impact, pose identity/body scale, negative space and camera with the reference. Evaluate the actual in-game sound; the attached video is silent. Final feedback values are starting values, not measured BBEEx internals.
- Profile uncaptured warmed gameplay at actual30/60/120 FPS, dense crowds, VFX pool growth, CPU/GPU/GC and draw calls against baseline. The capture cannot certify those budgets.
- Finish moving/edge/dead-target/crowd, airborne/platform/ledge, live HUD/input buffering/expiry boundary, legal cancellation at each phase, death/swap/QTE/scene transition, static/unsupported/wide/narrow room and authoring Undo/rescale checks. Unit checks and source inspection do not replace those full integration cases.
- Correct/classify the three existing fixture assertions separately, then rerun the affected regression suites.

The implemented action and concrete review footage are ready for the next art/game-feel review. No full acceptance or merge approval is implied.
