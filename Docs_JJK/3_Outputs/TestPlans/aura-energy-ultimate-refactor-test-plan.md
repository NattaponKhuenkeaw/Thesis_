# QA Test Plan: Refactor Ultimate System to Aura Energy

This test plan defines the validation strategy for the transition of the Player Ultimate ability from a time-based cooldown system to an **Aura Energy (AE)** resource-gated system, including team-swap continuity via the background executor.

---

## 1. Test Plan Summary

| Test Area | Priority | Type | Platform |
| :--- | :--- | :--- | :--- |
| **Aura Energy Accumulation** | P0 | Functional / Automated | PC (Windows) / Gamepad |
| **CDR & Cost Calculations** | P0 | Functional / Automated | PC (Windows) / Gamepad |
| **Ultimate Cast & Lifecycle** | P0 | Functional | PC (Windows) / Gamepad |
| **Interruption & Invulnerability** | P0 | Functional | PC (Windows) / Gamepad |
| **Team Swap & Executor Continuity** | P0 | Functional | PC (Windows) / Gamepad |
| **UI & Audio Feedback Loop** | P1 | Functional / Cosmetic | PC (Windows) / Gamepad |
| **Upgrade System Interaction** | P1 | Functional | PC (Windows) / Gamepad |
| **Game Optimization & GC Budget** | P1 | Performance | PC (Windows) / Gamepad |

---

## 2. Functional Tests

```
TEST: AE_Gain_On_Normal_Hit
GIVEN: The active player character is in combat and has current Aura Energy (AE) less than Max AE.
WHEN: The player executes a basic attack that successfully hits a surviving enemy.
THEN: There is a 20% chance to generate AE equal to 10f * auraEnergyMultiplier, triggering the particle spark trail and metallic suction SFX.
PRIORITY: P0
```

```
TEST: AE_Gain_On_Enemy_Defeat
GIVEN: The active player character is in combat and has current Aura Energy (AE) less than Max AE.
WHEN: The player executes an attack that defeats an enemy.
THEN: There is a 100% chance to generate AE equal to 10f * auraEnergyMultiplier, triggering the particle spark trail and metallic suction SFX.
PRIORITY: P0
```

```
TEST: AE_Cost_Calculation_With_CDR
GIVEN: A character is loaded into the party with a Cooldown Reduction (CDR) stat.
WHEN: The system calculates the ultimate cost as Base Cost (100 AE) * (1.0 - Math.Min(0.50f, CDR * 0.50f)).
THEN: The calculated cost scales dynamically down to a minimum of 50 AE at 100% CDR and is capped at a maximum of 50% cost reduction.
PRIORITY: P0
```

```
TEST: Ultimate_Cast_Attempt_Sufficient_AE
GIVEN: The active character's current AE is greater than or equal to their calculated Ultimate Cost.
WHEN: The player presses the Ultimate key (Q) or corresponding Gamepad button.
THEN: The player transitions to PlayerUltimateState, enters the Startup Phase, and transitions to the Active Phase where all AE is consumed to 0.
PRIORITY: P0
```

```
TEST: Ultimate_Cast_Attempt_Insufficient_AE
GIVEN: The active character's current AE is less than their calculated Ultimate Cost.
WHEN: The player presses the Ultimate key (Q) or corresponding Gamepad button.
THEN: The action is rejected, the input buffer discards the command, the HUD meter flashes red twice, and the reject buzz SFX plays.
PRIORITY: P0
```

```
TEST: Ultimate_Active_Phase_Juice
GIVEN: The active character transitions to the Active Phase of their Ultimate.
WHEN: The Active Phase begins.
THEN: Invulnerability and Super Armor flags are set to true; the screen desaturates (0.15s), hit-stop occurs (0.15s / 9 frames), screen shake triggers (0.35 intensity, 0.4s duration), camera zooms (FOV 60 to 50, 0.1s speed), 15-particle radial emitter spawns, sub-bass drop SFX plays, and background music volume ducks by -6dB during hit-stop.
PRIORITY: P1
```

```
TEST: Ultimate_Recovery_Phase_Execution
GIVEN: The active character finishes the Active Phase of their Ultimate.
WHEN: The character enters the Recovery Phase (0.4s / 24 frames).
THEN: Invulnerability and Super Armor flags are reset to false, and normal combat inputs remain locked until the recovery duration ends.
PRIORITY: P0
```

```
TEST: Ultimate_Startup_Phase_Interruption
GIVEN: The active character is in the Startup Phase (wind-up) of their Ultimate.
WHEN: The player is hit by an interrupting enemy attack.
THEN: The Ultimate is cancelled, the state resets, no AE is consumed, and no ultimate damage is dealt.
PRIORITY: P0
```

```
TEST: Team_Swap_During_Ultimate_Active
GIVEN: The active character is in PlayerUltimateState and is in the Active Phase.
WHEN: The player presses a team swap key (1, 2, or 3) to switch characters.
THEN: A BackgroundUltimateExecutor is spawned at the player's position, copying the casting character's sprite, animator, facing direction, current frame, remaining active frames, hitboxes, and stats to finish the ultimate in the background. The swapped-in character takes the field instantly.
PRIORITY: P0
```

```
TEST: Team_Swap_Input_Inheritance
GIVEN: The player initiates a swap during an Ultimate.
WHEN: The swap occurs while inputs are queued in the 12-frame buffer.
THEN: The swapped-in character inherits the buffered input state and immediately chains into the queued action.
PRIORITY: P1
```

