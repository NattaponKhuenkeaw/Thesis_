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

# Main Lightning Plunge — Manual Regression Checklist

## Resolver identity / CameraGirl handoff correction

- Open Main's Combat Animation Editor. QTE Entry appears before Combo with Lightning Plunge setup and LightningPlunge → ExecutionBreakdown. The resolver table itself contains the QTE action; editing/saving/reopening retains changes.
- Main's QTE profile references `main.qte.lightning_plunge`; ordinary Dimension Cut still uses `main.dash.dimension_cut`. QTE entry/follow-up consumes no cached cuts or dash quota.
- Land CameraGirl's Skill and EX Skill separately, then swap to Main while ready. Repeat both facings and with an outgoing finishing body at the landing/path. The body must not block readiness or abort descent; solid terrain must still reject unsafe entries.
- Compare Lightning Plunge timing, damage, VFX and saved placement/camera controls with the current authored settings. Run early recovery follow-up and no-input expiry.

Current validation is compilation plus serialized-reference/payload checks. No live gameplay pass is claimed; the Editor has no connected MCP session and native UI initialization failed. See [fix task card](../Specs/qte-resolver-entry-fix-task-card.md).

## Camera removal revision — 2026-10-28

2026-10-29 correction: Main now uses the same shared intro zoom pulse and horizontal centering as CameraGirl. The removed plunge-specific lens lock, vertical positioning and position-return blend stay removed. Verify this through a prepared swap and the plunge preview; no automated tests were added or run.

- [ ] Main and CameraGirl both trigger the shared intro zoom using the saved Intro Camera Impact settings; setting size reduction to zero disables the pulse for both.

The user approved removing the plunge-specific camera lock and return blend. This supersedes the earlier midair body-lock, vertical bound bypass and 0.45-second return expectations below; those observations are historical.

- [ ] Plunge entry, descent and contact use ordinary camera follow and room bounds, without a dedicated vertical lock, forced lens or post-impact position blend.
- [ ] Heavy-hit screen impulse still plays on a confirmed hit. Other characters retain their existing QTE horizontal centering and intro zoom pulse.
- [ ] No-input recovery, early Execution Breakdown, interruption and repeated entries leave no plunge camera state behind.
- [ ] Plunge placement, body visibility, lightning VFX, slowdown and damage timing remain unchanged.

No automated tests are authorized, added or run. Use the existing Test_Playground or a normal gameplay room. Do not save Play-mode state into progression or scenes.

- [x] Compile new scripts through Unity; inspect compilation diagnostics.
- [x] Isolated preview shows the enlarged impact exceeding Main's sprite size and a visible later wind/electrical tail.
- [x] Actual QTE-state preview hides the body in flight and reveals it on confirmed ground contact.
- [x] Body-following camera measured at viewport center during flight with the same lens at contact/recovery; confirmed again at final Y+5 (size 6.8).
- [x] Normal attack during recovery enters the existing ExecutionBreakdown node; owned particles clear before continuation.
- [ ] Confirm final enemy-relative Y+5 entry through a prepared team swap in both facings; readiness/cooldown/provenance remain valid.
- [ ] Confirm the larger visual remains readable against both bright and dark gameplay backgrounds, with danger silhouettes visible.
- [x] No-input recovery returned to visible, grounded Idle at frame 136 with particles/motion cleared.
- [ ] Broader early/late follow-up buffering checks on both primary input devices.
- [ ] Confirm Heavy impact tier feedback in a normal combat encounter; numeric damage budget and hitbox extent remain unchanged.
- [ ] Reject unsafe roofs/gaps/blocked exact destination before consuming a prepared QTE; ordinary swap fallback only when eligible.
- [ ] Moving/rotating/destroyed support, interruption, death, scene change and disable/re-enable restore visibility and camera ownership.
- [ ] Pause/hitstop holds meshes and particles together; repeated casts do not grow effect instances.
- [ ] Verify camera release in static/explore/combat rooms and unsupported/rotated confiner shapes; do not infer this from one room.
- [x] Latest camera correction compiled; grounded contact camera Y about 1.43 replaces the previous zoom-scaled height bias around 4.4 at size 6.8.
- [ ] Observe uninterrupted 0.45-second return at contact, no-input expiry and early Execution Breakdown. Pass: no one-frame position/lens jump, including when the old combat execution is gone.
- [ ] Pause during the return, then resume; scaled-time blend should hold and continue. Start a newer QTE during release; the new instant lock should cancel the old return.
- [ ] Ordinary Main Dimension Cut, CameraGirl QTE and Execution Breakdown's independent effects remain correct.

