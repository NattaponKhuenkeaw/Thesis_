# ADR-006: Hit-Owned Damage Types and Enemy Reductions

| Field | Value |
| --- | --- |
| Status | **Accepted** |
| Date | 2026-07-30 |
| Author | architect-agent |
| GDD Section | `@tag:combat`, `@tag:characters` |

## Context

The current enemy elemental component combines static resistance, outgoing
mastery, per-element gauges, DOTs, reactions, and temporary modifiers. Character
affinity also drives UI and ally-target filters. This coupling produced damage
events that were difficult to predict and conflicts with the requested simpler
damage-type model.

## Decision

- Damage type remains authored by hits through the existing `ElementType` enum.
- `ElementType.None` represents Physical damage until the later enum migration.
- `CharacterObject` no longer owns an `ElementType`.
- Enemy profiles author direct incoming reductions for Physical, Memory, Echo,
  Ash, and Void.
- `EnemyDamageTypeState` applies the matching profile reduction after
  DEF/general Damage Reduction and owns standalone active DOT debuffs.
- `ElementDefinition` configures DOT duration, attack ratio, and tick interval;
  every non-Physical element applies DOT.
- DOT bypasses DEF but remains subject to general Damage Reduction and its
  matching damage-type reduction.
- Elemental gauges, reactions, outgoing affinity mastery, and gauge-build
  modifiers are removed.
- Damage-type visuals, hit filters, and Elemental Mastery scaling remain.

## Alternatives Considered

| Option | Pros | Cons |
| --- | --- | --- |
| Keep `EnemyElementalState` name but strip reactions | Smallest code diff | Leaves a misleading stateful name and prolongs coupling |
| Move profile fields directly into `EnemyController` | Fewer components | Mixes enemy health/AI authority with damage-type tuning |
| Use `EnemyDamageTypeState` with the existing script GUID | Clear ownership and safe prefab migration | Requires coordinated rename and call-site updates |

## Consequences

### Positive

- Damage results become deterministic from hit type and enemy profile.
- Low enemy Max HP can no longer trigger unrelated burst damage.
- Enemy mitigation is visible as direct authored values.
- The later `ElementType` → `DamageType` migration remains isolated.

### Negative / Trade-offs

- Character-affinity synergies require removal or redesign.
- Existing reaction-specific character text/content must be migrated.
- The change intentionally alters combat behavior and requires rebalancing later.
- Memory and Echo now gain configurable DOT behavior.

### Migration

- Preserve the existing `EnemyElementalState.cs.meta` GUID while renaming the
  component so `EnemyCore.prefab` does not receive a Missing Script reference.
- Convert the one current enemy profile asset to direct values.
- Remove top-level character affinity display and target-filter code.
- Delete reaction/gauge scripts only after architecture approval.

## Related

- `Docs_JJK/3_Outputs/Specs/damage-type-resistance-simplification-task-card.md`
- `Docs_JJK/3_Outputs/Specs/damage-type-resistance-simplification-impact-analysis.md`
