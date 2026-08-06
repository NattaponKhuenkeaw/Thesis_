---
slug: "damage-type-resistance-simplification"
status: approved
source: manual
gdd_tags:
  - "combat"
  - "characters"
owner: "game-design-agent"
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Design Spec: Damage-Type Resistance Simplification

## Player Goal and Context

Players should be able to understand enemy mitigation from the damage type of
the attack and the enemy's listed reduction for that type. Damage should not
silently change because a low-HP enemy filled a hidden gauge and triggered a
separate reaction.

## System Rules

### Preserved

- Every damaging hit continues to author `ElementType.None`, `Memory`, `Echo`,
  `Ash`, or `Void`.
- `ElementType.None` is treated as Physical damage for enemy profile lookup.
- Elemental Mastery continues to scale non-Physical hit damage.
- Damage-number color and labels continue to use the hit's damage type.
- DEF and general Damage Reduction remain separate mitigation layers.

### Removed

- Character-level elemental affinity.
- Elemental gauges and gauge capacity.
- Elemental reactions and same-element reaction vulnerability.
- Gauge-owned DOT application.
- Enemy outgoing mastery against a player's character element.
- Gauge-build modifiers.

### New Enemy Rule

Each enemy profile authors one direct reduction for:

- Physical
- Memory
- Echo
- Ash
- Void

Recommended convention: inspector values are percentage points, so `25` means
25% less damage of that type. Reduction is applied after DEF and general Damage
Reduction, matching the existing incoming-damage pipeline order.

### Standalone DOT Rule

- Memory, Echo, Ash, and Void each apply a DOT debuff without requiring a gauge.
- `ElementDefinition` authors DOT duration, attack-damage ratio, and tick interval.
- Same-type DOT does not stack. A stronger application replaces the current DOT;
  an equal application refreshes its duration.
- DOT ticks retain their source-character attribution and cannot crit.
- DOT bypasses DEF, then passes through general Damage Reduction and the matching
  damage-type reduction.
- Physical damage does not apply DOT because `ElementType.None` has no
  `ElementDefinition`.

## Content Migration Requiring Approval

Kyo currently has two effects filtered to Ash-aligned allies and one upgrade
that increases Ash gauge build.

Approved migration:

- Change “Ash allies” effects to affect all allies, preserving their existing
  stat/damage values.
- Remove the gauge-build effect from the affected Kyo node and revise its text;
  do not silently replace it with a new damage bonus during this migration.

## Five-Component Evaluation

| Component | Result |
| --- | --- |
| Clarity | Stronger: one visible hit type maps to one authored enemy reduction, and DOT ticks are stable across enemy DEF values. |
| Motivation | Neutral: team-building loses character-affinity synergies. |
| Response | Neutral: player inputs and action timing do not change. |
| Satisfaction | Stronger: elemental DOT remains useful against armored enemies without bypassing dedicated damage reductions. |
| Fit | Stronger for the project's simple, readable action-combat direction. |

## Risks and Abuse Cases

- Reduction is limited to the approved `0–95%` range.
- Making former Ash-only buffs affect all allies increases their coverage.
- DEF-bypassing DOT becomes comparatively stronger against highly armored enemies;
  general and matching type reductions remain the balancing controls.
- Removing character-affinity UI may leave empty layout slots in multiple menus.
- Serialized character assets will retain unknown YAML fields until Unity
  reserializes them; runtime code must not depend on those stale fields.
- Existing skills whose descriptions promise reactions, DOT, gauge build, or
  element-specific allies will become misleading unless migrated.

## Playtest Scenarios

1. Hit one enemy with equal raw Physical, Memory, Echo, Ash, and Void damage and
   verify each result matches the corresponding profile value.
2. Repeat direct hits on 0 DEF and 500 DEF enemies to confirm profile reduction
   never reverses DEF behavior.
3. Apply every non-Physical type and verify configured DOT duration, damage
   ratio, tick interval, refresh behavior, and source attribution.
4. Compare the same DOT on 0 DEF and 500 DEF enemies with identical general and
   type reductions; tick damage must match.
4. Spam elemental hits for longer than 20 seconds and verify no gauge or
   reaction event appears.
5. Inspect character grid, team slots, shop, and Potential panels and verify no
   character-affinity icon/name remains.
6. Test Kyo with mixed teammates under the approved content migration.

## Tuning Priority

1. Verify calculation clarity and event count.
2. Verify Kyo's migrated team effects.
3. Tune individual enemy reduction values only after the pipeline is stable.
