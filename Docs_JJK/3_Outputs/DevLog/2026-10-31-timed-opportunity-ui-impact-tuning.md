# 2026-10-31 — Stronger opportunity outline impact

Apricha's playtest found the first shared effect too small and requested more
exaggerated impact like the supplied reference. Clarified explicitly that a little
edge spill is wanted while the icon center stays clear. Followed the existing
handoff contract's game-feel tuning loop within the approved UI architecture.

Updated the shared procedural Graphic and all three authored ability instances:
two staggered waves, expansion 1.85x (previously 1.24x), 1.35-second wave duration,
3.5-unit bright core, 14-unit cyan halo and 6-unit padding. These are tuning starting
values. QTE uses the same updated defaults. A three-stage halo falloff and wave
fade-in avoid a broad opaque-looking border around the art.

The inward feather is independently bounded to 2.5% of the shorter icon dimension;
enlarging the outside halo cannot wash across the center. Added geometry regression
checks at several phases; preserved reduced motion, pause and clearing behavior.
No changes to team-slot alpha, portrait exchange, gameplay windows or input.

Preserved pre-tuning captures/results as `before-impact-*` in the existing report
folder for comparison. Final test and render results are recorded in the review.

Final validation: 63 focused EditMode tests passed, 0 failed, 0 skipped. Unity
compilation passed; final ability and QTE render captures were inspected.
