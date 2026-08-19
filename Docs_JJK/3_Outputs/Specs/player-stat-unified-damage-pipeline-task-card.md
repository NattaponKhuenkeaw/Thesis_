---
slug: "player-stat-unified-damage-pipeline"
status: ready-for-human-e2e
source: manual
gdd_tags:
  - "combat"
  - "characters"
  - "items"
owner: "implementation-agent"
human_checkpoint: required
next_agent: human-reviewer
blocked_by: []
---

# Task Card: Player Stat and Unified Damage Pipeline

## Goal

Replace the player build vocabulary with ATK, HP, DEF, CM, CR, CD, DM, DI,
ADI, and AER while keeping SPD system-only, then route player-originated
Primary, DOT, and Additional damage through one standard resolution contract.

## Type

- [x] Feature
- [x] Behavior-changing refactor

## Acceptance

- Ten buildable stats are shared by authoring, runtime, and player-facing UI.
- Primary, DOT, and Additional damage follow the approved DM/DI/ADI matrix.
- Melee, projectile, background, runtime-effect, and passive Additional damage
  use the same resolver.
- Same-element DOT uses strongest-replace/equal-refresh behavior.
- Serialized assets migrate without missing references or unit ambiguity.
- Artifact random-roll generation and enemy-to-player pipeline migration remain
  out of scope.

## Approval

The user approved the complete implementation plan on 2026-08-17. This
approval satisfies the design and architecture checkpoints for this task.
