---
slug: main-meter-max-cancel
status: draft
source: manual
gdd_tags: [combat, characters, platform-input, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Completion audit — 2026-10-29

The current user-approved design is the **small Action Type override**, not the withdrawn unified
catalog/executor/cost proposal. Input, Category, AE/cooldown costs and HUD/window routing remain.
Dimension Slash is a zero-AE Skill-input action with Ultimate interrupt and white-hit-flash type.

## Native evidence

- Focused Unity integration suite after presentation/lifetime follow-up: **155 passed, 0 failed** (2026-10-29 16:17:56 UTC). Includes all MainMeterMaxCancelTests and ActionVfxLifetimeTests,
  ShadowMeterTests, QteTeamSwapTests and selected Skill-window/consumption cases.
- Eight new hold-buffer tests exercise keyboard state, charging during Active, delayed Recovery MAX CANCEL, release/tap expiry, one-shot consumption, focus/disable/character/action-map invalidation and clearing by another consumed input. See `Specs/held-ultimate-buffer-task-card.md` under the output folder and `MainMeterMaxCancel/held-ultimate-buffer-summary.json`.
- The subsequent presentation/lifetime change adds fifteen cases: thirteen animation/lifetime checks and two real state-exit/MAX CANCEL procedural handoffs. See `max-cancel-presentation-validation.md` and `MaxCancelPresentation/` for the new GNF animation preview and current native summary.
- Broader combat suite: **186 passed, 7 failed**. The exact failures are listed in the JSON summary.
  Six involve authored presentation expectations/test setup, one startup-hitstun behavior. No baseline
  execution was performed, so these are not asserted to be pre-existing or resolved.
- Runtime/Editor compilation passed; native Unity compiled and executed the updated fixtures.
- A native camera render of the MAX CANCEL UI was generated and visually inspected. Gold text and
  red line are legible on the captured background, with no inherited left-facing rotation.
- Resolver managed-reference payloads match HEAD exactly. Only descriptors/configuration changed;
  Dimension Slash remains in skillActions, actionType=Ultimate, cooldown=0, with no AE-cost field.

## Requirement audit

| Requirement | Evidence | Remaining |
|---|---|---|
| Per-action CharacterObject meter gains; Execution 5, named low actions 1 | Native rule/gain/cap/Void-area tests pass; six passive gain records removed | Human tuning review |
| QTE grants tag plus additive cached cut; paid spend contributes to Slash | Native committed-intro, additive cache, manual spend/count/window and fallback-no-reward tests pass | Live human combat acceptance |
| Five-second Dimension Slash offer on Skill | Native resolver, real FSM spend/Skill consumption and cooldown/window tests pass | Human HUD/feel review |
| CameraGirl Skill/EX actually starts Main QTE and lands | Both accepted-hit variants pass native entry identity, captured motion, reward and landing checks; safe-alternative and lower-platform cases pass | Confirm the user's original play scenario feels correct |
| Recovery MAX CANCEL, including Skill-bound Ultimate type | Native tests pass across Skill/Ultimate input routes, with source commitment/opt-out and destination cost/cooldown rejection | Human timing/readability review |
| Action Type does not add AE cost/change input or damage category | Native zero-AE Skill execution, category and snapshot tests pass; existing collections/states retained | None for the scoped code behavior |
| White hit flash follows Action Type | Native execution/clone presentation snapshot tests pass; existing PlayHitFlash branch uses that flag | Human in-combat flash intensity review |
| MAX CANCEL callout | Native render/label/raycast/facing test passes; rendered image inspected | Human stylistic approval |

The native run exposed and fixed two additional defects in the active goal: QTE support probes
stopped above a lower platform because enemy origin was treated as its feet; meter matching could
let a colliding display name override an explicit action id. The new regression cases pass.

## Artifacts

- [Native summary and exact broader failures](MainMeterMaxCancel/native-validation-summary.json)
- [Native MAX CANCEL render](MainMeterMaxCancel/max-cancel-preview.png)
- Full focused/broader NUnit XML: `Temp/main-meter-qte-max-cancel-tests.xml` and
  `Temp/main-dimension-slash-tests.xml`.
- Earlier managed checks: 15 actual-code checks passed; narrower than native integration coverage.

The full goal remains active because broader regressions and the required human gameplay/feel
checkpoint remain unresolved. The prior MCP/UI access failures are historical: the Unity MCP
connection recovered, and the native tests above executed successfully. No test job is still running.
