---
slug: main-qte-lightning-plunge
status: needs-human
source: manual
gdd_tags: [combat, combat-scope, characters, platform-input, visual-audio]
owner: architect-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main QTE Lightning Plunge — Intake and Design

## Latest exit animation

Apricha liked the current effect and requested an exit that withers/dissipates rather than remaining static and fading, inviting the design. Added morphology on the existing action clock: a contracting/thinning column, staggered retreating/narrowing spokes, rotating/drifting charged chips, inward-draining cracks and wind arcs that rotate/peel into shorter sections. Added procedural shader-cell erosion with charged edges for solid energy shapes; smooth halo/pool layers do not inherit the erosion mask. Geometry buffers are reused and fragments are sampled directly from age/seed rather than spawned as per-frame objects.

New starting controls: Exit Delay 12 ticks after the existing hold, Exit Frames 78 bounded to each layer's lifetime, Exit Scatter 0.65 and Fragment Count 24. A narrow authoring command saves these and the ExitFragments group without resetting current hold, palette, camera, height, slowdown, damage, recovery or follow-up. Unity compiled and rendered with zero console errors in the pass. Inspected phase 55/90 captures showing clear structural retreat and fragments instead of a stationary alpha fade. Manual continuous game-feel/reuse review remains human-owned; no automated tests were added/run.

Observed the user's latest saved entry offset as X 1.5 / Y 4 during final asset inspection and preserved it. This exit-only authoring operation did not reset the previously tuned entry or camera return.

## Latest pacing correction

Apricha clarified that the impact/overall sequence was too fast and the Main QTE intro slowdown should not have been removed. Restored Main's call to the existing handoff slowdown, preserving the Player prefab's current 0.08× / 0.5-second saved tuning. The code's default 0.24-second duration was not forced onto that prefab. The two-tick physical entry, Heavy hit, 120-tick recovery, follow-up, height and camera settings remain.

Added visual-only hold/column/light fade controls and lengthened burst, wind and cosmetic particle lifetimes. Starting values: hold 18 ticks, burst fade 90 after hold, column fade 54 after hold, light fade 36 after hold, flash 10, wind 108, residual 118. This replaces hard-coded 14-tick column and 18-tick light fades that made the impact disappear too early. Main's damage active phase is not extended. New narrow editor command applies timing without regenerating unrelated art/animation. Latest validation appears in the implementation report; no automated tests are added/run.

Saved the timing values through Unity and inspected regenerated phase 55/90 captures: the burst is still strong around 0.9 seconds and visibly decaying around 1.5 seconds. Live artist preview now calls the existing handoff presentation too, so it reflects the saved intro slowdown instead of omitting it. Unity finished compilation/domain reload; no automated tests were added or run. No Player prefab settings, saved height/camera, damage timeline or follow-up were reset.

Date: 2026-10-28, Asia/Bangkok.

Apricha requested replacing Main's QTE with a lightning-only descent, ground impact, Landed → Standing recovery and an Execution Breakdown input window. Confirmed in chat that mirrored X+1 / Y+3 is relative to the enemy target.

Inspected current Main QTE asset, optional authored combo, QTE state/controller, team handoff, horizontal-only camera correction, existing landing art and Execution Breakdown authoring. Existing entry already has 120 recovery logic ticks. The authoring utility would currently recreate the old cut intro, so the future implementation must update it too.

Created the [task card](../Specs/main-qte-lightning-plunge-task-card.md) and [design spec](../Specs/main-qte-lightning-plunge-design-spec.md). Proposed a short vertical descent, local ground contact, existing landed/standing poses and the retained two-second recovery as editable starting values. Included camera, terrain safety, visual reuse, interruption, audio asset limitations and manual acceptance criteria.

Selected `/implement-feature`. Its Step 3 requires human design approval before architecture work; the task is at that checkpoint. No runtime scripts, character assets, prefabs or scenes were changed for this task. No automated tests were added or run. The extensive pre-existing working-tree changes were preserved. Unity was connected to this project and reported edit mode while compiling during read-only discovery.

## Design approved; architecture prepared

Apricha approved the design with “lgtm” on 2026-10-28. Marked the design approved and advanced the task to the architecture checkpoint.

Inspected the current motor's grounded visual Y offset, explicit target-motion collision sweeps, combat frame processing order, intra-state QTE combo continuation, camera ownership exclusions and authoring overwrite path. The proposed physical descent needs an explicit captured-world MotionData path rather than a negative relative Y key. Contact must be resolved before the existing hit pipeline, and cleanup must occur before continuing to Execution Breakdown because that transition does not exit PlayerQteIntroState.

Saved the [architecture plan](../Specs/main-qte-lightning-plunge-arch-plan.md) and [proposed ADR-014](../ADRs/014-prepared-qte-plunge-motion-and-presentation.md), and registered the proposed ADR. Runtime implementation remains pending the selected workflow's Step 4 architecture approval. This stage changed documentation only; no automated tests were added or run.

## Implementation and human playtest revision

