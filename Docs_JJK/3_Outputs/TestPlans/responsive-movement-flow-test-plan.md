---
slug: responsive-movement-flow
status: needs-human
source: manual
gdd_tags:
  - platform-input
  - combat-scope
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Responsive Movement Flow: Validation

## Completed Native Checks

Run in Unity 6000.3.4f1 using **Tools > Project > Validation > Run Responsive Movement Checks**. The validator creates temporary preview-scene fixtures with inactive gameplay facades and active bare Rigidbody2D bodies. It does not open/save/replace the user's scene or alter live player singletons. Contact is scripted for state tests; this is not a traversal playtest or collision simulation.

- [x] Ground/double jump launch immediately; resource/launch event commits once, including the existing force-immediate action-output override.
- [x] An explicitly authored nonzero jump startup still launches at its configured boundary.
- [x] Movement, stop, and rapid facing reversal have no added turnaround delay.
- [x] Ground dash publishes Active on entry, returns steering after Active, and keeps dash unavailable until exit/cooldown.
- [x] Air dash returns steering and normal gravity independent of neutral/held A/D.
- [x] Buffered landing jump uses refreshed ground resources and launches immediately.
- [x] Buffered landing dash starts without a landing movement hold.
- [x] Coyote jump does not consume the remaining air jump.
- [x] All three shipped movement profiles use zero startup and landing recovery.

- [x] Real Goenitz Animator: neutral Dash survives Recovery and gameplay exit, finishes its clip, and returns to locomotion without delaying cooldown.
- [x] Real Goenitz Animator: movement replaces Dash immediately when Recovery control is open.
- [x] Real Goenitz Animator: neutral air-dash presentation survives falling while physical gravity continues.

Results: `../Reports/ResponsiveMovementFlow/native-checks.json` — **12 passed, 0 failed**. Runtime/editor assemblies compiled; Unity console returned no C# compiler errors. `git diff --check` passed for scoped changes. Before the animation fix, the two neutral Goenitz animation regressions failed; see `dash-animation-before-fix.json` in the same report directory.

All **12 tuning assets** passed approved-field validation. Eleven matched the exact expected scoped edit from the initial working-tree snapshot. Goenitz's dash speed changed concurrently from 30 to 35 outside the startup/cancel edits; the current 35 was preserved. See `../Reports/ResponsiveMovementFlow/asset-preservation.json`.

## Human In-Game Checks Still Required

Use the existing Test_Playground and deployed characters; no new scene is required.

| Scenario | Expected outcome |
|---|---|
| Hold A/D and repeatedly jump/double jump | No anticipation pause; steering continues; release/hold duration does not change the arc |
| Alternate A/D rapidly on ground and in air | Facing follows the intended direction; no 0.2 s turnaround delay |
| Move -> Jump -> Dash -> legal Jump | Each action begins at its legal boundary; no idle/startup freeze between movement verbs |
| Neutral Dash -> no input, then Dash -> Move | A neutral clip may finish; deliberate movement immediately replaces it after burst control returns |
| Air dash with neutral A/D, then with held A/D | Normal falling resumes at the same phase boundary; no neutral-input hover |
| Queue Jump/Dash just before genuine contact | Ground resources reset first; landing feedback does not overwrite the next launch animation |
| Spam dash before recovery/cooldown ends | No extra dash resource or early cooldown reset |
| Dash/jump into walls and low ceilings | Existing collision constraints hold; no penetration or extra jump grant |
| Land on slopes and one-way edges; use Down+Jump | Contact/downward crossing and platform drop-through remain correct |
| Attack/Skill/Ultimate -> legal movement cancel | Authored MotionData and output context retain authority; no premature ground action or unrestricted cancel |
| Take damage, pause/hitstop, and swap characters | Existing interruption/resource rules hold; no stale buffered action |
| Repeat at different render rates | State timing remains tied to the existing fixed motor clock |

Judge body response separately from sprite cadence and dash deceleration. Character movement speeds, jump arc values, dash Active durations/curves, Recovery durations, and cooldown durations were not retuned by this task. If the connected flow is still slow, record the exact action pair and character before changing additional parameters.
