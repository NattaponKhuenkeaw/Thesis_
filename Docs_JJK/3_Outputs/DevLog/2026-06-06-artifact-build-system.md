# DevLog: 2026-06-06 - Artifact and Combat Pipeline Completion

## Goal
Finish the artifact build system branch, apply equipped artifacts to runtime
characters, and make ActionResolver damage and Aura Energy rewards resolve
from confirmed enemy hits.

## What I Did
- Completed the artifact inventory grid, preview panel, drag/drop equip, swap, removal, inspect-stat feedback, and runtime artifact deployment wiring.
- Added per-slot effective runtime stats in `TeamLoader`, including artifact
  bonuses used by HP, movement, damage, cooldown, AE, HUD, projectiles, and
  background Ultimates.
- Separated hit-confirmed AE into two sources: every damaging hit rolls the
  character's Low/Med/High rate for a fixed 10 AE base reward, while each
  action may author one guaranteed `AE Reward` (default `0`) per successful
  hitbox. Enemy defeat has no separate reward branch.
- Removed action-start AE grants, so missed attacks and out-of-combat skill spam
  cannot generate Aura Energy.
- Removed `maxAuraEnergy` from `CharacterStats` and obsolete serialized character/artifact data.
- Derived each character's maximum AE directly from its resolved Ultimate cost.
  A 50 AE Ultimate therefore has exactly 50 maximum AE.
- Corrected Ultimate readiness so capacity, HUD completion, input gating, and consumption use the same cost-derived threshold.
- Added Normal, Elemental, and Additional damage routines. Normal uses Damage
  Mastery, Elemental replaces it with Elemental Multiplier, and Additional
  Damage is a separate non-critical damage event authored by passives or
  artifacts.
- Added total action damage budgets with clamped per-hit ratios while preserving
  legacy direct hit multipliers. Genocide Cutter now deals 300% ATK over three
  equal 33.333% ratios.
- Wired `StatItemUI` on `StatItem.prefab` so artifact preview stat rows receive their labels and values.

## Bugs Found
- ActionResolver AE could be granted without an enemy taking damage, allowing
  infinite out-of-combat generation.
- Hit and defeat rewards were separate paths, which could stack instead of
  selecting exactly one final result.
- Damage calculation did not distinguish Normal, Elemental, and passive or
  artifact Additional Damage.
- Hitbox multipliers represented independent damage values, so multi-hit action
  totals were not centralized or safely adjustable.
- Artifact assignments reached the Team UI but were not applied to deployed
  runtime character stats.
- Ultimate AE cost was authored and checked at runtime, but maximum AE still came from an unrelated `CharacterStats` field.
- Maximum AE was incorrectly doubled from the Ultimate cost, making the meter and readiness rules disagree with the intended single-cost capacity.
- `StatItem.prefab` was missing its `StatItemUI` component, leaving preview stat rows unpopulated.
- Training Debug measured enemy HP loss, so overkill hits were capped by the
  target's remaining HP (for example, repeatedly showing `64`) instead of the
  final damage payload accepted by `EnemyController`.

## Validation
- Unity asset refresh, domain reload, and script compilation completed with no
  compiler errors.
- Standard Unity script validation reported zero errors in the five central
  combat and runtime-stat scripts.
- The two focused EditMode regression tests passed: Combat Actions default to
  zero guaranteed AE Reward, and overkill damage reports the final payload
  instead of the target's remaining HP.
- A short `Test_Playground` Play Mode smoke run entered and exited normally.
  Existing `DontDestroyOnLoad` hierarchy errors remain in `GameAudioManager`
  and `GameFreezeManager`, outside this task's changed paths.
- Existing obsolete-API warnings remain outside this task's scope.
- Verified new artifact scripts and prefabs are referenced by `TeamCanvas.prefab`.
- Verified `EnemyController.TakeHit` returns the same final damage value emitted
  through `OnDamaged`, and Training Debug records that value for primary and
  Additional Damage.

## Git Commit Summary
```text
feat(combat): unify hit rewards and artifact damage
```
