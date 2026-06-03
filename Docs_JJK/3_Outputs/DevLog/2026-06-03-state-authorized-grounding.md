# DevLog: 2026-06-03 - State-Authorized Grounding Guardrail

## Goal
Refactor player grounding authority so `MotionData` Y impulses do not automatically make every ground-started combat action logically airborne.

## What I Did
- [x] Added a `GroundStateAuthority` layer to [GroundStateProvider.cs](/C:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Player/GroundStateProvider.cs) so gameplay can distinguish physical floor contact from state-authorized grounded/airborne logic.
- [x] Added `allowGroundToAirTransition` to [ComboNode.cs](/C:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Character/ComboNode.cs) so specific combat actions can opt into ground -> air promotion.
- [x] Updated [CombatPlayerState.cs](/C:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Player/States/CombatPlayerState.cs), [PlayerCombat.cs](/C:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Combat/PlayerCombat.cs), and [PlayerMovement.cs](/C:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Combat/PlayerMovement.cs) so non-authorized ground-started actions stay logically grounded, while authorized actions promote to airborne only after they physically leave the floor.
- [x] Split the player prefab body collider away from the nested `Visual` node in [Player.prefab](/C:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Prefab/Player.prefab) by adding a root-level `Collision` child, moving `GroundC` under it, and relocating the main body `BoxCollider2D` there.

## Key Decisions
- Chose a logical authority layer over an immediate collider hierarchy split because it is the smallest change that addresses the current bug without moving the whole player rig.
- Kept `MotionData` Y as physical Rigidbody impulse for now. The new guardrail changes when gameplay treats that lift as airborne, not how authored motion is generated.
- Used sticky grounded authority for ground-started actions that are not allowed to promote airborne, so cancel/interrupt paths can return to grounded locomotion rules instead of immediately falling into air state selection.
- Moved the body collider to a dedicated prefab child so future visual-only lift/offset work can happen without the main ground-check/collision node having to follow the sprite hierarchy.

## Bugs Found
- [x] Ground combat actions could still become logically airborne after the June 3 refactor because locomotion, animator grounding, and exit-state routing still consumed collider-driven ground truth.
- [x] Ground-cast `Both` actions could still route into air attack cancels because `IsGroundedForActionSelection()` checked upward lift before the sticky grounded authority/cast-context latch.

## Game Feel Notes
Ground-started launchers should now remain grounded unless the action is explicitly marked to allow ground -> air promotion. Actions that opt in should still transition cleanly into airborne behavior once the body actually leaves the floor.

Ground-cast `Both` actions now keep their cast context for follow-up routing while the grounded authority guardrail is active, so authored Y lift can stay visual/physical without silently switching cancel rules to air.

## Next Session
- Playtest which authored actions should enable `allowGroundToAirTransition`.
- If visual lift still feels wrong for non-authorized actions, move Y-only presentation offsets onto the visual branch and keep physical motion on the collision/root branch.

---

## Git Commit Summary
_Copy-paste this into your commit message:_

```text
refactor(player): add state-authorized grounding guardrail

- add logical ground authority on top of collider contact
- gate ground-to-air promotion behind combat action data
- keep non-authorized ground actions grounded across cancel and exit
```
