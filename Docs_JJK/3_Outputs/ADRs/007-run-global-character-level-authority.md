---
slug: run-global-character-level-authority
status: approved
source: manual
gdd_tags:
  - characters
  - progression
  - architecture
owner: architect-agent
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# ADR-007: Run-Global Character Level Authority

| Field | Value |
| --- | --- |
| Status | **Accepted** |
| Date | 2026-08-05 |
| Author | Codex, from project-owner direction |
| GDD Section | `@tag:characters`, `@tag:progression`, `@tag:architecture` |

## Context

The current run inventory stores level and EXP separately for each acquired character, and active leveling is coupled to EXP Potions and a Character Level panel. The project direction replaces this management loop with one casual, run-scoped level awarded by Level room events and shared by the complete roster.

ADR-005 already makes `TeamLoader` authoritative for effective stats of deployed characters, so the new persistent level authority must feed that cache rather than compete with it.

## Decision

`PlayerRunInventory` owns one serialized Run Global Level, its Lv.30 cap, save/load/reset behavior, and a previous/current level-changed event. `GetCharacterBaseStats(CharacterObject)` calculates character-authored ATK/DEF/Max HP growth at that shared level without requiring or creating `RunCharacterState` ownership.

`TeamLoader` remains the deployed effective-stat authority from ADR-005 and rebuilds all deployed records when the global-level event fires. Weighted Level-event selection lives in a stateless reusable domain utility. Individual run upgrades, EXP, Leveling Potions, and the Character Level panel remain present only for incremental serialized migration and are not active authorities.

## Alternatives Considered

| Option | Pros | Cons |
| --- | --- | --- |
| Mutate every `CharacterObject.upgrade.level` | Simple reads | Corrupts authored assets, mixes run state with content data, and still requires loading every character asset |
| Keep synchronized `RunCharacterState.upgrade.level` records | Reuses current panel code | Requires records for unowned characters and permits levels to diverge again |
| Store one level in `PlayerRunInventory` and calculate on read | One authority, works for unowned characters, backward-compatible migration | Requires consumers to use run-inventory stat resolvers |

The third option is accepted.

## Consequences

### Positive

- Every character shares one traceable run level.
- Unowned characters resolve correctly without becoming run-owned.
- The future Level room and current admin prototype share one weighted rule.
- ADR-005 runtime caching and off-field damage semantics remain intact.
- Legacy UI/code can be removed later without blocking the current migration.

### Negative / Trade-offs

- Legacy per-character upgrade fields remain serialized but inactive, so naming can be temporarily confusing.
- Code paths that bypass `PlayerRunInventory` still use authored fallback stats and must remain fallback-only.
- Legacy saves begin the new global system at Lv.1 instead of guessing how multiple character levels should merge.

### Migration

- Add the global field/event/mutation API and persistence.
- Switch base-stat resolution to the shared level.
- Bind `TeamLoader` to the shared-level event.
- Disable active Character Level panel navigation and label retained code legacy.
- Remove legacy fields/assets in a later serialization-safe cleanup after prefab and save migration approval.

## Related

- Extends ADR-005: Deployed Character Live-Stat Authority.
- GDD: `@tag:characters`, `@tag:ui`, `@tag:progression`, `@tag:scene-roles`, `@tag:architecture`.
- Task: `run-global-character-level-task-card.md`.
