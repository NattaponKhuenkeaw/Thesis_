# ADR-021 · Visit-scoped room rewards

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-11-05 |
| Scope | Room access and Hard route rewards |

## Context

The room's authored ZoneMode previously governed loadout access throughout its visit. Hard route rewards now need to hold that access and the exit until the final reward choice closes. The room-clear counter and the selected route each previously contributed Soul.

## Decision

- A room visit has a clearing phase and a monotonically advancing visit token. Layout and room presentation still use the existing room data. Clearing phase governs loadout access.
- A patterned route offer owns its total Soul and draw terms. Final encounter completion stages the draws, counts the clear, and pays the advertised Soul once. The room flow holds each set of candidates until the picker commits an exact copy, then advances after the picker closes.
- Artifact definitions and acquired copies retain their existing authorities. The picker reads prepared roll values and sends selection intent. The inventory is responsible for committing the chosen instance and deduplicating its ID.
- Pending drafts live for the room visit. A cross-session restart uses the existing boss-lobby checkpoint. There is no new room-resume format.

## Alternatives considered

| Approach | Trade-off |
| --- | --- |
| Let the reward UI own the draft and exit | Simpler initial wiring, with reroll and duplicate payout risk on view recreation. |
| Save each pending draw as a cross-session checkpoint | Supports mid-room resume and requires new persistence and route restoration semantics. |
| Keep reward ownership on the room visit | Supports view recreation and protects existing checkpoint behavior, with a clear scope for interruption tests. |

The provider retains its existing Unity asset binding during the migration. Boss, lobby, and training keep their distinct progression and presentation paths. [Architecture plan](../Specs/artifact-rarity-and-rewards-arch-plan.md)
