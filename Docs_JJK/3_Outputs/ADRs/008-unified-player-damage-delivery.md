---
slug: "unified-player-damage-delivery"
status: approved
source: manual
gdd_tags:
  - "combat"
  - "characters"
owner: "architect-agent"
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# ADR-008: Orthogonal Damage Delivery and Hit-Owned Type

## Context

`DamageRoutine.Physical` currently means Primary delivery even when a hit is
non-Physical, while DOT and Particle branches repeat calculation and target
application. This makes DM/DI/ADI eligibility depend on concrete callers.

## Decision

- Represent delivery as Primary, Dot, or Additional independently of `ElementType`.
- Submit every player-originated damage event through a shared request/resolver.
- Preserve ADR-006: the hit owns its element and enemies own matching reductions.
- Use DI for Primary, ADI for Additional, and DM for every non-Physical event.
- Make secondary events terminal except that non-Physical Additional damage may apply DOT.
- Use strongest-replace/equal-refresh same-element DOT without stacking.

## Consequences

- Formula and mitigation behavior become independent of melee/projectile/terrain producers.
- Existing Impact content broadens to Primary DI and requires balance review.
- Serialized stat and runtime-effect targets require an explicit migration.
- Future Terrain emitters can use the request API without another damage formula.

## Related

- ADR-006: Hit-Owned Damage Types and Enemy Reductions
- `player-stat-unified-damage-pipeline-arch-plan.md`

