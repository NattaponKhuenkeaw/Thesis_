# DevLog: 2026-07-12 - Scene Boundaries and Training Team Damage

## Goal

Keep room entrances/exits physically and visually aligned with the Cinemachine
confiner, expose a reusable room-clear exit API, and add TeamLoader-aware damage
contribution metrics to the training HUD.

## What I Did

- [x] Added scene-scoped `SceneBoundaryController` ownership behind the persistent `SceneTransitionManager` facade.
- [x] Opened the left boundary for `PlayerIntroState`, then sealed it at Intro completion/cleanup.
- [x] Locked exits at the transition trigger's near edge and opened walls/confiner beyond its far edge plus padding.
- [x] Added logical trigger gating, rightward camera cue, Admin `Clear State`, and Shop `ExitShop` wiring.
- [x] Added TeamLoader roster tracking plus per-character Total, Team Share, and Max Hit rows.
- [x] Routed accepted enemy damage with its original `sourceCharacter` and unified the 6.7-second sample reset.

## Key Decisions

- Scene-local objects own walls/confiner state; the persistent manager only coordinates and exposes a stable API.
- The exit trigger's bounds are the authored boundary authority because current scenes do not use a consistent initial right confiner edge.
- Damage attribution uses the accepted damage payload's `sourceCharacter`, preserving delayed projectile and background-Ultimate ownership after character switches.

## Bugs Found

- [x] Collider bounds lagged one frame after moving a wall; fixed with explicit `Physics2D.SyncTransforms()` on rare boundary state changes.
- [x] Scene reload with an unchanged roster could retain an old training sample; scene-load rebinding now forces a roster notification/reset.
- [x] Initial compile exposed `System.Object` versus `UnityEngine.Object` ambiguity; qualified Unity lookup calls.

## Game Feel Notes

The exit cue is an additive rightward ease-out-and-return on `CameraLookAhead`,
so it does not change player facing or permanent look-ahead. Distance and duration
remain serialized per scene for the final human feel pass.

## Verification

- Unity compile/import: clean after final verifier removal.
- `Test_Playground`: real boundary geometry, three-character damage table, max-hit/share math, and 6.7-second reset passed.
- `Test_Playground -> Test_Shop`: actual transition, Intro completion, locked shop exit, persistent `ExitShop` listener, and shared unlock API passed.
- Serialized Admin/Shop prefab listeners and both scene controller references were imported by Unity without errors.

## Next Session

- Human-controlled round-trip playtest and camera-cue feel tuning before merge.

---

## Git Commit Summary

```text
feat(scene): coordinate room boundaries and training team damage

- synchronize entrance and exit walls with Cinemachine confiner bounds
- expose reusable room exit availability with debug and shop controls
- add TeamLoader damage share and max-hit training metrics
```