```
TEST: Benched_Character_Aura_Energy_Scaling
GIVEN: A benched character's Max AE increases from 100 to 120 due to a Soul Shop upgrade.
WHEN: The team data is updated in memory.
THEN: The benched character's HUD portrait ratio updates dynamically (e.g., displaying 100/120 AE) via OnCharacterAuraEnergyUpdated event without memory loss.
PRIORITY: P1
```

---

## 3. Edge Case Tests

- [ ] **Rapid repeated input (Spamming Q):** Verify spamming the ultimate input button while energy is empty, partially full, or currently casting does not cause duplicate execution or double-dipping of AE depletion.
- [ ] **0 Health / Death during Ultimate Startup:** Confirm that if a player dies on the exact frame the Ultimate Startup begins, the player dies, the Ultimate halts, and the game correctly triggers the defeat/respawn screen.
- [ ] **Scene Transition mid-Ultimate:** Verify that loading a new scene (e.g., CombatRoom to RewardRoom) while an Ultimate is being cast or while a BackgroundUltimateExecutor is active cleanly destroys the executor and transitions the player state safely.
- [ ] **Pause game mid-action:** Verify that pausing the game mid-Ultimate or mid-swap freezes the hit-stop timers, screen-shake curves, and the BackgroundUltimateExecutor physics ticks, resuming correctly upon unpause.
- [ ] **First frame after scene load:** Ensure that TeamLoader initializes all character AE values correctly on scene start and binds UI portraits without throwing null reference exceptions.
- [ ] **Maximum Values:** Verify correct scaling calculations when stats are pushed to limits: Max AE (150+), 100%+ Cooldown Reduction (cost capped at 50% max reduction), and extreme Aura Energy Multipliers.
- [ ] **Null/Missing Dependencies:** Confirm that if UI elements or particle managers are missing from the scene, combat and state transitions still execute without throwing critical script-breaking NullReferenceExceptions.
- [ ] **Multiple Background Executors:** Swapping rapidly back and forth while casting Ultimates on multiple characters. Verify that multiple BackgroundUltimateExecutors can run concurrently without crossing stats or damage outputs.

---

## 4. Performance Tests

- [ ] **Frame Rate Stability:** Ensure the frame rate remains stable during the VFX-heavy Ultimate cast, maintaining target budgets of **60 FPS stable** on PC and **30 FPS stable** on Mobile targets.
- [ ] **GC Allocations in Gameplay:** Verify using the Unity Profiler that `BackgroundUltimateExecutor` ticks (Physics2D Overlap checks) result in **0 bytes of GC allocation** per frame using the pre-allocated non-allocating overlap checks.
- [ ] **Scene Load Time:** Confirm scene load times containing the new UI components and energy particle systems remain under **2.0 seconds** on PC and **3.0 seconds** on Mobile.
- [ ] **Memory Usage:** Verify total memory footprint remains within budget limits (**< 1 GB** for PC, **< 512 MB** for Mobile).

---

## 5. Platform-Specific Tests

| Test | Primary Platform (PC Keyboard & Mouse) | Secondary Platform (Gamepad Controller) |
| :--- | :--- | :--- |
| **Ultimate Activation** | Verify pressing **Q** triggers the Ultimate when AE cost is met. | Verify pressing the mapped Gamepad Ultimate button triggers the Ultimate when AE cost is met. |
| **Swapping Characters** | Verify pressing **1, 2, or 3** triggers character swap. | Verify pressing mapped D-pad/Trigger buttons triggers character swap. |
| **Failed Cast Feedback** | HUD flashes red, reject SFX plays on screen. | HUD flashes red, reject SFX plays, and controller rumble trigger (if supported). |

---

## 6. Regression Checklist

_Systems that could break due to this change:_
- [ ] **Team Swapping System** — Verify character swap cooldowns, portrait updates, and that benched characters retain their current HP and status effects during and after swap execution.
- [ ] **Hitbox Damage Resolver** — Ensure that `HitBoxDamage` hit calculations do not register double-hits or ignore standard damage multipliers during high-frequency combat.
- [ ] **Audio Manager & Volume Ducking** — Verify that the -6dB background music volume ducking during hit-stop properly restores back to 0dB baseline volume after the 0.15s hit-stop window expires.
- [ ] **Player Finite State Machine (FSM)** — Verify that movement transitions (Airborne/Ground state overrides) function correctly before, during, and after casting the Ultimate.

---

## 7. Single-Player / Offline Bounds

> [!NOTE]
> Neo-JJK is a single-player offline game. There is no network synchronization or server authority validation required.

- [ ] **Offline Save Persistence:** Verify that character Aura Energy values are correctly serialized/deserialized when saving/loading run data, ensuring AE stats persist correctly between gameplay segments.

---

## 8. Bug Report Template

```
🐛 BUG: [Short descriptive title]
SEVERITY: Critical / Major / Minor / Cosmetic
REPRO STEPS:
  1. Load character with [X] stats in scene [Name].
  2. Perform [Action 1].
  3. Perform [Action 2].
EXPECTED: [What should have happened according to specs]
ACTUAL: [What actually happened]
PLATFORM: PC (Windows) / Gamepad model
FREQUENCY: Always / Sometimes / Rare
SCREENSHOT/VIDEO: [Link or absolute file path to visual proof]
```