Apricha subsequently approved the architecture with “lgtm.” Implemented Main's optional prepared plunge profile, physical motor path, root-state contact-before-hit execution, renderer restoration, reusable procedural/Particle System prefab, logic-sampled actor clip and scoped camera. Preserved the existing Execution Breakdown node when authoring the Main asset. Added editor authoring and visual/live preview menus. Used existing Project/FieldEffectUnlit; no packages or external assets were added.

Human playtest requested a much larger/longer impact, Heavy tier, higher entry and removal of a ground-dipping camera/zoom transition. The final height instruction is Y+5; Y+10 was an interim request only. Implemented impact-only scale 2.5 with a 54-tick burst, 84-tick wind and 114-tick electrical tail as starting values. Heavy changes feedback tier; the numeric damage ratio and hitbox are not enlarged. The camera now follows the body and holds the handoff lens, with horizontal box confinement and a floor-safe Y minimum. This replaces the midpoint/floor focus and size punch that caused the reported sequence.

Manual Unity preview observed the hidden airborne body, grounded contact/reveal, cosmetic particles and normal-attack continuation into Execution Breakdown. During the interim Y+10 check the actor was centered at viewport (0.50,0.50), and camera size stayed 6.844998 through contact/recovery. Regenerated phase screenshots demonstrate the much larger impact and its later tail. Final Y+5 observations and remaining coverage are recorded in the [implementation report](../Reports/MainLightningPlunge/implementation-report.md). No automated tests were written or run. Existing unrelated edits and current branch were preserved.

Final Y+5 was applied through the editor authoring command and confirmed in the saved Main asset. Live frame 4: hidden airborne body Y 4.78, camera Y 5.78, viewport center (0.50,0.50). Frame 21: grounded visible actor, enlarged impact with 36/18/15 cosmetic particles. Camera size held at 6.8, including no-input completion into Idle at frame 136 with effect/motion cleared. Unity returned to edit mode. Runtime console messages from the existing Skill Animator trigger and hitbox debug sprite drawing remain documented; no unrelated changes were made to hide them.

## Camera framing and exit damping correction

Apricha then reported the camera framing too high and the QTE ending in a jump cut. Root causes: `size × 0.65` imposed a large vertical floor bias, while release immediately removed the correction and invalidated Cinemachine composer/group history. Removed that zoom-scaled minimum and exit invalidation; added profile controls Camera Actor Offset Y (starting value 1.5) and Camera Return Seconds (0.45). The extension eases from its last QTE view to each camera's current gameplay position/lens, survives the old action ending, and yields to a newer handoff. Begin remains an instant lock; return starts at contact or early release. This preserves the approved scoped presentation authority.

Unity compiled with zero compile errors. Live frame 7: body Y 3.97 / camera Y 5.47; frame 14: grounded body Y -0.07 / camera Y 1.43, lens 6.8. Subsequent normal view around Y 3.35. Continuous early Execution Breakdown return observation was interrupted by an editor state change; it remains in the human manual review checklist. No automated tests were added/run, and no VFX size/tier/Y+5 timing values were reset.

During the concurrent manual playtest, the saved start height changed to Y+3. Preserved that external asset edit and notified Apricha; the camera fix does not force the height back to its Y+5 initial default.

## Thunderstrike timing and URP visual polish

Apricha requested a quick 1–2-frame cue and a long bolt from above the screen, rejecting the winding shape, with dark-purple aura, brightness/glow and smooth low-alpha gradients. They explicitly allowed URP shader/material/lighting work. Changed Main's startup from 12 to 2 logic ticks, moved physical endpoint/contact audio and pose sampling to that timeline, and skipped Main's own handoff slowdown. Saved height/camera tuning, Heavy tier, 120-tick recovery and the existing Execution Breakdown node were preserved.

Replaced length-scaled bolt displacement with bounded fine kinks and small forks. Added camera-top overscan, separate soft aura/halo/radial glow meshes, UV edge gradients, an HDR additive/alpha URP shader and four dedicated materials. Added an action-clock local Point Light2D that fades after impact. No packages, scene-wide Bloom/volume, renderer feature or external artwork was introduced. Shader geometry supplies the visible glow even without Bloom; local lighting affects compatible lit sprites only.

Unity compiled, the revision authoring command updated actual asset references, and isolated phase rendering completed with zero console errors. Captures show an off-screen thin strike, luminous ground contact and larger fading purple breakup. Visual inspection found dark segment-join stripes on the absorption ribbon; replaced that main aura with a continuous ribbon and recompiled for the final render pass. No automated tests were added or run.

Final rendering also uses a continuous main halo, removing the visible join gaps. Reimport/compilation and regenerated phases completed with zero console errors in the final pass. Inspected phase 1 and phase 6: thin off-screen bolt, fine forks, smooth purple halo and bright ground breakup. Verified saved startup/endpoint/audio/standing ticks as 2/2/3/14, actual four material references, local Light2D sorting-layer assignment, and retained saved height Y+3 / camera offset 1.5 / return 0.45. Full prepared-swap gameplay was not rerun for this visual-only revision; no automated tests were run.
