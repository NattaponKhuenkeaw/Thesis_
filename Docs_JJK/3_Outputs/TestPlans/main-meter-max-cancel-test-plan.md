---
slug: main-meter-max-cancel
status: draft
source: manual
gdd_tags: [combat, characters, platform-input, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main meter / QTE / MAX CANCEL acceptance

## Current native result — 2026-10-29

Focused runner after presentation/lifetime follow-up: **155/155 passed** (`Temp/main-meter-qte-max-cancel-tests.xml`, 2026-10-29 16:17:56 UTC). The prior 132 cases remain green; eight keyboard/gesture/FSM buffering cases and fifteen animation/lifetime cases were added. See `max-cancel-presentation-test-plan.md` for the current visual acceptance checklist.
Menu: Tools > Project > Combat > Validate Main Meter QTE Max Cancel.
Includes Skill-bound Ultimate type with zero AE, cross-input MAX CANCEL, commitment/opt-out,
flash snapshots, native label rendering, lower-platform QTE entry and existing meter/window cases.
The rendered label was visually inspected; it is readable and does not inherit fighter facing.

Broader runner: **186/193 passed, 7 failed**. See the report for exact failures. These checks
are still unresolved; this is not a whole-suite pass. Live human feel/acceptance remains pending.

Persistent evidence: `../Reports/MainMeterMaxCancel/native-validation-summary.json` and
`../Reports/MainMeterMaxCancel/max-cancel-preview.png`.

## Earlier automated evidence (before Unity connection recovered)

- Runtime and Editor assemblies compile using the installed Unity Roslyn response files, with
  isolated outputs under `Temp/MainMeterMaxCancelCompile`. Unity test cases compile, but are unrun.
- A .NET harness executes the actual compiled rule selection, recovery policy, counter arithmetic
  and resolver adoption code against rules extracted from the saved CharacterObject: 15 checks pass.
  This does not exercise native Unity components, collisions, UI rendering or action commitment.
- Serialization audit: resolver managed-reference section is byte-for-byte equal to HEAD; Dimension
  Slash exists only in Skill; Ultimate and QTE identities and window category filters are preserved.

Focused fixtures: `MainMeterMaxCancelTests`, `MainDimensionSlashTests`, `ShadowMeterTests`,
`QteTeamSwapTests`. Menu: Tools > Project > Combat > Validate Dimension Slash includes these.

## Gameplay acceptance checklist (automated portions now covered; human playtest pending)

1. With an enemy and non-full meter, compare each authored action's accepted hits against its
   CharacterObject rule. Whiff/block/DOT must not inherit ordinary gains. Verify cap and Void Area 2x.
2. Start with two cached cuts. CameraGirl Skill and EX must each prepare Main; swap during an allowed
   state, including during cooldown. Main must enter QteIntro/LightningPlunge, descend and land,
   retain the authored animation/VFX/camera, and own three cached cuts plus the QTE marker.
3. At four counted cuts, spend the gifted cut manually. Exactly one cache is spent, the QTE marker
   clears, and the five-second Skill offer opens. QTE readiness/grant alone must not advance the count.
4. Skill consumes Judgment Cut/Dimension Slash once, even when Dark Chain is cooling down; Ultimate
   input still selects tap Jailbreak / hold Execution. Expiry and portrait/ability UI must agree.
5. Put the original QTE target over unsupported terrain but another target on valid terrain nearby.
   Use the safe alternative. Then remove all valid targets: ordinary fallback grants no QTE reward.
6. With enough AE and a distinct Ultimate available, input it during Recovery before Cancel. The old
   action stops and the new one enters Startup; old motion/hitboxes/camera/freeze/armor do not linger.
   The destination's normal costs/cooldown apply once; MAX CANCEL appears once with its action audio.
7. Test Startup, Active, source opt-out, destination cooldown, insufficient AE/counter, wrong gesture,
   same-action input and simultaneous buffers. Rejections preserve the source and show no callout.
8. Verify left/right facing, 1280x720 and the user's display, pause, rapid repeated MAX CANCEL, scene
   change and player disable. Text must not mirror, block input, obscure the target or leak UI roots.

## Earlier access limitation (resolved for native tests)

The configured `http://127.0.0.1:8080/mcp` refused the connection. Native Editor inspection listed the
Thesis_ window and read its accessibility tree, but screenshot capture failed with
`FrameArrived timed out` / `window capture timed out`; the keyboard request failed with
`computer-use request timed out: press_key`. No live test result or rendered-label proof was obtained.

## Held Ultimate buffering follow-up — 2026-10-29

Charge hold during a committed phase and keep R down through Recovery. The held gesture must survive the ordinary buffer timeout, MAX CANCEL once at the first permitted boundary, and preserve costs/opt-out. Release before Recovery: keep the held gesture for the normal 0.15-second buffer only. Short release remains tap. Focus loss, pause/input lock, character change and disabled input must cancel it. Consuming another queued action must not requeue the still-held Ultimate.

Automated cases are in `MainMeterMaxCancelTests`; all eight new cases pass in the native focused run (140/140 total). Human feel check remains required.

