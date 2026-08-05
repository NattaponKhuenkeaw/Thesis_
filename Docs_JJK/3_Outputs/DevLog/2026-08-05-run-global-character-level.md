# 2026-08-05 - Run-Global Character Level

Replaced the active per-character EXP progression path with one run-scoped level shared by the complete character roster.

- Added a persistent Run Global Level authority to `PlayerRunInventory`, starting at Lv.1, capped at Lv.30, and reset with run progress.
- Resolved every character's authored ATK, DEF, and Max HP growth from the shared level without creating ownership or run-character records for unowned characters.
- Added a public `AdminConsole.TriggerGlobalLevelUpEvent()` prototype hook with the approved +1/+2/+3 weights of 65%/25%/10%.
- Rebuilt all deployed effective stats through `TeamLoader` after a global-level change while preserving absolute current HP.
- Changed active Inspector and team level readouts to the concise shared `Lv.X/Y` presentation and disabled the individual EXP page route.
- Retained and labeled the serialized individual level, EXP, Leveling Potion, and Character Level panel implementation as legacy migration code.
- Rewrote the relevant GDD direction and specified the future safe `Level` room flow, failure recovery, and implementation boundary.
- Added ADR-007, implementation/design artifacts, and an automated Run Global Level regression suite.
- Verified Unity compilation, 12/12 focused tests, and 17/17 full EditMode tests in Unity 6000.3.4f1.

## Game Feel Notes

The prototype preserves absolute current HP when Max HP grows, so leveling does not silently heal the active character. Human PlayMode review should decide whether the final room instead preserves HP percentage or grants the Max-HP delta. Camera timing, number-roll pacing, SFX, controller focus, motion-sensitivity handling, and the per-room claim rule remain checkpoints for the separate Level-room task.

## Next Session

- Bind the prototype method to a clean admin button or implement the dedicated `Level` room controller.
- Run the manual save/relaunch, deployed-team, Artifact, Potential, KB+M/controller, and profiler checks in the approved test plan.
- Obtain human approval for the room sequence and current-HP feel policy.

## Git Commit Summary

```text
feat(progression): add run-global character level

- share one run level across the complete roster
- add weighted admin level-event prototype and stat refresh
- mark individual EXP UI legacy and update GDD direction
```
