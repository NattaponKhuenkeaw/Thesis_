---
slug: "damage-type-resistance-simplification"
status: ready-for-human-e2e
source: manual
gdd_tags:
  - "combat"
  - "characters"
owner: "orchestrator-agent"
human_checkpoint: required
next_agent: qa-agent
blocked_by: []
---

# Task Card: Damage-Type Resistance Simplification

## Player-Facing Goal

Damage should be predictable from the hit's authored damage type and the enemy's
explicit reduction for that type. Characters no longer have an innate element,
and enemies no longer build elemental gauges or trigger elemental reactions.

## Source

- Origin: Direct user prompt on 2026-07-30
- Requested change:
  - Remove the Elemental Gauge and Elemental Reaction systems.
  - Remove `ElementType` from `CharacterObject`.
  - Preserve hit-authored `ElementType` for now.
  - Replace enemy Weak/Normal/Strong elemental profiles with direct reduction
    values for Physical, Memory, Echo, Ash, and Void damage.
  - Preserve DOT as a standalone debuff configured by `ElementDefinition`;
    every non-Physical element applies DOT.
  - DOT bypasses DEF while retaining general and matching damage-type reduction.

## GDD Reference

- `@tag:combat` — current damage resolution and elemental reaction rules.
- `@tag:characters` — current character element and Elemental Mastery rules.
- The requested behavior intentionally supersedes the current Elemental Reaction
  and Character Element sections and therefore requires a GDD update.

## Type

- [x] Feature / behavior removal
- [x] Refactor / migration

This is not a behavior-preserving refactor: it removes reactions, gauges, DOT
creation from the gauge system, and character-affinity targeting.

## Scope

### Systems Affected

- Enemy incoming-damage resolution
- Enemy damage-type profile authoring
- Elemental gauge and reaction runtime
- Standalone configurable elemental DOT runtime
- Character data and character-affinity UI
- Character-targeted runtime-effect filters
- Kyo Potential/passive content that depends on Ash character affinity or gauge
- GDD combat and character documentation

### Files To Inspect First

- `Assets/Project/Scripts/Combat/Elements/EnemyElementalState.cs`
- `Assets/Project/Scripts/Combat/Elements/EnemyElementalProfile.cs`
- `Assets/Project/Scripts/AIBehaviour/EnemyController.cs`
- `Assets/Project/Scripts/Combat/HitBoxDamage.cs`
- `Assets/Project/Scripts/Character/CharacterObject.cs`
- `Assets/Project/Scripts/Character/CharacterProgressionDefinition.cs`
- `Assets/Project/Scripts/Combat/CharacterPassiveController.cs`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`
- `Assets/Project/Scripts/UI/Helpers/CharacterElementRoleUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/`

### Out of Scope

- Renaming `ElementType` to `DamageType`.
- Removing hitbox/projectile/additional-damage `ElementType`.
- Reworking damage-number colors or hit damage-type icons.
- Removing Elemental Mastery damage scaling.
- Changing DEF or general Damage Reduction formulas.
- Rebalancing character action multipliers.

## Acceptance Criteria

- [x] Enemies never build elemental gauges or trigger reactions.
- [x] Memory, Echo, Ash, and Void hits can apply their independently configured DOT.
- [x] `CharacterObject` has no character-level `elementType`.
- [x] Character/shop/team UI no longer reads or displays character affinity.
- [x] Hitbox, projectile, runtime-effect, and damage-popup `ElementType` remain functional.
- [x] Enemy profiles expose direct Physical, Memory, Echo, Ash, and Void reduction values.
- [x] Incoming damage applies exactly one matching profile reduction.
- [x] Direct-hit DEF and general Damage Reduction behavior remain unchanged.
- [x] DOT bypasses DEF while retaining general and matching damage-type reduction.
- [x] No runtime code references gauge/reaction classes.
- [x] Removed serialized fields leave no Missing Script components or broken inspectors.
- [x] Former character-element filters and Kyo gauge content follow the approved migration.
- [x] Edit-mode tests cover all five damage types and reduction boundaries.
- [x] GDD no longer documents character affinity, gauges, reactions, or gauge-owned DOT.
- [ ] Unity compiles with no new errors or warnings.

## Human Checkpoints

- [x] Design/game-feel approval required
- [x] Architecture approval required
- [x] Destructive file-removal approval required
- [x] PR review before merge

## Router Decision

Workflow: combined behavior-change migration using `/implement-feature` design
gates and `/refactor` impact/regression controls.

Current artifact:

- `Docs_JJK/3_Outputs/Specs/damage-type-resistance-simplification-task-card.md`

Next artifacts:

- `Docs_JJK/3_Outputs/Specs/damage-type-resistance-simplification-design-spec.md`
- `Docs_JJK/3_Outputs/Specs/damage-type-resistance-simplification-impact-analysis.md`
- `Docs_JJK/3_Outputs/ADRs/006-hit-owned-damage-types-and-enemy-reductions.md`
