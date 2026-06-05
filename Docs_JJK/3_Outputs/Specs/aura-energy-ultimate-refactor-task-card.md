---
slug: "aura-energy-ultimate-refactor"
status: approved
source: manual
gdd_tags:
  - "combat-fsm"
owner: "architect-agent"
human_checkpoint: required
next_agent: coder-agent
blocked_by: []
---

# Task Card: Refactor Ultimate System to Aura Energy

## Player-Facing Goal

The Ultimate skill is no longer governed by a standard time cooldown. Instead, it is gated by a resource called **Aura Energy (AE)**.

Each character on the team tracks their AE individually (default cap is 100 AE). AE is displayed on the UI as a circular fill meter (filling from bottom to top). When AE reaches 100, the Ultimate skill is ready, triggering visual ability-ready feedback (glow/animation). Using the Ultimate consumes all accumulated AE.

AE is gained through combat actions:

- **Hitting enemies**: Each hit has a 20% chance to grant 10 AE (default).
- **Killing enemies**: Defeating an enemy grants 10 AE (default).
- **Skill/Passive effects**: Special conditional skill casts or passive triggers can generate AE.

Team-wide progression upgrades can increase the team's maximum Aura Energy (even for benched characters not currently in runtime).

## Source

- Origin: Manual prompt request
- Requested by: User

## GDD Reference

- `@tag:combat` - Ultimate ability pacing, resource gates, combat UI indicators.

## Type

- [ ] Feature
- [ ] Bug fix
- [x] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Combat Core (`PlayerCombat`, `HitBoxDamage`, `EnemyController` events)
- FSM State Machine (`PlayerUltimateState` conditions)
- Combat UI (`AbilityUIManager`, `TeamSwitchHUD`, or similar active HUD panels)
- Character Stats (`CharacterStats`, `CombatRuntimeState`, or Team-wide modifier calculators)

### Files To Inspect First

- `Assets/Project/Scripts/Combat/PlayerCombat.cs`
- `Assets/Project/Scripts/Combat/HitBoxDamage.cs`
- `Assets/Project/Scripts/Player/States/PlayerUltimateState.cs`
- `Assets/Project/Scripts/Player/UI/AbilityUIManager.cs`
- `Assets/Project/Scripts/AIBehaviour/EnemyController.cs`
- `Assets/Project/Scripts/Character/CharacterStats.cs`

### Out of Scope

- Adding new animations or visual FX assets (refactoring logic only using existing components/materials).
- Changing basic attack or normal skill cooldowns (only the Ultimate system changes).

## Acceptance Criteria

- [ ] Remove cooldown-based check for starting Ultimate actions in `PlayerUltimateState` and `PlayerCombat`.
- [ ] Introduce a non-shared `AuraEnergy` field on each character loadout/stats tracker, capped at 100 by default.
- [ ] Introduce a global variable that can be modifyied by team-wide passive modifiers/upgrades, affecting characters in backup status.
- [ ] Introduce Aura Energy Multiplier (AEM) stat in `CharacterStats` which Increase amount of AE gained.
- [ ] CDR stat in `CharacterStats` is will reduce the Ultimate AE Cost (0.50 per 1% CDR, max 50% AE Cost Reduction).
- [ ] Hitting an enemy with any attack has a default 20% chance to grant 10 AE.
- [ ] Killing an enemy grants 10 AE.
- [ ] Max Aura Energy can be modified by team-wide passive modifiers/upgrades, affecting characters in backup status.
- [ ] UI Ability Meter displays Ultimate status as a circular bottom-to-top fill of AE.
- [ ] Trigger an ability-ready visual and/or audio feedback once AE reaches maximum capacity.
- [ ] Executing the Ultimate successfully consumes all accumulated AE.
- [ ] Follows `RULES_AND_POLICY.md`

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/refactor`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/aura-energy-ultimate-refactor-design-spec.md`
