# DevLog: 2026-10-21 - Combat Room Restart Parallax Reset

## Goal

Reset the Combat 1 room shell and its parallax positions when restarting the currently mounted room.

## What I Did

- [x] Routed same-room re-entry through a full shell-scene reload.
- [x] Kept in-place layout swaps for transitions to a different room in the same shell.
- [x] Avoided a duplicate prefab mount when the reloaded shell already bootstrapped the requested room.
- [x] Replaced per-frame accumulation of whole-tile offsets with a deterministic offset calculated from total camera travel.

## Key Decisions

- A same-room re-entry is treated as a restart; scene-authored transforms and room prefab state reset together.
- The shell bootstrap remains the source of the initial layout after reload.
- Infinite-loop displacement is a periodic offset from the authored start position; the start position must not absorb the full accumulated travel repeatedly.

## Bugs Found

- [x] Same-shell room transitions remounted the layout and wrapped the player without reloading the shell; stale runtime position state could leave Infinity parallax misaligned after a room restart. Same-room re-entry now reloads the shell.
- [x] `ParallaxBackground` added the total whole-tile displacement to `_startPosition` every `LateUpdate` after crossing the tile threshold. Since total travel continued growing, the same large displacement was added repeatedly and pushed Infinity layers out of frame, including after a restart. Loop offsets now derive from total camera travel while `_startPosition` remains fixed.

## Game Feel Notes

Not playtested in Unity during this change. Existing fade and entrance flow remain in place.

## Next Session

- Play Combat 1, move far enough to trigger parallax looping, restart the same room, and confirm the background returns to its authored alignment.
- Verify in Unity that both Infinity X and Infinity Y remain aligned after multiple tile crossings and a same-room restart. Runtime playtest is still pending.

---

## Git Commit Summary

```text
fix(parallax): stop infinite layers drifting out of frame

- calculate whole-tile offsets from camera travel without accumulating them into the authored start position
- reload same-room prefab layouts to reset scene and parallax positions
- skip mounting a layout already loaded by the shell bootstrap
```
