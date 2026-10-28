---
slug: main-shadow-meter
status: draft
gdd_tags: [combat, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Shadow Meter revision validation

The current design is press Dash to cache, then Normal Attack to spend. The earlier hold-Dash behavior has been removed.

## Automated checks

`ShadowMeterTests` covers hit-only gain, different authored gains, overflow/capacity, Potential clamping, insufficient-box conversion, multiple cached charges, no-target retention, rejected-dash behavior, counter cost at Normal action commitment, repeated cuts returning to the ordinary combo, grounded state preservation, horizontal/minimum target travel, solid casts, meter partial fill/readiness pulse and prefab sibling order.

Run **Tools > Project > Combat > Validate Shadow Meter**. The menu uses the project's isolated synchronous executor, creates temporary preview objects and restores the previous scene without saving it. Results and previews are copied into `../Reports/MainShadowMeter/` after validation.

## Human playtest

1. Gain 40 points through hits, press Dash twice with normal cooldown between commitments: meter becomes zero, cached runtime counter becomes two, neither dash attacks.
2. Repeat with 19 points or during dash cooldown/hitstun: no unaffordable conversion or cache duplication. Holding the button must not repeat a dash or queue a cut.
3. With two cached cuts and a target, press LMB and then LMB during Recovery/Cancel: each input commits one cut; subsequent LMB resumes the ordinary combo. Confirm cut use works while dash cooldown is running.
4. With cached cuts but no target, LMB uses ordinary attacks without losing cache. Confirm no meter gain from the cut itself and no refunds after a committed cut is interrupted.
5. Target left/right/at the same X and much higher/lower: player Y remains unchanged during horizontal motion. Grounded action does not forcibly become airborne. Natural lost-support falling is still allowed.
6. Close targets produce at least the authored minimum horizontal travel unless a wall blocks it. Check wall, ledge, slope, ceiling and one-way-platform cases before changing damage/timing.
7. Armor/reaction immunity and another grab owner must prevent forced enemy rebinding. Target death/loss and action cancellation safely release the owned anchor.
8. Inspect the prefab HUD: status icons above, larger meter with no CUT label in the middle, HP/AE/stat panel below. Switch characters and verify no duplicate meter, stale meter fill or inherited cache.
9. Verify Potential capacities 2/3/3/3/5 and Attack4-6's Lv2 gate. At Lv1 only Dimension Cut gains +15 CRIT RATE and only its critical hits bypass Defense. At Lv3 verify 8% healing from actual Cut damage. At Lv4 verify 2x meter gain inside Void Area, returning to normal within 0.3 seconds of leaving. Author final animation/motion and approve game feel after this smoke test.

## Potential regression checks

Run `Tools/Project/Combat/Validate Main Potential` in Edit mode. It runs ShadowMeterTests and CombatDamageTypeTests in an isolated additive scene and writes `Temp/main-potential-tests.xml` without saving the user's open scene.

Check the actual Skill Lv4 enemy effect replaces 15% with 40% rather than adding to 55%; the allies buff grants +15 CRIT RATE for 7 seconds and refreshes instead of stacking. Exercise all three Void Area assets for marker expiry, doubled gain and unchanged Dash cost. Use deterministic crit rolls to compare locked/unlocked tiers, critical/noncritical cuts, ordinary attacks, preserved resistance/type reduction, and unchanged shared stats.

Latest Potential run: **79/79 passed** (61 ShadowMeterTests and 18 CombatDamageTypeTests), with zero Unity console errors. Results: `../Reports/MainPotential/tests.xml`. Tests use cloned kit/table data and an isolated team inventory for runtime effects. Live animation, combat feel and the field boundary refresh tolerance remain part of the requester smoke test.

## Buffered facing regression

Disable Face Target On Arrival, cut past an enemy on each side, then buffer NA, Skill and Ultimate. Each manual action should face the enemy before its startup payload. Check chained actions with the same combo depth and no intermediate movement tick. Automatic follow-ups should retain their facing; holding horizontal input should retain the existing manual-direction override. Automated cases exercise actual PlayerCombat.ExecuteStep commitments with the previous target behind the player.

Facing correction verification: Unity compiled the changed runtime and Editor assemblies; eight new regression cases are compiled but unrun. The connector was unavailable and computer-use capture timed out. The previous 79/79 Potential result predates this correction.

No automated result certifies final animation alignment, live target choice or game feel. Final run results are recorded below.

Latest checks also cover radial target selection left/right/above/below, captured-target facing against opposite movement input, vertical target anchoring without player lift, and absence of a text label. Render the actual prefab and verify its larger meter in the status/stat gap.

## Final verification — 2026-10-27

Unity compilation completed with no console errors. All **39/39 ShadowMeterTests passed**. The broader run passed **61/63**; only the existing CameraGirl skill value expectation (33 expected, current edited asset 43) and an older damage-interrupt fixture with an uninitialized HitstunState remain failing. All 7 AbilityUIFeedbackTests passed in this final run.

Rendered and inspected the actual Player_UI prefab: the enlarged segmented meter occupies the status/stat gap with no CUT label. Captures: ../Reports/MainShadowMeter/hud-layout.png and meter-preview.png. Raw results: ../Reports/MainShadowMeter/tests.xml. The integration check executes Normal Attack state input for two cached cuts and a normal attack afterward without switching to airborne. Physical/visual game feel still needs the user's final animation authoring and PlayMode testing. The user authorized stopping Play mode for final validation; no scene saves or publishing were performed.

## Target-radius regression verification — 2026-10-27

All **47/47 ShadowMeterTests passed**. With action fallback still 8 and motion range 1000, action selection, target capture and cached-charge commitment accept targets at 100 units on either side and at 999 units, and reject 1001 units. A motion range of 2 rejects a target at 3 while accepting one at 1, proving there is no hidden larger fallback. Additional checks cover shared editor setters, earliest target-motion selection and fallback behavior for actions without target motion. Unity console reported no errors. Broader run: **69/71 passed**; the same CameraGirl value and startup-hitstun fixture expectations remain failing.
