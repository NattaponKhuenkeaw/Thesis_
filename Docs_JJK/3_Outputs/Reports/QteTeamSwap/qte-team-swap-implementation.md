# QTE Team Swap - implemented

2026-10-27. Includes Apricha's playtest revision: outgoing-character offsets and strong slow motion at intro start.

## Current behavior

- Main's accepted Dimension Cut primary hit prepares CameraGirl; CameraGirl's accepted Skill / EX Skill primary hit prepares Main.
- Readiness lasts five combat seconds. Further qualifying hits refresh expiry; no duration or charge stacking. The teammate portrait shows a pulsing white overlay and an independent remaining-window bar.
- Incoming Main appears three units behind the outgoing character; CameraGirl appears one unit behind. Outgoing facing mirrors the offset. The incoming character then faces the target. This also works when Main has dashed through the enemy and is now past them.
- Main executes the actual Dimension Cut action and step data; CameraGirl executes actual NA Attack4. Original motion, hitboxes, damage budgets, action identity, audio and action events are referenced rather than approximated. No cached cut is required/spent for Main's intro, and CameraGirl does not automatically continue Attack5.
- The handoff starts the intro immediately and applies a strong slowdown: editable starting values 0.08x speed for 0.24 real seconds. CameraLookAhead resets on X only; the camera uses its normal framing behavior.
- The outgoing executor retains animation phase, current active hit history, original source damage attribution and resolved target-dash displacement. Its lifetime is derived from action/animation length rather than the old fixed three-second cutoff.
- QTE intro and off-field hits cannot generate another free QTE. Cooldown, ground context, hitstun, death, grab, input locks and auto-chain restrictions remain authoritative. Walls may clamp an entry; gaps or blocked body clearance prevent unsafe placement.

## Tune in Unity

Select **Player prefab > TeamManager > QteTeamSwapController**:

| Field | Starting value | Purpose |
|---|---|---|
| Window Seconds | 5 | Refresh-only combat-time readiness |
| Intro Time Scale | 0.08 | Smaller values make the handoff slower |
| Intro Slow Motion Seconds | 0.24 | Real-time duration; zero disables the extra slowdown |
| Reduce UI Motion | Off | Uses a steady readiness overlay instead of breathing motion |

Select either **CharacterObject > QTE Team Swap** to change `entryOffset`, trigger action ids, the source intro action/step, or ready SFX. Main offset is 3 and CameraGirl offset is 1. The actual attack timing, motion, hit payload, VFX and audio remain editable in their existing ActionResolverTables / Animation Editor.

Feel test: make ten exchanges in both directions, including swapping after Main passes through a target. The handoff should remain legible, spawn behind the outgoing facing, and return to full speed promptly. If sticky, shorten the slowdown duration first; if too subtle, lower the time scale. These are tuning starting values, not reference-game measurements.

## Verification

- Unity compilation: no compiler errors after the final changes.
- QTE-specific isolated EditMode checks: **22 passed / 22**.
- Selected QTE and character/combat regression checks: **105 passed / 107**.
- The accepted-damage pipeline test verifies real resolved damage carries the execution context and prepares readiness.
- Tests also cover refresh/expiry, invalid damage and stale executions, empty-cache intro, terminal intro completion, early confirmed-hit swaps, auto-chain exclusion, rejected-swap preservation, left/right/outgoing-facing placement, wall/gap safety, outgoing hit-history transfer, long-action lifetime, targeted-dash snapshot, and actual portrait overlay creation/clearing.
- The real Hero_TeamSwitchPanel prefab was rendered during validation; see `qte-ready-portrait.png`. This is a static ready-state preview, not a motion recording.
- Re-run from **Tools > Project > Combat > Validate QTE Team Swap** with Play mode stopped. The isolated runner does not save or replace the open working scene.

Two wider checks remain outside the QTE slice:

1. `CameraGirlPotentialTests.CameraGirl_SkillTrack_ConfiguredCorrectly` expects 33% at Skill Lv2; the existing authored asset is 43% (also present in the pre-QTE session snapshot). That balance data was preserved.
2. `PlayerActionInterruptTests.TakeDamage_DuringStartup_InterruptsIntoHitstun` compares to an uninitialized HitstunState in its EditMode fixture. The failure's expected value is null. Player damage/hitstun behavior was not changed by this feature.

The existing shop AnimatorController errors seen during the user's live Shop session were outside this work. No dependencies, build/CI configuration or scene saves were changed. Final subjective camera/slow-motion feel remains a live playtest judgment.

## Project changes

Main runtime additions: QteSwapDefinition, CombatExecutionContext, QteTeamSwapController and PlayerQteIntroState. Integration touches CharacterObject, the damage/projectile context path, action execution/resolution, team switching, input legality, safe grounded placement, outgoing continuation and CharacterSwitchSlot presentation. Both requested character profiles and the Player prefab's saved QTE settings are configured. Canonical GDD swap rules, implementation plan, specs and test plan reflect the playtest revision.

## Camera follow-up - X-only reset

The swap resets CameraLookAhead X and clears its horizontal tween and smoothing velocity. UnityMCP inspection showed that this alone cannot center the rendered camera: Combat follows CombatTargetGroup with separate damping, while Explore has its own composer offset, dead zone and damping. The intro extension therefore centers the generated Cinemachine state on the incoming character's X for the existing intro slowdown duration, then eases back to ordinary framing over CameraLookAhead's configured horizontal tween duration. This applies to each camera contributing to a blend. It preserves Y, group membership and group size calculations, and clamps X to the room BoxCollider2D using the current viewport width. The proxy's offset remains zero until a later facing change. Compilation passed with no console errors; a paused live QTE inspection is pending. No scripted tests were run.

Compilation/console verification only. No scripted tests were added or run for these camera revisions; Apricha handles E2E. The earlier test counts predate them.

## Intro camera impact

At intro start, orthographic size is reduced by 1 immediately, then eases back to the normal group-framed size over 60 rendered frames (editable, allowed range 1-120). SmoothStep makes the recovery gradual; the effect does not alter the authored lens or stack repeated reductions. Rendered-frame duration is independent of slow motion. Edit Intro Camera Size Reduction and Intro Camera Impact Frames on Player prefab > TeamManager > QteTeamSwapController. The current prefab slowdown duration of 1 second was preserved. Compilation/console checks only; no scripted tests added or run.
