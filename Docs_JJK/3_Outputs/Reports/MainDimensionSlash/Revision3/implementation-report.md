---
slug: main-dimension-slash
status: in-review
source: manual
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Execution Breakdown and Judgment Cut

The latest user direction is implemented on `codex/dimension-slash-procedural`. Main's QTE Dimension Cut now has an ordinary input-driven combo continuation named **Execution Breakdown**. Judgment Cut remains the procedural Skill and now stays anchored in world space, with30% overall scale and a broader small-to-large slash range.

## Player flow and authoring

Expanded action coverage: **DimensionCut** now enables the adapter on its existing 17-frame sprite VFX. **Execution Breakdown** enables it on six 4-frame burst sprites in addition to its final procedural tear. **Lightning Plunge** drives it from the current bolt ceiling/contact endpoints, with a12-frame onset pulse/refraction4/RGB0/blur0/band width0.04. Its profile suppresses baseline chromatic aberration only in the evaluated camera Volume stack while the presentation is visible; the feature restores the previous value after that camera renders. The saved default Volume profile is not edited. Authoring confirmed11 enabled slash/sprite/plunge sources, preserving existing action trees. Source and shader compilation completed; only the unrelated editor Graph/Edge.WakeUp exception appeared during reload. No gameplay tests/captures or visual/performance verification were run. The sprite adapter approximates cut direction using the sprite's longest local bounds axis, so curved sprite slashes may need artist tuning.

Barrage enablement correction: refraction is now enabled on the DimensionSlash barrage as well as the two finishing events. Its saved toggle was false and both authoring paths omitted it. The targeted impact utility now includes DimensionSlash, while CreateStep explicitly enables it for future regeneration. Runtime uses the latest visible primary ribbon during the barrage. No gameplay tests/captures were run for this correction.

Latest impact follow-up: the user's correction replaces the ground-anchored circular wave with an 18-frame band of refraction/RGB separation/edge blur bound to the visible cut. Ribbon metadata supplies its exact randomized position, angle, reveal direction, shrink and fade. The fullscreen tear uses the actual mask quad, mirrored diagonal and narrowing opening. Tune **Cut-following Refraction**, start, duration, band width and pixel strengths in the Procedural Slash Field event card. The gated Renderer2D feature reuses URP's fullscreen pass; nearest sampling preserves pixel edges. **Tools → Project → Combat → Author Main Cut Impact Distortion** updates only the existing impact fields. Compilation and shader import completed; authoring confirmed two enabled finishers at startFrame 0 and preserved the 28.5° tear angle and Test_Playground file hash. No gameplay tests or captures were run; verification/media below describe earlier revisions. The editor Graph/Edge.WakeUp reload exception remains outside this effect's stack. [ADR015](../../../ADRs/015-action-owned-cut-refraction.md) records remaining feel/performance review.

Activation audio follow-up: **Activation SFX Volume** on UltimateAction and **SFX Volume** on UltimateOverlaySequence's ActionEventEntry both default to 1 and clamp to 0–1. Runtime multiplies the action master by the event gain before the existing master/SFX mixer scaling. The legacy action overload also forwards its gain. Voice volume, unscaled delay and pooled-source return timing retain their existing behavior. These controls are available in the Animation Editor and serialized action data; no audio playback test was run.

QTE Dimension Cut → **NA during Recovery** → Execution Breakdown. Its starting Recovery is120 logic frames, approximately2 combat seconds: frames27–146 after the existing26-frame startup/active portion. Skipping input ends at147 and returns to locomotion. Existing short input buffering and normal Recovery interrupts apply. The follow-up shares the Dimension Cut action context and advances to depth2 through `ActionResolver.ResolveNext`; it does not select another standalone action or spend another cached cut. The source manual cut keeps its existing3/23/6 timing and cost.

An optional `QteSwapDefinition.introComboTree` supplies an authored root and next node to the existing explicit-entry resolver. Empty profiles retain terminal single-step behavior. The explicit QTE combo preserves its entry context across the dash's transient physical ground-contact changes. The first node is marked as QTE intro; the player-triggered continuation can own the original bounded action-camera envelope. ExecutionBreakdown's step name excludes it from the five-paid-Dimension-Cuts gate.

