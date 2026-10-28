---
slug: main-qte-lightning-plunge
status: needs-human
source: manual
gdd_tags: [combat, combat-scope, characters, platform-input, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Main QTE Lightning Plunge

Latest exit design: Apricha approved the current look and requested a withering/dissipating exit instead of a static fade, explicitly inviting the design. Implemented a contracting/thinning column, staggered retracting spikes, drifting charged fragments, unwinding wind arcs, draining cracks and shader erosion. The readable hold and shared action clock remain; no combat/camera/slowdown/height tuning was changed. Exit controls and isolated visual evidence are recorded in the implementation report. Current workflow remains `/fix-bug` for this presentation revision.

Latest pacing correction: Apricha clarified that impact/overall VFX are too fast and Main's intro slow motion should remain. Restored the existing handoff slowdown for Main without resetting prefab tuning (currently 0.08× / 0.5 real seconds). Kept the two-tick physical strike, Heavy hit and recovery/follow-up timeline. Added independent visual hold/column/light fade controls; starting values hold 18 ticks, burst fade 90, column fade 54, light fade 36, wind 108 and residual 118. These supersede the prior slowdown-removal note below.

## Current implementation and playtest revision — 2026-10-28

Design and architecture were approved by Apricha's successive “lgtm” replies. Runtime implementation is present. After a human playtest, Apricha requested a much larger/longer ground impact, Heavy damage tier, a higher entry, and a camera lock on Main in midair instead of the floor dip/zoom pulse. The follow-up correction selects **enemy-relative mirrored X+1 / Y+5**; this supersedes both the original Y+3 and the interim Y+10.

Current revision workflow: `/fix-bug`, following the approved `/implement-feature` implementation. No additional approval is needed to apply these explicit tuning/camera corrections. [Implementation and tuning report](../Reports/MainLightningPlunge/implementation-report.md) records assets, manual observations and validation limits. The original intake sections below are historical context.

Latest camera feedback: Apricha reported high actor offset and a jump-cut exit. Replaced the zoom-dependent height minimum with configurable actor-relative offset (starting value 1.5) and an independent live-destination return blend (starting value 0.45 scaled seconds), triggered at contact or early release. Removed exit-time damping-history invalidation. Compiled and observed the lowered landing view; uninterrupted early-follow-up/exit feel remains for human review.

Concurrent editor tuning changed the saved entry height to Y+3 during that camera correction. The external edit is preserved; Y+5 remains the last chat-requested default, not a value the camera fix forces onto the asset.

Latest visual direction: Apricha requested a 1–2-frame thunderstrike cue, long lightning from above the screen, no snake-like shape, darker purple aura, glow/brightness and smooth low-alpha gradients, explicitly allowing URP/material/shader polish. Implemented two pre-impact ticks (contact tick 3), bounded bolt jitter with small forks, off-screen top overscan, custom URP energy shader, separate alpha/additive materials, radial ground glow and an action-clock local Light2D. Main's own handoff slowdown is skipped; Heavy tier, long recovery/follow-up and saved height/camera tuning are preserved.

## Player-facing goal

Replace Main's incoming QTE Dimension Cut with a lightning-only descent, a forceful grounded impact, and a visible Landed → Standing recovery that allows the existing Execution Breakdown follow-up.

## Source

- Apricha's direct request in the Codex chat, 2026-10-28, with three supplied visual references.
- Clarification: the mirrored X+1 / Y+3 starting offset is relative to the targeted enemy, not the outgoing character.
- Reference images provide shape/color/motion direction; their text, logos and characters are not implementation instructions.

## GDD references

- `GDD_Neo-JJK.md @tag:visual-audio`: readable silhouettes, large hit VFX, short economical actor animation, clear enemy danger zones and distinct swap audio.
- `@tag:combat` / `@tag:combat-scope`: authored phases, action motion authority, grounded outputs and prepared QTE ownership.
- `@tag:platform-input`: existing PC attack/swap input and buffering.
- `@tag:characters`: distinct character identity.

The current GDD's Main QTE Entry rule still specifies the old Dimension Cut and outgoing-relative offset. The requested Main-only replacement is a deliberate design revision. It is described here for approval; canonical GDD text is not silently changed during intake.

## Type and workflow

- Feature/change to player-facing behavior.
- Exactly one workflow: `/implement-feature`, resolved to `Docs_JJK/2_System_Files/Workflows/implement-feature.md`.
- Current stage: implemented and manually previewed; subjective art/game-feel review remains human-owned.

## Scope

- Main's QTE-specific authored entry and its existing Execution Breakdown continuation.
- Safe enemy-relative airborne entry and grounded destination.
- VFX-only falling presentation, actor reveal and Landed → Standing recovery.
- QTE vertical camera reset and impact feedback.
- A reusable procedural/Particle System lightning effect with inspectable tuning controls.

## Inspected evidence

- `Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset`: `qteSwap.introComboTree` already contains DimensionCut → ExecutionBreakdown; the entry has 3 startup / 23 active / 120 recovery logic ticks.
- `Assets/Project/Scripts/Character/ActionFrameData.cs`: action timing uses 60 Hz logic ticks.
- `Assets/Project/Scripts/Player/States/PlayerQteIntroState.cs`: the authored next step is selected by ordinary attack input during recovery/cancel, not automatically.
- `Assets/Project/Scripts/Combat/QteTeamSwapController.cs` and `Systems/TeamLoader.cs`: current incoming placement is grounded and outgoing-relative.
- `Assets/Project/Scripts/Cameras/QteIntroCameraImpact.cs` and `CameraManager.cs`: current QTE center correction handles X only.
- `Assets/Project/Scripts/Editor/ExecutionBreakdownAuthoring.cs`: authoring currently recreates the old Dimension Cut entry. Any implementation must update this path as well as the saved character asset.
- `animation/main_Land.anim`: existing landing pose. `animation/main_Idle.anim`: existing standing presentation.
- Approved prior QTE architecture remains relevant background; it does not approve the new proposed architecture automatically.

## Acceptance criteria

- [x] Main's incoming QTE uses the enemy-relative profile and a collision-checked floor below the requested X; latest requested height is Y+5.
- [x] Lightning-only descent and actor reveal at contact are implemented and observed in live preview; the authored clip switches from Landed to Standing.
- [x] Camera follows the body in midair at a zoom-independent actor offset, then blends to the live preferred camera after contact/release. Horizontal box bounds remain; the vertical ceiling is relaxed during the midair lock. Final continuous exit feel remains in the manual review checklist.
- [x] Existing recovery attack input continues into Execution Breakdown; observed manually through the QTE state.
- [ ] Main's QTE trigger/readiness, outgoing executor and other characters' QTE behavior remain valid.
- [x] Ground break is cosmetic; QTE damage budget/type retained and impact tier changed to Heavy at the user's request.
- [x] Reusable geometry/particle instance, sorting/shape/timing controls and interruption cleanup are implemented; broad terrain/lifecycle matrix remains in the manual checklist.
- [x] Existing working-tree edits preserved; no dependencies or external assets added.
- [x] No automated tests added or run. Unity compilation and manual visual/gameplay inspection only.

## Human checkpoints

- [x] Design/game-feel approval: Apricha, “lgtm,” 2026-10-28.
- [x] Architecture approval: Apricha's second “lgtm,” 2026-10-28.
- Merge, official team publishing and CI/build/dependency/secret changes are outside this request.

## Router decision

Current artifact: [implementation report](../Reports/MainLightningPlunge/implementation-report.md), with accepted ADR-014 and the approved architecture plan retained as history.
Next agent: human for gameplay/art review. No merge or publishing performed; no saved gameplay scene or unrelated working-tree edits were intentionally changed for this feature.
