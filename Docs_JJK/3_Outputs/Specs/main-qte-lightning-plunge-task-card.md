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

Ground shockwave rendering repair (2026-10-29) — supersedes disabling it: Apricha explicitly requested fixing the ground wave, not hiding it. Restored Ground Shockwave Enabled=true in Main's saved asset and class default. The shared procedural energy shader no longer samples the per-renderer sprite _MainTex: mesh/particle geometry already supplies UV/color and does not need an external texture, eliminating a potentially stale atlas-texture dependency. Alpha is saturated and emitted RGB is nonnegative. This removes a plausible colored-artifact path; it is not a live reproduction proving that binding was the sole cause of the reported green/black ring.

Rebuilt Arc as a joined ellipse ribbon with analytic edge normals shared between segments, clamped nonnegative taper and explicit zero-width endpoints. This avoids independent overlapping/turned wedges at flat ellipse tips. WindArcs uses the existing soft additive glow material in runtime initialization, prefab and authoring. The expanding ground ring, wide ground glow, lightning, large explosion, purple accents and existing exit motion are all retained. Damage, timing, camera and placement are unchanged.

Validation: Runtime and Editor C# compilation using Unity's cached response files and bundled Roslyn, with output DLLs only in OS temp; no game assemblies overwritten. No automated tests added or run. Unity MCP remains unavailable on localhost:8080; both Windows UI tool entry points also failed to initialize (missing kernel-assets path). Therefore shader import/GPU rendering and the exact Game-view reproduction remain unverified. Manual acceptance: reimport in Unity and repeat the Main hit; ring must visibly expand and fade purple with no green/black band. Do not treat the prior disable pass as resolution.

Ground-ring isolation correction (2026-10-29): Apricha identified the expanding ground-impact/dust-wind circle as the suspected green/black artifact. Added Ground Shockwave Enabled and saved it OFF for Main. This gates both the procedural WindArcs rings and wide GroundGlow pool, while leaving the broad jagged blast, falling bolt, purple electrical arcs/halos, sparks, local light, damage and timing unchanged. The radial glow layer remains available for tiny electrical tip glows; no wide ground pool is submitted. Cleared/uploaded geometry each sample prevents an old ring mesh from staying visible.

The preceding actual-hit investigation also found the generic Heavy contact sprite had RGB fringes baked into VFX_Hit_2.PNG. Main's root hitbox now has suppressContactVfx=true; the existing HitFeedbackResolver returns only after hit audio, so Heavy damage/reaction/audio/hitstop remain. Authoring an existing root now preserves this suppression. A live hit capture before/after showed the generic sprite removed and camera chromatic intensity at zero, but did not reproduce the exact green/black ground ring.

Current validation limit: Unity MCP is unavailable at http://127.0.0.1:8080/mcp, so this ground-ring change is statically inspected only; no fresh compilation or Game-view reproduction is claimed. No automated tests added or run. Manual acceptance: repeat the same Main plunge hit and verify the expanding ground circle is gone while the blast/electricity remain. This is a scoped isolation of the user-identified layer, not proof of the remaining artifact's shader cause.

Latest additive electrical accents: Apricha requested purple glowing lightning/sparks around both falling and impact, with two supplied reference images. Added short seeded angular arc pulses, occasional small forks, soft additive purple halos and sparse falling Particle System sparks. The approved broad explosion geometry/exit, placement, action timing and disabled chromatic/refraction values remain intact. Arcs are secondary and thin out with the existing exit rather than replacing the blast with sparkle geometry. All arcs use cached mesh layers (GroundCracks for electrical cores, existing envelope/halo/radial glow); no new materials, prefab objects, packages or external assets.

Starting controls under Plunge > Visuals > Purple Electrical Accents: Falling Arc Count 6, Impact Arc Count 9, Electrical Spread 1, Arc Length 0.8, Width 0.028, Glow 1.2, HDR purple color (0.95, 0.28, 2.4, 1), Falling Spark Count 8. Tune count/spread first: reduce count if accents compete with the explosion/silhouettes, increase Glow for a broader visible aura, and keep the two-tick falling/contact timing intact. Arc pulses and particle simulation use action time, including pause/rewind and impact-crossing frame skips.

Unity compiled. Sequential isolated rendering succeeded; inspected frames 0 (falling), 15 (impact) and 54 (tail) showing purple arcs around the main shapes. The first standard phase capture hit an existing Windows mapped-file lock on phase-000.png; used the separate motion-capture output and saved fresh electrical-falling/impact/tail.png plus electrical-motion.gif. The existing unrelated editor NullReferenceException remains; no full-game runtime validation claimed. No automated tests added or run.

