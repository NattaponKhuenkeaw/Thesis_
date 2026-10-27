---
slug: qte-team-swap
status: draft
source: manual
gdd_tags: [combat, combat-scope, characters, platform-input, visual-audio]
owner: game-design-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

## Implemented revision - 2026-10-27

Apricha approved implementation, then playtested and requested strong slow motion at intro start and spawn offsets relative to the outgoing character. This revision supersedes the historical enemy-relative/no-slow-motion proposal below.

Current rule: spawn X = outgoing X minus outgoing facing times entryOffset (Main 3, CameraGirl 1), collision-limited and grounded. After placement, face the selected enemy and execute the exact referenced attack. Starting handoff slowdown: 0.08x speed for 0.24 real seconds. Settings are saved on Player prefab > TeamManager > QteTeamSwapController; entryOffset and exact action/step references are on each CharacterObject's QTE Team Swap profile. The five-second refresh-only window, accepted-hit triggers and anti-loop rules remain.

Tuning test: try both swaps and ten quick exchanges with a target behind and ahead. Pass when the outgoing body remains readable, the new body appears behind the outgoing facing, and slowdown clarifies control transfer without stalling the exchange. If too sticky, shorten introSlowMotionSeconds first; if too subtle, lower introTimeScale. These are editable starting values, not measurements from the reference games.

# QTE Team Swap — validation plan

Prepared 2026-10-27. Not yet run: this is a design-stage test plan, not a QA pass.

## Functional checks

| Scenario | Pass condition |
|---|---|
| Main Dimension Cut whiffs, then hits | Whiff produces no offer. Accepted positive primary damage opens eligible teammate offers. |
| CameraGirl Skill and EX Skill | Each qualifying Skill hit refreshes Main's offer; casting or missing does not. |
| Repeated hits near expiry | Deadline becomes last qualifying hit plus five combat seconds, with no extra charges/duration stack or repeated ready-audio spam. |
| Expiry / pause / hitstop | HUD and input agree on expiry; pausing combat does not burn the window. |
| Swap Main into an empty cut cache | Main still performs the intro. Cached-cut count and dash charge do not change. Manual Dimension Cut retains its cost. |
| Swap CameraGirl during Skill cooldown | Her Attack4 intro remains available through valid readiness; Skill cooldown and AE are not consumed/reset. |
| Attack data comparison | Main intro matches Dimension Cut clip, frame phases, motion, hit payload and action passives; CameraGirl matches all Attack4 hit entries, motion, audio and afterimage. |
| Intro completion and fresh input | No automatic Attack5. Fresh legal attack chains into incoming normal combat at authored cancel; outgoing inputs are cleared. |
| Hit-confirm handoff | A ready QTE accepts from the current grounded step after its accepted hit, while an unconfirmed startup does not gain an early cancel. |
| Outgoing continuation | Executor begins at captured frame and animation phase. No already-processed hit or once-only AE reward replays. Damage retains outgoing source attribution. |
| Long outgoing animation | Executor remains until legitimate action/animation completion, including durations beyond its current three-second safety cap. |
| Ordinary swap regression | Without readiness, existing recovery gate, cooldown, clone and locomotion behavior remain correct. |

## Environment and lifecycle

- Approach the same enemy from each side. Main starts three units away and CameraGirl one unit away on the approach side, facing the target, subject to safe-placement limits.
- Try equal X, enemy above/below the player, target death during confirmation, target destruction before swap, target beyond allowed range and several adjacent enemies. Capture/reacquire must be deterministic and remain in range.
- Try a wall immediately behind the desired spawn, low ceiling, slope, ledge, gap and moving platform. No body penetration, fall-through, arbitrary enemy-height teleport or unintended cross-gap relocation.
- Test hitstun, death, grab ownership, auto-chain, menu input lock, cinematic traversal, airborne action and shared cooldown. Rejected QTE attempts consume nothing and do not create an executor.
- Test loadout deploy, roster compaction after KO, scene transition, object disable, domain reload with the project's configured play settings, and run restart. No stale offers, event subscriptions or portrait animations survive ownership changes.
- Verify keyboard and clickable HUD use identical conditions. Existing controller mapping coverage must be inspected before claiming controller support.

## Significant-feature playtests

1. **New player:** ten unexpected activations during normal fighting. Starting pass target: identify the ready teammate in at least nine and understand that the prompt is optional. If unclear, improve cue/availability before expanding the timer.
2. **Stress:** spam swap and attack around readiness expiry, hit confirmation and cooldown end at different frame rates. One accepted swap creates one intro and at most one continuation of the outgoing action; no double damage or orphan motion.
3. **Skill:** compare waiting for normal recovery with spending the ready offer immediately after a legal hit. The prepared handoff must start visibly sooner and keep the enemy in the exchange without requiring a walk-in correction.
4. **Abuse:** alternate intros without manual trigger attacks, including intro clones, DOT, artifact effects and delayed off-field hits. None may create an unlimited free relay. Normal earned triggers must still refresh properly.
5. **Readability:** record left/right entries and crowded combat with both actions visible. A viewer should identify the new controlled character and attack target without a camera cut or obscuring white screen flash.

## Evidence to capture after implementation

- Unity compilation/console result and relevant automated timer, trigger-provenance and consumption checks.
- Recorded swaps showing no idle/landing frame between replacement and intro, with input accepted and action-start markers.
- Source-attack versus intro damage/hit logs for both characters.
- Scene/terrain and long-animation cases above.
- Human observations for response, cue clarity, camera continuity and overlapping audio/VFX.

Numbers for new feedback are starting values from the design spec. Existing authored attacks and user-specified five-second/offset values are separate acceptance facts. A subjective “feels smooth” claim requires the live human pass.