Open **Window → Animation Editor**, select Main and expand **QTE Entry Combo**. Select DimensionCut to edit Recovery and ExecutionBreakdown to edit its actor, hits, VFX and final procedural event. The editor uses the existing timeline/property cards and scrubs the same procedural renderer. `Tools/Project/Combat/Author Main Dimension Slash` rebuilds generated content and replaces manual tuning of these generated steps.

## Presentation

- Judgment Cut's visual meshes have a detached world root. Its action-owned component remains under the actor for cancel/disable/destruction cleanup. Each field retains its initial position despite player translation/rotation.
- Starting composition values: overall1.3×, length range3–46, viewport maximum1.8 diagonals and width0.08–1.8. Five screen-centered slashes span at least1.25 viewport diagonals, placing their endpoints beyond opposite edges. The other cuts remain distributed around the scene. Squared width sampling preserves fine cuts with larger accents;50% of slash ribbons use foreground sorting. Faster pacing uses2-frame intervals,1–3-frame reveals and restored16–27-frame decay. The traveling glint stays compact in pixels even on screen-spanning cuts.
- Execution Breakdown restores the original actor clip and seven contacts at12/20/27/33/38/42/54, original8%×6 +52% damage shares, burst/residual/seam/shards tracks and audio timing. Its final Heavy contact keeps the original authored impact feedback and bounded camera motion.
- Only the old final upper-cut sprite is replaced. At54 a procedural foreground sheet flashes white/lilac, opens a screen-length diagonal transparent tear, narrows it and dissolves through oriented fragment holes. The scene shows through actual alpha holes. The foreground order is12000; screen-space overlay HUD remains above it.
- Judgment Cut retains19+1 Low contacts, enabled crits, suppressed global hitstop/impulse and the existing Passive4/five-paid-cuts/Skill opportunity.

These are tunable starting values based on the supplied references and the user's scale request. The implementation does not claim knowledge of BBEEx's internal particle/shader setup. The later impact follow-up adds one gated renderer feature; no package, build, physics or timestep change was required.

## Review media

- [Execution Breakdown: QTE → Recovery NA → original barrage → transparency tear](execution-breakdown-qte-silent.mp4).
- [Larger world-anchored Judgment Cut](judgment-cut-silent.mp4).
- [Execution phase board](execution-phase-board.png), [Judgment phase board](judgment-phase-board.png).
- [Runtime trace](Runtime/runtime-report.json), [compact results](verification-summary.json), [test XML](tests.xml).

The attached clips show the previous captured version. This latest size/cadence adjustment has not been recaptured yet. The earlier clips use actual Unity camera readbacks with the real Player/enemy prefabs, colliders and damage pipeline in the dedicated validation scene. They contain no recorded game audio and observed22–38 FPS. Raw readbacks remain locally available and are ignored by Git.

## Verification

**28/28 focused tests pass.** Coverage includes existing Judgment gate/provenance/damage/camera tests, detached world roots and30% geometry scale, optional QTE combo entry versus terminal manual/legacy entry, original cut animation/motion/hit reuse, authored Recovery boundaries, NA at first/last Recovery, resource/count preservation, skipped-input completion, retained seven-contact budget and the final mask event.

Live capture passes six Judgment casts in both directions at requested30/60/120 caps, each with20 Primary contacts and minimum timeScale1. Every sampled barrage has one unique world anchor. Critical contacts remain enabled; actor visibility and camera ownership restore on completion. The QTE Recovery input advances to depth2 and delivers seven Execution Breakdown contacts at the original frames. Cached cuts remain unchanged. Skipping Recovery expires the combo without executing the follow-up. Pause/Skill expiry, startup damage and disable-while-hidden checks also pass.

Selected regression fixtures total **143/154**, with11 unresolved failures already present in the preceding revision: eight ShadowMeter reflection-fixture null references, one startup-hitstun fixture with uninitialized states, and two QTE placement expectations differing from the current shared authored offsets. Raw XML retains them. This is not a fully green project test suite or a full acceptance/performance pass; see the preceding [failure classification](../Revision2/implementation-report.md).

The [preservation check](preservation-check.json) matches all seven protected files with this revision's [preflight](preflight.json): shared scene, run inventory, progression, package, build, physics and timing data. No commit, merge, deployment or team publication was performed. Remaining review covers final feel, a populated bright/dark scene, HUD/camera composition, alternate aspect ratios and uncaptured performance.
