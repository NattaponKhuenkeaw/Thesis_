---
slug: timed-opportunity-ui
status: approved
gdd_tags: [ui, combat, visual-audio, platform-input]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Timed-opportunity UI implementation review

Self-review complete; ready for human in-game feel review. No remaining actionable
code findings were identified in the scoped change. Design and architecture were
approved by Apricha, including the InactiveCanvasGroup cooldown change.

## Current implementation

- One TimedOpportunityGraphic renders the shared procedural core, feathered glow
  and slow expanding wave. No shader, bloom, material, dependency or pipeline change.
  The effect is non-interactive and intentionally renders beyond sprite masks.
- AbilityUIManager drives the effect from existing selected eligible action windows.
  The Icon stays fully drawn; former follow-up Image components are replaced by
  the shared Graphic. Expiry/consumption restores current ordinary art/fallback.
- TeamSwitchHUD instantiates inactive destinations only and reuses the same view
  across two-character swaps. The standby label stays `1` across roster indices
  and gaps; keyboard `1` targets the next living teammate. Stat events
  cannot preempt the committed-swap portrait transition.
- InputHandler accepts top-row or numpad `1`, resolves the next living teammate
  at press time, and no longer binds `2` to a roster index. The existing input
  buffer and switch legality route remain in use.
- CharacterSwitchSlot animates only portrait content, caching a single non-raycast
  outgoing Image. Gameplay destinations and stats rebind immediately. Reduced
  motion and disable restore the portrait position/alpha.
- The existing InactiveCanvasGroup stays active. Its authored intake opacity 0.34
  is preserved as editable inactiveAlpha; ready alpha is 1. The separate tuning
  field prevents a ready-state rebind from replacing the cooldown value with 1.

## Verification

- Unity 6000.3.4f1 compilation completed; final console query returned no errors.
- Focused EditMode validation: **63 passed, 0 failed, 0 skipped**. Covers ability
  icons/windows/readiness, QTE gameplay regression, roster lifecycle, cooldown
  alpha restoration, rapid portrait swaps, reduced motion, pause and effect cleanup.
- Inspected authored-canvas images: ordinary Main, Skill time window and QTE
  portrait. Artwork stays visible and the outline renders outside the masked icon.
- Exact serialized references checked for all three ability graphics and the
  existing standby CanvasGroup. Source/prefab diff whitespace check passed.
- Results: `../Reports/TimedOpportunityUI/tests.xml`, `test-summary.txt`,
  `main-ordinary.png`, `main-judgment-cut-window.png`, `qte-ready-portrait.png`.
- The fixed-key control follow-up was made after that 63-test run; it has not been
  recompiled or rerun in Unity in this turn.

The first pass reported three failures: test reflection needed the exact mesh
overload, the migrated graphics needed parenting under their actual icons, and an
old content assertion assumed every combo Sprite was assigned. The current Main
ground-combo Sprite subasset does not resolve; the test now verifies the required
visible HUD fallback. Character art/data was preserved. Corrected wiring and tests
passed the second full focused run; first-pass XML is retained for traceability.

## Limits and remaining human checkpoint

Automated state/geometry tests and canvas captures were run. Full interactive
combat-playtest sign-off for pulse intensity and slide feel remains with Apricha.
No merge, publish, configuration change or official team notification performed.
Pre-existing local changes were preserved; task-intake source/prefab snapshots are
under ignored `Temp/TimedOpportunityUI-baseline/` for scoped comparison.

## Requested impact tuning — 2026-10-31

After the first playtest Apricha requested larger, exaggerated feedback and
confirmed a slight inward glow spill with a clear icon center. Increased the
shared defaults and authored ability instances to 1.85x expansion, two staggered
1.35-second waves, a thicker bright line and wider cyan halo. The feather is
limited to a 2.5% rim; the artwork center has no overlay geometry. A multi-stage
falloff keeps the halo soft. Reduced motion remains static.

Final focused run: 63 passed, 0 failed, 0 skipped, including three phase samples
for the enlarged footprint and inward-spill limit. Unity reported no C# compile
errors. Inspected final ability and QTE captures; pre-tuning captures/results are
retained as before-impact-* in the report folder. Subjective combat feel remains
for the next user playtest. No gameplay or cooldown-alpha changes in this slice.
