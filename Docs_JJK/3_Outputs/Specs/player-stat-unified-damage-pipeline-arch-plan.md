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

# Architecture Plan: Player Stat and Unified Damage Pipeline

## Data Flow

```text
Damage producer -> DamageRequest -> CombatDamageResolver
    -> DamageResult -> CombatDamageProcessor -> Enemy resolved hit
    -> permitted DOT / hit notification / AE / presentation
```

## Public Contracts

- `CombatStatId` and `CombatStatCatalog` define stable stat identity and display metadata.
- `DamageDelivery` separates Primary, Dot, and Additional delivery from `ElementType`.
- `DamageDescriptor` owns delivery, element, and critical policy.
- `DamageRequest` carries source, target, amount, and action attribution.
- `DamageResult` carries the resolved amount and critical/type attribution.
- `CombatDamageResolver` owns pure math; `CombatDamageProcessor` owns application.

## Authority

- Hitboxes own element and geometry, not formulas.
- Damage delivery controls eligible build stats, mitigation, reaction, and proc policy.
- Enemy type reduction remains owned by `EnemyDamageTypeState`, extending ADR-006.
- The outgoing player pipeline is in scope; enemy attacks remain separate.

