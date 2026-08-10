---
slug: damage-type-refactor
status: draft
source: manual
gdd_tags:
  - mechanics
owner: human-and-agent
human_checkpoint: required
next_agent: orchestrator-agent
blocked_by: []
---

# Task Card: Damage Type System Refactor (Physical / DOT / Particle)

## Player-Facing Goal

Refactor the damage system to use three core damage types:
1. **Physical**: Direct damage from player actions/projectiles. Uses existing damage formula (mitigated by DEF/RES, can crit, impact scaling on Ult). Can be infused with elements (Lightning / Bleed / Darkness) to apply DOT.
2. **DOT**: Damage over time ticks (Lightning / Bleed / Darkness). Bypasses DEF, mitigated by RES, scales with Elemental Mastery. Each elemental hit stacks DOT ticks up to a stack limit. Default cannot crit, but supports critical hits when enabled by passive/artifact effects.
3. **Particle**: Additional/particle/area damage from artifacts/summons. Ignores DEF and RES. Scales with Elemental Mastery when infused (`totalParticleDmg * (1 + EM/100)`). Can crit based on ownership.

## Source

- Origin: Manual user request
- Requested by: User

## GDD Reference

- `@tag:combat-damage` - Damage types, formulas, DOT stacking, and particle scaling.

## Type

- [x] Refactor
- [x] Feature

## Scope

### Systems Affected

- `CombatFormula.cs`
- `HitReactionData.cs`
- `ElementType.cs`
- `ElementDefinition.cs`
- `EnemyDamageTypeState.cs`
- `EnemyElementalProfile.cs`
- `HitBoxDamage.cs`
- `EnemyController.cs`
- `PlayerCombat.cs`
- `AdditionalDamageOnHitRuntimeEffectDefinition.cs`
- `EnemyWorldUI.cs` / `WorldTextUtility.cs`
- `CombatDamageTypeTests.cs`

### Files To Inspect First

- `Assets/Project/Scripts/Combat/CombatFormula.cs`
- `Assets/Project/Scripts/Character/HitReactionData.cs`
- `Assets/Project/Scripts/Combat/Elements/EnemyDamageTypeState.cs`
- `Assets/Project/Scripts/Combat/HitBoxDamage.cs`

### Out of Scope

- Full artifact passive system implementation (tagged `[##wait for Artifact passive Update]`).

## Acceptance Criteria

- [ ] `DamageRoutine` enum refactored to `Physical`, `Dot`, `Particle`.
- [ ] `ElementType` updated/expanded with `Lightning`, `Bleed`, `Darkness`.
- [ ] Physical damage formula handles direct hits, defense mitigation, resistance, crits, ult impact, and element infusion.
- [ ] DOT system supports stacking per element up to `maxStacks` (each elemental hit adds a stack), with tick damage calculation and optional crit support.
- [ ] Particle damage ignores DEF and RES, scales as `totalParticleDmg * (1 + EM / 100)` when infused, and supports crits.
- [ ] All unit tests pass in `CombatDamageTypeTests.cs`.

## Human Checkpoints

- [x] Design/game-feel approval
- [x] Architecture approval

## Router Decision

Recommended workflow: `/refactor`

Next artifact: `implementation_plan.md`
