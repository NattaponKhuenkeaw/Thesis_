# DevLog: 2026-06-03 — Player Jump and Combat Grounding Refactor

## Goal
Refactor player movement and combat grounding behaviors to resolve FSM confusion, make jump rising deterministic, and remove legacy hardcoded plunge mechanics.

## What I Did
- [x] Declared `PlayerStateId.JumpRising` enum value in [PlayerState.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Player/PlayerState.cs)
- [x] Initialized and registered the new `PlayerJumpRisingState` in [PlayerStateMachine.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Player/PlayerStateMachine.cs)
- [x] Implemented [PlayerJumpRisingState.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Player/States/PlayerJumpRisingState.cs) to allow horizontal steering (air control) and accept live actions (attacks, skills, dashes, double jumps) during the upward rise phase while ignoring buffered ground inputs, transitioning to `AirborneState` once upward momentum peaks (`VerticalVelocity <= 0.01f`).
- [x] Updated [PlayerJumpStartupState.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Player/States/PlayerJumpStartupState.cs) to transition to `JumpRisingState` upon completion
- [x] Decoupled combat movement grounding in [CombatPlayerState.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Player/States/CombatPlayerState.cs) by overriding `isGrounded` evaluation to `!player.Combat.IsAirCombo` during ground attacks. This prevents vertical displacements from `MotionData` (e.g. ground attacks pushing players up) from incorrectly triggering airborne combat states.
- [x] Simplified `IsInAirborneAction()` in [PlayerCombat.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Combat/PlayerCombat.cs) to prevent gravity suppression on ground attacks that have upward movement.
- [x] Removed all legacy hardcoded plunge attack mechanics, including methods, states, physics, and resolvers across [ICharacterCombat.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Combat/ICharacterCombat.cs), [PlayerCombat.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Combat/PlayerCombat.cs), [PlayerAttackState.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Player/States/PlayerAttackState.cs), [PlayerMovement.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Combat/PlayerMovement.cs), [PlayerAirAttackState.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Player/States/PlayerAirAttackState.cs), and [ActionResolver.cs](file:///c:/Users/Apricha/Documents/GitHub/Thesis_/Assets/Project/Scripts/Combat/ActionResolver.cs).
- [x] Verified compilation success using `unityMCP/validate_script` for all modified files.

## Key Decisions
- **Forced Grounding context during ground attacks:** Using `!player.Combat.IsAirCombo` to evaluate grounding state during active combat attacks prevents FSM confusion from upward/downward vertical movements defined in `MotionData` templates. This keeps the combo tree routing deterministic.
- **Jump Rise Interactivity:** By setting `LocksMovement = false` and enabling input flags in `PlayerJumpRisingState`, the player has full horizontal steering control and can cancel the rise with live inputs (attacks, dashes, double jumps). This keeps locomotion feeling responsive, while ignoring the input buffer during the rise ensures ground-buffered inputs do not pre-emptively cancel the jump startup phase.
- **Plunge Removal boundaries:** All runtime code references to plunge attacks were eliminated. Plunge fields inside `StandaloneAction` and the Inspector UI code were kept to prevent breaking Unity asset serialization, but they are bypassed at runtime.

## Bugs Found
- FSM confusion during ground attacks due to `MotionData` vertical displacements. Fixed by decoupling FSM grounding evaluation from physical `IsGrounded` check during ground combat actions.
- Static jump rise where movement and inputs were locked during the upward phase. Resolved by enabling active steering and action inputs in `PlayerJumpRisingState`.

## Game Feel Notes
- The initial jump rise is fully interactive, letting players steer, attack, or double jump immediately on the way up, making locomotion feel responsive and fluid.
- Ground combo attacks with vertical leaps (lifts) now correctly remain grounded actions, enabling the FSM to transition into follow-up ground combo nodes seamlessly.

## Next Session
- Integrate designer-authored air-plunge attack nodes into `ComboTrees` via the editor.

---

## Git Commit Summary
```
refactor(player): polish jump states and decouple combat grounding

- Add PlayerJumpRisingState to allow horizontal steering and active input handling during rise
- Decouple grounding checks from physical ground contact during active ground combat attacks
- Remove legacy hardcoded plunge attack mechanics across PlayerAttackState, PlayerMovement, ActionResolver, and PlayerCombat
```
