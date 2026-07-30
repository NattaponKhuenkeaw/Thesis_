---
slug: projectile-owner-damage-context
status: superseded
source: manual
gdd_tags:
  - combat
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Projectile Owner Damage Context

> Superseded on 2026-07-29 by
> `deployed-character-live-stats-task-card.md`. A switched-out source that
> remains deployed now resolves live off-field stats; snapshot fallback is
> reserved for a source removed from the deployed team.

## Workflow

`/fix-bug`

## Bug

A projectile correctly retains its source character for Training Debug and
reward attribution, but after a team switch its delayed hit refreshes combat
stats and passive modifiers from the currently active character.

The bug occurs because projectile hit processing calls the shared
`PlayerCombat` and `CharacterPassiveController` instances after those
components have been rebound to the switched-in character.

## Intended Behavior

- A projectile belongs to the character that spawned it.
- While that owner remains active, delayed hits may refresh from the owner's
  current effective stats.
- After switching characters, the projectile retains its captured owner
  damage context.
- It must not inherit stats, action modifiers, or passive modifiers from the
  switched-in character.
- Training Debug, runtime-effect source context, and reward attribution remain
  assigned to the projectile owner.

## Acceptance Criteria

- Switching from a low-ATK owner to a high-ATK character does not increase the
  existing projectile's damage.
- Switching from a high-ATK owner to a low-ATK character does not reduce it.
- A projectile may still refresh live damage context while its owner remains
  the active character.
- Melee and newly spawned projectiles are unchanged.

## Human Checkpoint

The direct user report establishes projectile-owner damage as the approved
behavior. Merge and combat-feel verification remain human checkpoints.
