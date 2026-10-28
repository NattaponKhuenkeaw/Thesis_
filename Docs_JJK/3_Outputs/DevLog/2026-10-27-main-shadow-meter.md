---
slug: main-shadow-meter
status: draft
gdd_tags: [combat, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main Shadow Meter / Dimension Cut

Implemented the user's requested replacement of the main character's third-step heal/AE passive. The user clarified that meter gain is from successful hits only. Gain, box quota/capacity and action cost remain authored data. Passive Potential adds capacity at Lv1/3/4, while Lv2's Attack4-6 gate and existing Void Area data remain intact.

Extended existing passive counters and standalone ActionResolver actions with optional meter capacity, counter spending, enemy requirements and dash commitment. Added held-dash input routing and a combat frame-data state that shares the existing cooldown and air quota. Added XY DashThroughTarget motion, fixed enemy anchors using the existing reaction/immunity/ownership path, and Physical output context. The status HUD creates a generic segmented meter above its existing layout; readiness uses an ease-out white scale/fade overlay. No character identity checks were added to runtime code.

ActionResolver exposes the new commitment and motion controls. Main's Dimension Cut is a Both-context standalone action with a temporary Dash clip and existing hit SFX/VFX. The user owns final animation, motion tuning and game-feel approval. Starting gains/timings/damage are documented in the task card, not treated as approved balance.

Validation: Unity compilation succeeded; all 24 new tests pass. A broader 48-test run has 43 passes and five existing fixture/asset failures detailed in the test plan. The generated meter preview was visually inspected. No PlayMode game-feel claim is made. See `../Specs/main-shadow-meter-impact-analysis.md` for authoring controls and `../TestPlans/main-shadow-meter-test-plan.md` for results and human playtests.

Preserved existing local edits; no commit, push, merge, dependency/build changes or scene saves were performed.

## User revision: press Dash, cache cuts, spend through Normal combo

Replaced hold-Dash routing with immediate ordinary dash. A generic passive counter Transfer operation converts one ready 20-point box into a cached cut per actual dash commitment. Dimension Cut is now a Normal standalone action costing one cached charge; reselectOnAttackInput supports repeated LMB cuts and then resumes ordinary attacks. Costs are committed once per action, not per substep, and cached cuts do not charge dash cooldown again.

Added horizontal-only target motion and configurable minimum travel (starting value 3). MatchInput preserves the grounded context. Inserted a serialized ShadowMeter row into Player_UI between its saved RuntimeStatusPanel and PlayerPanel containers, added the cached charge readout, and removed the hold timer and separate dash-attack state. The user's animation edits remain intact. Final validation is recorded in the updated test plan.

### Latest user steering

Removed the cached CUT label, enlarged meter boxes to reference-style proportions, and increased the prefab row height. Captured targets now set facing immediately and take precedence over camera-facing targets. Radial acquisition remains independent of input direction; authored target-plane alignment supports enemies above/below while keeping player movement horizontal.

Final corrected-loop verification: 39/39 Shadow Meter checks passed; broader 61/63, with two older fixture/asset expectation failures. Unity console reported no errors. Actual prefab and isolated meter renders were visually inspected and saved under Reports/MainShadowMeter. The user authorized stopping Play mode for validation.

### Fixed inconsistent target radius

Availability/commitment previously read the action-level radius while movement read the keyframe radius. Made the earliest entry target-motion radius authoritative across those paths, synchronized Animation Editor controls, and removed SnapToEnemy's literal 8-unit search. Left authored radius values intact. Verification: 47/47 feature checks passed, broader 69/71 with two older failures, no Unity console errors.

### Adapted Main Potential to cached Dimension Cuts

Implemented Apricha's requested Skill Lv4 replacement and Passive Lv1-4 revision. Skill Lv1-3 and the Ultimate track retain their effects. Skill hits at Lv4 replace the base 15% damage-taken effect with 40% (same 16-second duration) and refresh +15 CRIT RATE for every ally, including Main, for 7 seconds. Removed Lv4's old magnetism replacement.

Passive capacity progresses 2/3/3/3/5. Lv1 adds +15 CRIT RATE to Dimension Cut and Defense bypass only on its critical primary hits. Lv2 retains NA4-6. Lv3 heals 8% of actual Cut damage. Lv4 adds two boxes and doubles meter gains while Main occupies Void Area, using the user's chosen 2x multiplier. Removed old field speed, DOT Mastery and slow applications from all three field variants.

Extended generic action modifiers and centralized their Defense inputs after the crit roll. Counter Gain Modifiers use an authored runtime-effect stack requirement; field occupancy refreshes a 0.3-second marker at the existing 0.25-second interval. No Main-specific runtime condition was introduced. The action crit bonus does not mutate shared stats, and resistance/type reduction still apply.

Validation: **79/79 passed**: 61 ShadowMeterTests and 18 CombatDamageTypeTests, zero Unity console errors. Initial new effect tests incorrectly mixed local receivers with the project inventory fallback; corrected the test setup to use an isolated team inventory and verified both Main and a reserve ally. Runtime effects are transient and cleared by the script reload. Results saved to Reports/MainPotential/tests.xml. Live combat feel remains the user's review step.

### Fixed buffered facing after DashThroughTarget

With Face Target On Arrival disabled, buffered actions at the same combo depth could inherit the completed initial-facing flag because movement never observed an idle tick. PlayerCombat now notifies PlayerMovement at each committed step, resetting the step's facing state and resolving its initial auto-target turn before animation/payload processing. Automatic chains preserve facing and held directional input retains the existing override. No character-specific condition or arrival setting change is involved.

Added eight regression cases for Normal/Skill/Ultimate, both cut directions, automatic chaining and directional input, using actual ExecuteStep commitments without an intermediate movement update. Unity recompiled both runtime and Editor assemblies after these edits; scoped diff checks passed. Automated execution was unavailable in this session: the Unity connector was absent and computer-use window capture failed with `FrameArrived timed out: timed out waiting on channel`. A temporary one-shot runner never executed and was removed along with its marker. The new cases are compiled but unrun; the previous 79/79 result above predates this correction. Run `Tools/Project/Combat/Validate Main Potential` or the buffered-facing smoke test in the current test plan.
