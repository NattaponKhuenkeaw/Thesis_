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

## Dissipating exit animation

- [x] Unity compile/render pass completed without console errors; inspected mid/late exit renders at action frames 55 and 90.
- [x] Rendered geometry contracts and fragments rather than staying at the original silhouette with reduced opacity.
- [ ] In continuous gameplay, the impact retains its initial readable hold, then the column contracts downward, spikes withdraw at different times, chips drift/rotate and wind arcs unwind.
- [ ] Exit remains readable against bright backgrounds; erosion should resemble charged breakup rather than noisy transparency. If too granular, adjust shader grain/coverage rather than lengthening damage.
- [ ] Pause/slow motion holds the same fragment/retraction progress; frame skipping and preview rewind preserve deterministic sampling.
- [ ] Repeated use clears old meshes/particles/light and starts with zero erosion. Early Execution Breakdown still takes presentation ownership cleanly.

Performed observations and limitations are in the [implementation report](../Reports/MainLightningPlunge/implementation-report.md). The latest user-approved height is Y+5, replacing the initial Y+3 and interim Y+10. Scale/tail starting values require human feel review; this checklist is not an automated pass report.
