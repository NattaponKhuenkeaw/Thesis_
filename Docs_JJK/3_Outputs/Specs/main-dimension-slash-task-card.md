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

## Current Revision — 2026-10-28

Expanded refraction request: enable on ordinary DimensionCut's primary sprite VFX, Lightning Plunge's rendered bolt, and Execution Breakdown's six primary burst sprites plus its existing final tear. VFXEntry has optional cut-impact settings sampled from current transformed sprite bounds; the plunge supplies its sampled bolt endpoints and onset. Default plunge tuning: 12 frames, band width0.04, refraction4 reference pixels, RGB split0 and blur0. The pipeline default Volume has chromatic intensity0.1; this is a possible contributor to the reported lightning fringe. During the plunge's visible presentation the feature suppresses chromatic aberration only in the camera's evaluated Volume stack and restores it after that camera renders. No saved Volume profile changes. Compilation/saved-data checks only; no gameplay tests or captures.

Barrage refraction correction: user observed the effect on the finishing cut but not Dimension Slash. The barrage's saved impactDistortion.enabled was false, and the authoring utility selected only the two finishers. Enable it on the DimensionSlash barrage too, both in CreateStep and the targeted impact authoring utility. The existing runtime follows the latest visible primary ribbon and restarts its envelope from that ribbon's generated start, so each new barrage slash supplies the moving distortion band. No gameplay tests or captures are run for this correction.

Latest user correction replaces the hitbox-centered circular distortion with a band bound to the actual rendered cut. Ribbon metadata supplies the randomized angle, position, length, reveal direction, shrink and opacity; the screen-mask tear supplies its current diagonal, screen position and narrowing opening. Distortion starts with the visible cut. Ground offset/radius authoring is removed. Ultimate activation SFX gains are editable on both UltimateAction and the activation timeline event; the action gain multiplies the event gain in normal playback. Both default to 1. Compilation/import checks only; no gameplay tests or capture requested.

Cut-impact follow-up: add a short scene-refraction shockwave with localized chromatic separation and radial blur to DimensionSlashFinalCut and Execution Breakdown's final tear. Optional settings stay under the existing Procedural Slash Field event. A gated stock URP fullscreen pass samples the 2D scene during the pulse. [Rendering decision and starting tuning](../ADRs/015-action-owned-cut-refraction.md). Preserve authored data and avoid tests/captures per the user's instruction.

Latest window correction: Apricha explicitly requested a **5-second** Dimension Slash/Judgment Cut opportunity. Saved ready-tag duration, authoring utility and player descriptions now use 5 seconds, superseding the historical 1.5-second window. The fifth paid Dimension Cut opens the window; using Judgment Cut consumes it. [Window/policy fix](qte-window-policy-fix-task-card.md).

Facing/angle follow-up: both ribbon geometry and the fullscreen transparency tear mirror with the player's facing captured when the event starts. The world field stays anchored after launch. Animation Editor exposes a min/max ribbon angle range in degrees (equal endpoints fix the angle) and a separate fullscreen cut angle. Angle selection stays inside the authored range. No gameplay tests or captures are run for this follow-up, per the user's instruction. Compilation/domain reload completed; the console also logged a disposed SerializedProperty exception from AnimationEditorWindow.CleanupPreview → AnimationMode.StopAnimationMode during OnDisable. That editor cleanup issue has not been changed by this follow-up.

Latest user corrections: keep Judgment Cut world-anchored, enlarge it30%, and widen the generated size range so thin cuts coexist with substantially larger accents. Reuse the original seven-contact sprite sequence as **Execution Breakdown**, replacing only its final upper-cut VFX with a screen-length foreground transparency tear through a white overlay. After the user's architecture correction, this is a QTE combo: Dimension Cut → NA during authored Recovery → Execution Breakdown. Starting Recovery is120 logic frames; no standalone action, passive ready tag or custom long input queue is needed. An optional generic QTE entry ComboTree feeds the existing ActionResolver, and the Animation Editor exposes both steps under **QTE Entry Combo**.

Latest size/speed follow-up uses range3–46 with1.8-diagonal viewport coverage; five slashes cross the viewport with both endpoints offscreen. Inter-cut spacing is2 frames, reveal1–3 frames, and decay is restored to16–27 frames. Foreground sorting now receives50% of the slash ribbons. This tuning has not been recaptured yet.

Workflow: `/implement-feature` within the approved action architecture. [Current routing and presentation](../Reports/MainDimensionSlash/Revision3/design-and-routing.md); [implementation, videos and verification](../Reports/MainDimensionSlash/Revision3/implementation-report.md).28 focused tests pass. Live capture confirms20 hits per Judgment Cut and7 hits after QTE Recovery NA, with cached cuts preserved. The following procedural revision and original research are retained as history.

### Prior procedural revision

The user rejected the first implementation's reference likeness/smoothness and requested a procedural/Particle-System-style purple Judgment Cut field: independent sliding-alpha reveal, luminous leading edge, then shrinking width/height; varied size/length/timing/direction; almost full-screen coverage; approximately20% foreground-depth cuts and80% normal VFX sorting. Actor anticipation/lunge → hidden barrage → reappearance/plunge → existing Dimension Cut sprite/motion. About20 Low-impact hits, without screen impulse. The user corrected a typo and confirmed critical hits do not need an opt-out; they remain enabled.

Workflow for this revision: `/fix-bug`, with the earlier research/design packet retained as history. [Concrete revision and validation direction](../Reports/MainDimensionSlash/Revision2/revision-design.md); [implemented result, updated gameplay video and test evidence](../Reports/MainDimensionSlash/Revision2/implementation-report.md). Existing Passive4/five-cut/Skill gate remains; latest direct user corrections supersede the old seven-hit sprite choreography.22 focused tests pass and six live casts register20 contacts each, with no global freeze and correct actor cleanup. Human feel review is the current stage.

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