Latest broad-impact correction — supersedes the thin ribbon/wisp revision: Apricha prefers the earlier large explosion and requested removal of the colored/chromatic artifact. Restored the broad irregular starburst: layered dark-purple/near-black angular lobes, bright white/violet branching energy and the existing long column. The exit opens the silhouette from contact outward while lobes drift laterally/upward and fade at staggered times. No shattering, grain/noise dissolve, shrinking root transform or separate sparkle-wisp pass. Lobe geometry is constrained above the supplied ground plane.

Chromatic investigation found DefaultVolumeProfile ChromaticAberration active at 0.1 and Main's cut-effect chromaticPixels values 2/3. Disabled the profile override and set Main's saved cut RGB split strengths to zero; new CutImpactDistortionSettings default to zero. Disabled Main plunge's optional refraction and updated its authoring paths to keep it disabled. Other cut refraction/blur capability is retained. Numeric tuning, hit tier, combat timing, camera and current placement are preserved. Cosmetic starting values: hold 18, saved exit delay 0, exit duration 78, scatter 0.65, lift 0.6; 18 sparks / 10 debris / 4 residual sparks.

Unity compiled and rendered phases 14, 55 and 72 were inspected; 41 sequential frames regenerated storm-motion.gif. No new compile/invalid-bounds errors appeared in the final capture log. An existing unrelated Unity editor NullReferenceException remains; continuous live gameplay verification of the reported colored artifact is not claimed. No automated tests added or run. Manual acceptance: broad blast must dominate at contact; center opens and lobes float/fade without becoming sparkles; Main QTE and follow-up show no RGB fringes. If too small widen Shape controls rather than add sparks; if exit too quick increase Exit Frames without extending damage.

Previous thin-ribbon motion correction (superseded) — supersedes all atomized/grain exit notes: Apricha clarified the explosion sheet is a motion reference: ground strike spreads outward, opens negative space at the center, then becomes sparse floating energy that tapers away. Removed all AtomTriangle/AtomLine/AtomCell tessellation, grain controls and exit mote emission. Storm ribbons now grow outward continuously, sweep their trailing ends away from contact, curl/lift and end at different times. Six sparse curved wisps float and become smaller; wind spreads low across the ground. No noise/dissolve mask, shattering or whole-object scale animation. Shader filament flicker removed. Existing Procedural Slash no-dissolve behavior, combat timing, camera, placement, slowdown and refraction edits remain intact.

Starting tuning: Exit Frames 78, Exit Scatter 0.65, Storm Lift 0.6, Floating Wisp Count 6, original saved hold 18 / delay 0 preserved. Supporting particles reduced to 12 impact sparks / 6 debris / 4 residual sparks. Manual acceptance: center must clear while wavefront moves outward; surviving strokes float and taper, with no speckled breakup or shrinking impact stamp. If too busy reduce branches/wisps; if motion travels too far reduce Exit Scatter; if too brief increase Exit Frames without extending damage. No automated tests added or run.

Previous atomized exit correction (rejected and superseded): User rejected dissolve and whole-shape contraction. Full-size lightning/impact surfaces now break into fine seeded grains, splash outward, brake into drift, rotate and get smaller individually. No noise dissolve remains in the plunge shader or Procedural Slash screen-mask shader. Starting values: Grain Size 0.16, Exit Frames 78, Scatter 0.65, 72 tiny supplementary Particle System motes; saved zero exit delay and 18-tick hold preserved. The current user-tuned action is 2 startup / 24 active / 26 recovery ticks, offset X -1.5 / Y 5 and overscan 5; these are preserved. A world-anchored cosmetic tail can finish after normal completion/follow-up without extending combat timing. Cancel/disable/reuse still clear it. No automated tests added or run; visual verification and limitations are recorded in the implementation report.

Previous exit design (superseded): Apricha approved the current look and requested a withering/dissipating exit instead of a static fade, explicitly inviting the design. Implemented a contracting/thinning column, staggered retracting spikes, drifting charged fragments, unwinding wind arcs, draining cracks and shader erosion. The readable hold and shared action clock remain; no combat/camera/slowdown/height tuning was changed. Exit controls and isolated visual evidence are recorded in the implementation report. Current workflow remains `/fix-bug` for this presentation revision.

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
