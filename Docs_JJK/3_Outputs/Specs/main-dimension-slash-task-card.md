---
slug: main-dimension-slash
status: in-review
source: manual
gdd_tags:
  - visual-audio
  - combat
  - platform-input
  - inspirations
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Main Dimension Slash

## Player-Facing Goal

Make a NEW standalone action for CharacterObject_main, unlocked by a Potential, evoke the supplied BBEEx reference: multiple slashing bursts followed by a giant Dimension Cut, with deliberate impact, rhythm, and camera motion. Keep actor sprites and slash VFX as separate authored layers, with high-quality pixel-art VFX and actor consistency based exclusively on test_0_Main/sprite/New_sprite. Preserve the existing Ultimate and cached-resource Dimension Cut dash.

## Source

- Origin: user's direct request, 2026-10-27.
- Reference: `E:/Drive_E_Download/dimension-slash.mp4`.
- Requested by: project owner.
- User clarification: standalone action, intended to be unlocked by a Potential; no replacement of an existing action is requested.
- User design approval: “LGTM, Start process”, 2026-10-27.
- User activation requirement: unlock on **Passive Lv.4**; performing **five Dimension Cuts** opens a timed follow-up on the **Skill command** to execute Dimension Slash.
- User art clarification: new actor sprites may be generated to mimic the reference motion; use existing New_sprite for visual consistency, not mandatory frame reuse.
- Current stage: approved design advances to architecture and asset-production preparation; architecture approval remains required before runtime implementation.

## GDD Reference

- `@tag:visual-audio`: pixel-art identity, VFX, combat presentation, and audio rules.
- `@tag:combat`: existing authored action semantics and ultimate behavior.
- `@tag:platform-input`: PC-first input and action response.
- `@tag:inspirations`: BBEEx inspiration and design boundaries.

## Type

- [x] Feature (research/design stage).

## Scope

### Systems Affected

- Main character authored action data and ActionResolver (inspection only).
- Action events, VFX, camera feedback, time/hitstop, audio, and cancel cleanup (inspection only).
- Separate actor sprite and VFX production plan.

### Files To Inspect First

- `Assets/Project/Scripts/Combat/ActionResolver.cs`
- `Assets/Project/Scripts/Character/ActionResolverTable.cs`
- `Assets/Project/Scripts/Character/ActionEventFrameData.cs`
- `Assets/Project/Scripts/Character/AttackStepData.cs`
- `Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset`
- `Assets/Project/Character/Hero/test_0_Main/ActionResolverTable_main.asset`
- `Assets/Project/Character/Hero/test_0_Main/sprite/New_sprite/`
- `Assets/Project/Scripts/Cameras/`

### Out of Scope

- Runtime edits before the architecture checkpoint; build/dependency/secret configuration changes; unrelated working-tree edits.
- Treating any actor sprites outside New_sprite as visual direction.
- Copying reference game artwork or claiming knowledge of its internal implementation.
- Final damage balance, resource changes, or selecting a replacement action without a human decision.

## Acceptance Criteria

- [x] Inspect and timestamp the local reference; distinguish observation from inferred technique.
- [x] Research primary sources on impact, animation rhythm, camera, and pixel presentation.
- [x] Map design needs to verified existing ActionResolver/event/feedback capabilities and gaps.
- [x] Document a phase timeline with separate actor, VFX, camera, time, damage, and audio tracks.
- [x] Label all proposed tuning values as starting values and attach pass/fail checks and adjustment directions.
- [x] Recommend a practical authoring toolchain based on inspected assets and available tools.
- [x] Include state, interruption, readability, frame-rate, and repeated-cast validation.
- [x] Present a reviewable design and the smallest unresolved design decisions before implementation.

### Implementation and Verification Status

- [x] Passive Lv.4 preserves its existing effects and enables five paid Dimension Cut starts to open the Skill follow-up; unit and real-prefab smoke checks pass.
- [x] Ready Skill executes Dimension Slash once; locked/expired state falls back under existing rules. Live scaled expiry/pause and real FSM entry checked.
- [x] Authored six accelerating burst contacts then giant final cut through ActionResolver, with separate actor/VFX/audio/contact tracks.
- [x] New actor poses and separate native pixel effects imported; measured palette/alpha/import contract validated. Artistic acceptance remains below.
- [x] Scoped camera and impact delivered; token/field/QTE exclusions, bounded correction and live startup interruption checked. Full scene/QTE/crowd matrix remains in QA report.
- [x] Selected Unity checks, actual collider contact trace and live gameplay capture completed; evidence and limitations documented.
- [ ] Complete human game-feel/art review on the implemented result; no implementation completion is claimed by the research/architecture packet.

## Human Checkpoints

- [x] Design/game-feel approval: user “LGTM, Start process”, 2026-10-27, including Passive Lv.4 / five Dimension Cuts / Skill follow-up clarification.
- [x] Architecture approved by the user: “LGTM, arch approval”,2026-10-27.
- [ ] No merge or official team publishing in this phase.

## Router Decision

Workflow: `/implement-feature`, implementation review after approved architecture.

Current artifact: `Docs_JJK/3_Outputs/Specs/main-dimension-slash-task-card.md`.

Next agent: human. Implementation is available on `codex/main-dimension-slash` for art/game-feel review. See the implementation report for executed checks and remaining QA coverage; this is not a full acceptance or performance claim.

Next artifact: `Docs_JJK/3_Outputs/Reports/MainDimensionSlash/implementation-report.md`.

Review packet:

- `Docs_JJK/3_Outputs/ADRs/013-action-owned-camera-presentation.md` — accepted scoped camera ownership.
- `Docs_JJK/3_Outputs/TestPlans/main-dimension-slash-test-plan.md` — full38-case plan; implementation report records executed coverage and remaining cases.
- `Docs_JJK/3_Outputs/Reports/MainDimensionSlash/asset-production-manifest.md` — actual production assets and historical proposal.
- `Docs_JJK/3_Outputs/Reports/MainDimensionSlash/dimension-slash-gameplay-silent.mp4` — current live Unity footage in both directions, without recorded game audio.
- `Docs_JJK/3_Outputs/Reports/MainDimensionSlash/dimension-slash-art-direction-v1.png` and `dimension-slash-vfx-direction-v2.png` — generated direction plates, not final animation atlases.
- `Docs_JJK/3_Outputs/Reports/MainDimensionSlash/art-direction-prompt.md` — prompts and art QA limitations.

Context pack: project-stack, relevant GDD tags above, this task card, implement-feature workflow, game-design-agent prompt, relevant source/asset evidence, supplied video, and primary-source research.
