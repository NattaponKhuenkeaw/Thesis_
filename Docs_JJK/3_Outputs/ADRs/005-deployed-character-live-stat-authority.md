---
slug: deployed-character-live-stat-authority
status: approved
source: manual
gdd_tags:
  - combat
  - characters
  - progression
owner: architect-agent
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# ADR-005: Deployed Character Live-Stat Authority

| Field | Value |
| --- | --- |
| Status | **Accepted** |
| Date | 2026-07-29 |
| Author | Codex |
| GDD Section | `@tag:combat`, `@tag:characters`, `@tag:progression` |

## Context

Inventory and TeamLoader currently expose different effective-stat paths.
Delayed and off-field damage can therefore read the active character or omit a
deployed source's Potential modifiers.

## Decision

TeamLoader owns runtime-only records for every deployed character. A record
contains stable character identity, slot identity, cached effective stats,
combat runtime state, and a revision. PlayerRunInventory remains persistent run
data and snapshot storage.

Formula-bearing effects explicitly choose `SnapshotOnApply` or
`LiveOnResolve`. Damage and Additional Damage use live source records; ordinary
buff/debuff, heal, and shield application use snapshots. Missing/removed source
records fall back to their captured snapshot.

## Alternatives

| Option | Result |
| --- | --- |
| Store mutable stats in serialized team slots | Rejected: persistence and runtime authority become coupled. |
| Rebuild inventory stats on every hit | Rejected: duplicates TeamLoader rules and hot-path computation. |
| Use only active `PlayerCharacter.Stats` | Rejected: invalid for off-field ownership. |

## Consequences

- Active and off-field characters share one stat authority.
- Damage-time lookup is a cache read.
- Effect magnitude semantics are authored explicitly.
- Shared active-character MonoBehaviours cannot define off-field source truth.

## Related

- Extends ADR-002 and ADR-004.
- Task: `deployed-character-live-stats-task-card.md`.
