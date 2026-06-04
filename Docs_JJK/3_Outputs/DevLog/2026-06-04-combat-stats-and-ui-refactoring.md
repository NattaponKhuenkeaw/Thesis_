# DevLog: 2026-06-04 — Combat Stats and UI Refactoring

## Goal
Implement 9 new combat stats (ATK, DEF, SPD, CDR, CRIT, CRIT DMG, SKILL Mastery, DMG Mastery, DMG Res) and refactor the Team Builder UI to include a Combat Zone pause mode with a detailed Inspect Screen.

## What I Did
- [x] Created `CombatFormula.cs` to act as a centralized math utility for damage mitigation and outgoing multipliers.
- [x] Updated `CharacterStats.cs` to house the new 9 combat statistics.
- [x] Modified `PlayerMovement.cs` to accept a dynamic `speedOverride` driven by the character's SPD stat.
- [x] Refactored `TeamBuilderUI.cs` with a new `TeamBuilderMode` enum (SafeZone vs CombatZone) to lock drag interactions and pause Time.timeScale.
- [x] Created `InspectScreenUI.cs` to display the detailed breakdown of the 9 stats when a team slot is clicked during CombatZone mode.

## Key Decisions
- Chose a centralized static utility `CombatFormula.cs` over embedding the math inside `PlayerCombat` or `EnemyController` to keep logic pure and unit-testable.
- Modeled the SPD display as an integer string format `"10(+0)"` directly in the UI instead of modifying the underlying float struct to stay aligned with the requested UI visual.

## Bugs Found
- [x] Missing path for Y-velocity accumulation in jumping vs standard gravity - solved by ensuring `BaseMoveSpeed` handles horizontal speed cleanly without corrupting the jump frame data logic.

## Game Feel Notes
_How does it feel when playtested? What needs juice/tuning?_
- The game will instantly pause (`Time.timeScale = 0f`) upon entering the Team menu in combat. This might feel too abrupt. We might want to add a UI blur transition or a slight slow-down (hitstop-esque) before the full pause to make it juicier.

## Next Session
- Implement Artifact mathematical bonuses directly into the newly established 9 stats.
- Map the Potential 1-6 UI text in `InspectScreenUI.cs` to actual upgrade nodes.

---

## Git Commit Summary
_Copy-paste this into your commit message:_

```
feat(combat-ui): implement 9 combat stats and inspect screen

- Create CombatFormula for universal damage/defense calculations
- Add ATK, DEF, SPD, CDR, CRIT, CRIT DMG, Masteries to CharacterStats
- Refactor TeamBuilderUI to handle CombatZone pause mode
- Create InspectScreenUI to display full character details and stat breakdown
```
