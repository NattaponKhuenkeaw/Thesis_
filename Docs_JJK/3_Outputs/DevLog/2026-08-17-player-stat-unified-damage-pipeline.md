# 2026-08-17 - Player Stat and Unified Damage Pipeline

Implemented the approved behavior-changing refactor that separates damage
delivery from elemental type and establishes DM, DI, and ADI as the three
offensive build paths.

## Changes

- Added the catalog-backed ATK, HP, DEF, CM, CR, CD, DM, DI, ADI, and AER
  vocabulary; SPD remains system-only and is omitted from build UI and artifact
  application.
- Added a shared `DamageRequest` / `DamageResult` contract, pure resolver, and
  application processor for Primary, DOT, and Additional damage.
- Migrated melee, grab, projectile, background team-switch, artifact/potential,
  runtime-effect, and dash-through producers to the shared delivery pipeline.
- Centralized outgoing modifiers, mitigation, elemental reduction, target
  application, DOT application, Primary on-hit notification, and AE dispatch.
- Replaced stackable DOT with snapshotted strongest-replace/equal-refresh logic.
- Removed `DamageRoutine`, `damageIsFinal`, legacy particle formula branches,
  and `maxStacks`; renamed the authored type-reduction bypass explicitly.
- Migrated serialized CharacterStats keys and AER decimal values. Legacy
  Damage Reduction values on artifacts were cleared and not mapped to ADI.
- Added the shared CharacterStats property drawer, catalog-driven artifact
  preview, ten-row player stat UI, migration validator, ADR-008, design/impact
  documents, regression plan, and GDD updates.

## Verification

- `git diff --check`: pass.
- Unity 6000.3.4f1 runtime assembly compilation via the project Bee/Roslyn
  references: pass with zero errors.
- Unity Editor/test assembly compilation via the same references: pass with
  zero errors.
- Legacy serialized stat keys, `DamageRoutine`, `damageIsFinal`, and
  `maxStacks` reference audit: pass; only intentional
  `FormerlySerializedAs` migration aliases remain.
- EditMode test coverage now includes the five delivery/type matrix paths,
  multiplicative build stats, mitigation, critical opt-in, type reduction,
  non-negative clamping, stat visibility, and DOT snapshot replacement.

## Human E2E Checkpoint

- Run the EditMode suite in the open Unity Editor.
- Play representative melee, projectile, grab, background switch, runtime
  Additional, artifact Additional, and dash-through scenarios.
- Inspect migrated assets/prefabs for missing references and run the Training
  Debug comparisons for DI, DM, and ADI.
- Profile representative combat for the zero per-hit GC target.

## Follow-up Scope

- Artifact random-roll generation.
- Enemy-to-player damage pipeline migration.
- Terrain/Particle lifetime, cadence, and targeting emitters; they can submit
  the public `DamageRequest` contract without another formula path.

## Git Commit Summary

```text
refactor(combat): unify player stats and damage delivery

- add catalog-backed DM, DI, ADI, CM, and AER stat contracts
- route Primary, DOT, and Additional damage through one resolver/processor
- migrate serialized assets, stat UI, DOT behavior, and regression coverage
```