## Thunderstrike revision

- [ ] Two-tick strike is presented through the restored saved intro slowdown. Pass when the handoff reads as intentional slow motion and impact remains visible after it; do not equate logic ticks with rendered frames while slowed.
- [x] Saved timeline is 2 startup / 3 active / 120 recovery, with captured-world endpoint tick 2 and contact audio tick 3.
- [x] Isolated rendered phases show a long thin bolt extending beyond the screen top and soft material glow instead of the winding silhouette.
- [x] Final isolated dark-background render shows a continuous soft aura/halo with the main column join stripes removed; bright-background gameplay remains for human review.
- [ ] Confirm local URP light on a lit sprite; unlit sprites are expected not to receive local-light shading.
- [ ] At contact tick 3 the body is grounded before Heavy damage; standing pose uses tick 14. Confirm the existing recovery attack continuation still works at actual gameplay speed.
- [ ] Compare shader softness/emission and dark aura at gameplay zoom; pass when the core is bright, the aura is deep purple and low-alpha trails preserve enemy readability. Adjust emission/halo alpha before increasing geometry/particle count.

## Readable impact / restored intro slowdown

- [ ] Main handoff invokes the existing intro slowdown with the saved prefab duration/intensity; other QTEs and the corrected camera remain valid.
- [ ] Hold/fade settings keep the ground burst strong around halfway through recovery, then fade cleanly before completion. If too busy, reduce hold or burst fade; if still too fast, increase hold first. Do not extend damage to lengthen visuals.
- [ ] Confirm column and local light persist through their configurable holds/fades; pause/hitstop continues to hold the shared action clock.

## Ground ring rendering repair - 2026-10-29

- [x] Ground Shockwave Enabled restored ON; WindArcs and wide GroundGlow remain rendered.
- [x] Shared shader is texture-free; wind uses existing soft additive glow material in saved prefab, authoring and runtime.
- [x] Arc sections share analytical ellipse-edge vertices, taper stays nonnegative, endpoints have zero width.
- [x] Runtime and Editor C# compiled with Unity's cached references into OS temp. No automated tests added or run.
- [ ] Once Unity tools reconnect: import shader, verify no shader errors, repeat the exact Game-view hit and inspect expanding purple ring for green/black bands.
- [ ] Observe smooth outward travel/fade with blast/electricity retained; verify reuse/interruption clears old geometry and pause/hitstop holds it.

No fresh GPU/Game-view validation is claimed while Unity MCP and UI tool initialization remain unavailable. The earlier ring-disable workaround was rejected and has been reversed.

## Purple electrical accent revision

- [x] Unity sequential preview: falling frame 0 / impact 15 / tail 54 inspected with short purple arcs and halos around the main shapes.
- [ ] Live: falling arcs point down, impact arcs remain secondary, and the large explosion/actor silhouette remains readable.
- [ ] Pause/hitstop freezes arc pulses and falling sparks. Skipping across contact emits the impact burst at the correct sampled time; rewind/reuse has no duplicate bursts.
- [ ] Tail reduces arc density with the existing exit; cancellation/disable clears accents and particles.
- [ ] Chromatic/refraction remain disabled. Tune counts/spread/glow only; preserve action/contact timing.

## Broad explosion exit and chromatic removal (current)

- [x] Isolated renders inspected at 14/55/72: broad initial starburst, clearing center, smaller floating energy lobes.
- [x] Saved Main cut chromaticPixels values are zero, plunge impactDistortion.enabled is false, DefaultVolumeProfile ChromaticAberration is inactive at zero.
- [ ] Live Main plunge and Execution Breakdown show no colored RGB fringes or reported green/black distortion. If another colored artifact remains, capture its active source rather than changing the blast shape again.
- [ ] Broad explosion dominates at contact; exit opens outward and loses density without grain, shatter, shrinking root or sparkle-wisp animation.
- [ ] Above-ground lobe clamp remains correct on slopes; normal completion/follow-up lets cosmetic motion finish, cancel/disable/reuse clears it.
- [ ] Pause/hitstop and rewind preserve deterministic timing; authored damage, Heavy tier, camera and placement are unchanged.

Starting values and tuning directions are in the implementation report. No automated tests added or run.

Performed observations and limitations are in the [implementation report](../Reports/MainLightningPlunge/implementation-report.md). The latest user-approved height is Y+5, replacing the initial Y+3 and interim Y+10. Scale/tail starting values require human feel review; this checklist is not an automated pass report.
