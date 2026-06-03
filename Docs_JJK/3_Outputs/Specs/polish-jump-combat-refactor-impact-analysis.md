# Impact Analysis: Polish Jump & Combat Refactor

**Refactor Name:** Polish Jump & Combat FSM/Gravity decoupling
**Author:** implementation-agent
**Date:** 2026-06-03

## Refactor Goal

Extract and decouple physical motion forces from logical FSM grounded/airborne states to:
1. Prevent `MotionData` vertical impulses in ground attacks from causing accidental airborne state transitions or FSM combo routing confusion.
2. Make jump execution deterministic by splitting jump into a purely vertical rising phase (`PlayerJumpRisingState`) and a steerable/input-consuming airborne phase (`PlayerAirborneState`).
3. Remove the legacy hardcoded plunge attack flow in favor of clean, designer-authored combo branching.

---

## Affected Systems & Blast Radius

- **FSM Transitions:** High. Changing how ground/air states are resolved during combos and landing exits.
- **Physics Coupling:** Medium. Restoring standard gravity scales for ground combat actions that apply vertical motion forces.
- **Editor Tooling:** None. We preserve the `isPlungeAction` field in `StandaloneAction` data definitions to maintain Unity asset serialization compatibility, but bypass it at runtime.

---

## Class Responsibility Table (Before -> After)

| Class | Before | After |
|---|---|---|
| `PlayerStateId` | Contains normal movement, combat, and hit states. | Added `JumpRising` enum value for the new rising phase. |
| `PlayerStateMachine` | Initializes and routes inputs for `JumpStartup`, `Airborne`, and combat states. | Also initializes and routes inputs to `JumpRisingState`. |
| `PlayerJumpRisingState` [NEW] | *None (Created)* | Manages the purely upward rise phase. Locks horizontal movement and ignores input buffer actions until vertical velocity <= 0. |
| `PlayerJumpStartupState` | Transitions directly to `AirborneState` on completion. | Transitions to `JumpRisingState` on completion. |
| `PlayerAirborneState` | Active from the moment jump startup finishes; consumes input buffers immediately. | Active after `JumpRisingState` apex is reached; consumes input buffers and enables air control. |
| `CombatPlayerState` | Resolves input context grounding dynamically via physical raycasts. | Caches entry grounding state. Uses `!isAirCombo` to guarantee grounded action context for ground actions. |
| `PlayerCombat` | Tracks `isPlungeFalling` flag and determines airborne state using physical checks. | Removes plunge flags. Checks `isAirCombo` directly to determine if in an airborne action. |
| `ActionResolver` | Walking stateful combo trees with custom hardcoded plunge transitions. | Walks stateful combo trees only. Hardcoded plunge states and methods removed. |
| `PlayerAttackState` | Integrates hardcoded plunge charge, steering, dive, and landing logic. | Legacy plunge methods and fields removed. Operates purely as a standard combat state. |
| `PlayerMovement` | Owns unused plunge methods (`StartPlungeMotion`, `ApplyPlungeChargeLift`). | Legacy plunge methods removed. Gravity is unaffected by ground combat vertical motions. |

---

## Verification Plan

### Manual Verification
- Execute ground attack combos that contain vertical movement (such as launcher attacks). Verify that the player does not float indefinitely, normal gravity pulls the player back down, and subsequent inputs resolve to ground combo nodes.
- Perform a normal jump. Verify that the player rises straight up and cannot steer horizontally during the initial rise, and that holding Attack or Dash during the startup does not cancel the jump prematurely. Verify that the player can steer normally after reaching the apex.
