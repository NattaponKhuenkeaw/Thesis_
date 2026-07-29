---
slug: deployed-character-live-stats
status: implemented
source: manual
gdd_tags:
  - combat
  - characters
  - progression
owner: architect-agent
human_checkpoint: approved
next_agent: human
blocked_by: []
---

# Deployed Character Live Stats — Impact Analysis

## Responsibility Change

| Class | Before | After |
| --- | --- | --- |
| `PlayerRunInventory` | Persistent run state plus ad-hoc effective-stat rebuilding. | Persistent run state, effects, equipment, and snapshot fallback. |
| `TeamLoader` | Team deployment plus a private stats dictionary. | Authoritative registry of runtime-only deployed-character records. |
| `PlayerCharacter` | Active-character view of stats. | Active projection of its deployed runtime record. |
| Damage contexts | Character plus mutable/shared component references. | Stable source identity, live deployed lookup, and snapshot fallback. |
| Runtime effects | Duration implicitly influences live source lookup. | Explicit `SnapshotOnApply` or `LiveOnResolve` policy. |

## Blast Radius

- Projectile primary damage.
- Additional Damage from runtime effects.
- Stat-ratio buff/debuff, heal, and shield formulas.
- Potential and runtime-effect stat invalidation.
- Team switching and off-field sources.

## Performance

Damage-time resolution reads an existing deployed cache. Stat graphs rebuild
only on existing team, Potential, equipment, or stat-affecting runtime-effect
events. Cross-character cycles retain snapshot fallback.

## Validation

One E2E Unity test covers spawn, switch, off-field stat mutation, hit, and
snapshot-on-application behavior. No unit tests are added.
