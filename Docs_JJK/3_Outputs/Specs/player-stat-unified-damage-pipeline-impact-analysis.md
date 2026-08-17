---
slug: "player-stat-unified-damage-pipeline"
status: approved
source: manual
gdd_tags:
  - "combat"
  - "characters"
  - "items"
owner: "architect-agent"
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Impact Analysis: Player Stat and Unified Damage Pipeline

## Responsibility Changes

| Current owner | Target owner |
| --- | --- |
| `CharacterStats` plus duplicated UI switches | `CharacterStats` plus `CombatStatCatalog` |
| `HitBoxDamage` damage, mitigation, DOT, and Additional logic | Collision/request construction only |
| `CombatFormula` routine-specific mixed calculation | `CombatDamageResolver` pure resolution |
| `EnemyController` repeated mitigation | Resolved-hit application only |
| `EnemyDamageTypeState` DOT strength calculation | DOT lifetime/refresh state and request emission |
| Individual Additional producers | `CombatDamageProcessor` request application |

## Blast Radius

- Character, Artifact, Potential, and Runtime Effect serialized stat fields.
- Melee, projectile, grab, background switch, dash-through, and Additional damage.
- Damage text, Training Debug, Character Inspect, Artifact Preview, and stat formatting.
- Existing enum ordinals and prefab TMP references require migration protection.

## Incremental Migration

1. Add catalog and compatibility-safe field renames.
2. Add resolver contracts and tests without removing legacy callers.
3. Migrate Primary, DOT, then Additional producers.
4. Validate assets and remove compatibility branches.

