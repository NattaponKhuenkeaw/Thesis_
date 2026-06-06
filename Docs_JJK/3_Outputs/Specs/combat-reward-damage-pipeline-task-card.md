---
slug: "combat-reward-damage-pipeline"
status: done
source: manual
gdd_tags:
  - "combat"
  - "progression"
owner: "implementation-agent"
human_checkpoint: required
next_agent: "human"
blocked_by: []
---

# Task Card: Combat Reward and Damage Pipeline

## Player-Facing Goal

Aura Energy is earned only by attacks that damage an enemy. Every confirmed hit
independently rolls the character's fixed 10 AE chance reward, while an action
may also grant its own guaranteed `AE Reward` once per successful hitbox.

Damage supports mutually exclusive Normal and Elemental primary routines plus
separate, non-critical Additional Damage ticks from passives or artifacts.
Action damage is authored as a total budget with adjustable per-hit ratios.
Equipped artifact stats affect the deployed character in combat.

## Type

- [x] Bug fix
- [x] Feature
- [x] Refactor

## Systems Affected

- ActionResolver combat actions and hitbox authoring
- Player melee, projectile, and background-Ultimate execution
- Aura Energy generation and Ultimate capacity
- Passive and artifact on-hit effects
- TeamLoader runtime character stats
- Enemy damage feedback

## Acceptance Criteria

- [x] No AE is granted when an action starts or misses.
- [x] Every damaging hit independently rolls the fixed PlayerCombat AE reward.
- [x] `CombatAction` exposes one guaranteed `AE Reward` field (default `0`) and
      has no separate on-hit amount or on-kill branch.
- [x] AE capacity equals the resolved Ultimate AE cost exactly.
- [x] Normal damage keeps the existing damage-mastery formula.
- [x] Elemental damage replaces Normal damage and uses Elemental Multiplier
      instead of Damage Mastery.
- [x] Additional Damage is a separate popup/tick and cannot crit.
- [x] CombatAction supports a total action damage multiplier and hitboxes use
      clamped ratios when that budget is authored.
- [x] Genocide Cutter deals 300% ATK total across three equal hit ratios.
- [x] TeamLoader applies artifact bonuses to the matching runtime team slot.
- [x] Melee, projectile, and background-Ultimate paths share the same rules.
- [x] Training Debug records the final damage payload sent to the enemy rather
      than HP loss capped by the target's remaining health.

## Human Checkpoint

The direct user request on 2026-06-06 approved these behavior and architecture
constraints for implementation. PR merge remains a separate human checkpoint.

## Router Decision

Workflow: `/fix-bug`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/combat-reward-damage-pipeline-arch-plan.md`
