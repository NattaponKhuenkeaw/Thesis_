---
slug: character-runtime-effect-teammate-random-buff
status: approved
source: manual
gdd_tags:
  - combat
  - characters
  - progression
owner: implementation-agent
human_checkpoint: approved
next_agent: qa-agent
blocked_by: []
---

# Task Card: Character Runtime Effect Teammate Policy & Random Buff Pool

## Player-Facing Goal

Enable character passives/runtime effects to target teammates specifically (all team members excluding self) and grant a randomly selected range of buffs ("between X and Y buffs") upon triggering conditions.

## Source

- Origin: Manual prompt
- Requested by: Apricha

## GDD Reference

- `@tag:combat` - Team synergy and rotation effects can affect active character, entire team, or specifically teammates excluding the caster.
- `@tag:characters` - Character Potential passives and runtime effects support conditional and probability-based buff application.
- `@tag:progression` - Ability to define dynamic buff pools and target scopes per kit node.

## Type

- [x] Feature

## Scope

### Systems Affected

- `PassiveEffectScope` enum & `RuntimeEffectApplyTarget` enum (`Teammates` target support)
- `PassiveRuntimeEffectDefinition` (random buff pool selection fields)
- `CharacterPassiveController` runtime effect application logic (random buff pool rolling & `Teammates` target resolution)
- `TeamLoader` always-active runtime effect binding (`Teammates` scope handling)
- `ProjectileRuntime` area runtime effect application (`Teammates` scope handling)
- `StatModifierPropertyDrawers` (Inspector GUI for random buff pool configuration)

### Files To Inspect First

- `Assets/Project/Scripts/Character/CharacterProgressionDefinition.cs`
- `Assets/Project/Scripts/Combat/RuntimeEffectDefinition.cs`
- `Assets/Project/Scripts/Combat/RuntimeEffectRecipe.cs`
- `Assets/Project/Scripts/Combat/CharacterPassiveController.cs`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`
- `Assets/Project/Scripts/Combat/ProjectileRuntime.cs`
- `Assets/Project/Scripts/Editor/StatModifierPropertyDrawers.cs`

### Out of Scope

- Modifying existing authored character assets unless requested.
- UI redesign of HUD icons.

## Acceptance Criteria

- [ ] `PassiveEffectScope` and `RuntimeEffectApplyTarget` support `Teammates` scope (targeting all team members except self).
- [ ] `PassiveRuntimeEffectDefinition` supports random buff selection mode with min/max buff count (between X and Y) and a configurable `randomBuffPool`.
- [ ] When triggered, `CharacterPassiveController` rolls between X and Y count and selects random buff recipes from `randomBuffPool` to apply.
- [ ] `Teammates` scope correctly filters targets so `sourceCharacter` is excluded from receiving the effect.
- [ ] Unity Editor drawers cleanly format the new random buff pool fields.

## Human Checkpoints

- [ ] Architecture & Design approval
- [ ] Verification on Unity editor build
