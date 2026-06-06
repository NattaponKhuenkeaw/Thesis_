---
slug: "combat-reward-damage-pipeline"
type: arch-plan
status: approved
owner: "architect-agent"
gdd_tags:
  - "combat"
  - "progression"
next_agent: "implementation-agent"
---

# Architecture Plan: Combat Reward and Damage Pipeline

## Authority Decisions

1. `HitBoxDamage` is the confirmation point for primary damage, Additional
   Damage, defeat state, passive hit notification, and AE reward dispatch.
2. `CombatAction` owns `actionDamageMultiplier`, `auraEnergyGainAmount`
   (on-hit), and `auraEnergyOnKillAmount`.
3. `actionDamageMultiplier <= 0` preserves legacy direct per-hit multipliers.
   An authored positive value enables the new formula:
   `ATK * actionDamageMultiplier * Clamp01(hitbox.damageRatio)`.
4. `DamageType.Physical` uses the Normal routine. Other damage types use the
   Elemental routine. Additional Damage is explicitly marked as Additional.
5. Normal primary damage uses Damage Mastery. Elemental primary damage uses
   Elemental Multiplier instead of Damage Mastery. Both may crit and apply
   Skill Mastery for Skills and Ultimates.
6. Additional Damage uses its effect's ATK ratio, target mitigation, and
   Elemental Multiplier for non-Physical damage. It cannot crit and does not
   use Damage Mastery, Skill Mastery, or primary-action passive scaling.
7. `TeamLoader` owns cached effective stats per team slot. It clones base
   character stats, adds numeric artifact bonuses, and passes the result to
   `PlayerCharacter`.
8. Aura capacity remains exactly the highest resolved Ultimate cost. Artifact
   CDR can change that resolved cost, but there is no separate maximum-AE stat.

## Runtime Flow

```text
ActionResolver selects CombatAction
  -> PlayerCombat snapshots action damage and AE reward context
  -> melee/projectile/background executor configures HitBoxDamage
  -> primary damage resolves against one enemy
  -> eligible Additional Damage effects resolve as separate enemy hits
  -> final enemy alive/dead state is checked
  -> defeated: guaranteed on-kill AE only
  -> alive: on-hit AE chance only
```

## Compatibility

- Existing serialized `damageMultiplier` values migrate into `damageRatio`
  through `FormerlySerializedAs`.
- Actions without an authored positive action budget retain their previous
  direct per-hit damage behavior.
- New or migrated actions use clamped `0..1` per-hit ratios.

## Artifact Runtime Flow

```text
TeamBuilderUI.DeployLoadout(team, slotIndex, artifacts)
  -> TeamLoader clones each CharacterStats block
  -> numeric bonuses from that slot's artifacts are added
  -> PlayerCharacter.SetCharacter receives effective stats and artifacts
  -> combat, movement, HP, CDR, AE generation, and background Ultimates read
     the effective runtime stats
```

## Migration Target

`Genocide Cutter`:

- Action damage multiplier: `3.0`
- Hit 1 ratio: `0.33333334`
- Hit 2 ratio: `0.33333334`
- Hit 3 ratio: `0.33333334`

The resulting total remains `300% ATK`.
