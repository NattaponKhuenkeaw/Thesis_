---
adr_number: 006
title: Boss pattern controller instead of Behavior Graph for bosses
status: accepted
date: 2026-08-04
gdd_tags:
  - bosses
  - enemies
---

# ADR 006: Boss Pattern Controller

## Context

Trash enemies use Unity Behavior Graph (detect/chase/attack). Bosses need phase sequencing, telegraph → active → recovery, and scripted movement (parabola jump, charge) that Behavior Graph does not express cleanly yet.

## Decision

Boss prefabs use `BossPatternController` and disable `BehaviorGraphAgent` at runtime. They still reuse `EnemyController`, `EnemyAttackHitbox`, `EnemyWorldUI`, and `RoomWaveManager`.

## Alternatives

1. Extend Behavior Graph with custom boss nodes — more authoring cost for MVP.
2. Rewrite a separate boss health/damage stack — duplicates combat pipeline.

## Consequences

- Boss AI is code-driven and easier to tune for thesis MVP.
- Behavior Graph remains the path for regular enemies.
- Future bosses can share `BossPatternController` or specialize with new pattern timings.
