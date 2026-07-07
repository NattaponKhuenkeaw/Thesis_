---
slug: runtime-effect-character-progression-refactor
status: approved
source: manual
gdd_tags:
  - combat
  - characters
  - progression
owner: codex
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Runtime Effect / Character Progression Refactor

## Player-Facing Goal

Character kits can reuse a small set of runtime-effect formulas while each kit/potential node controls its own values, targets, duration, stack policy, and trigger conditions.

## Source

- Origin: Manual prompt
- Requested by: Apricha

## GDD Reference

- `@tag:combat` - Team rotation should apply buffs, debuffs, heals, shields, and setup effects.
- `@tag:characters` - Character Potential modifies passive loops, skill utilities, and team modifiers.
- `@tag:progression` - Passive descriptions only execute when represented by implemented stat bonuses or authored runtime effects.

## Type

- [x] Refactor

## Scope

### Systems Affected

- Runtime effect formula definitions
- Character kit / potential runtime-effect applications
- `PlayerRunInventory` active run effects
- Enemy/local `RuntimeEffectReceiver`
- Projectile area runtime-effect applications
- Runtime effect HUD/debug display metadata

### Files To Inspect First

- `Assets/Project/Scripts/Combat/RuntimeEffectDefinition.cs`
- `Assets/Project/Scripts/Combat/RuntimeEffectContainerDefinition.cs`
- `Assets/Project/Scripts/Character/CharacterProgressionDefinition.cs`
- `Assets/Project/Scripts/Combat/CharacterPassiveController.cs`
- `Assets/Project/Scripts/Shop/PlayerRunInventory.cs`
- `Assets/Project/Scripts/Combat/RuntimeEffectReceiver.cs`
- `Assets/Project/Scripts/Combat/ProjectileRuntime.cs`

### Out of Scope

- Full manual rebalance of every character asset.
- End-to-end Unity playtest; the human owner will run E2E validation.
- PR creation or official team status publication.

## Acceptance Criteria

- [ ] Runtime-effect formulas can be reused by multiple kit recipes with different values and policies.
- [ ] Duration, target scope, stack policy, refresh policy, display metadata, and remove-on-damaged policy can be owned by the recipe/application layer.
- [ ] Existing authored assets continue to run through legacy fallback fields until migrated.
- [ ] Runtime effect containers can act as one visible authored concept while internally running multiple formula payloads.
- [ ] Follows `RULES_AND_POLICY.md`.

## Human Checkpoints

- [x] Architecture approval
- [ ] E2E game validation
- [ ] PR review before merge

## Router Decision

Recommended workflow: `/refactor`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/runtime-effect-character-progression-refactor-impact-analysis.md`
