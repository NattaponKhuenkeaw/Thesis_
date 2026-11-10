---
slug: route-reward-pool
status: needs-human
source: manual
gdd_tags:
  - progression
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Route Reward Pool

## Player-Facing Goal

Hard route rewards offer Big Soul, Soul with two Artifacts, Soul with one Artifact, or Soul with one Memory. Two-Artifact rewards are less frequent.

## Source

- Origin: Direct user request
- Link: Current Codex task
- Requested by: Project owner

## GDD Reference

- `@tag:progression` - Route rewards are part of the implemented room progression loop.

## Type

- [x] Feature / reward tuning

## Scope

### Systems Affected

- Authored Hard reward patterns in `RouteOfferTables`

### Files To Inspect First

- `Assets/Project/Resources/MapSO/RouteOfferTables.asset`
- `Assets/Project/Scripts/Core/ZoneState/RouteOfferGenerator.cs`

### Out of Scope

- Soul payout amounts
- Artifact or Memory reward resolution

## Acceptance Criteria

- [x] Two-Artifact reward weight is reduced from 25 to 10.
- [x] Artifact rewards do not also grant Memory.
- [x] The no-Artifact pattern grants one Memory.
- [x] Big Soul is a pure-Soul reward and pays 350% of biome base Soul.
- [x] Other Hard rewards pay 150% of biome base Soul.
- [x] The four pattern weights total 100 (10 / 30 / 30 / 30).

## Human Checkpoints

- [ ] Final reward balance / game-feel review
- [ ] Review before merge

## Router Decision

Workflow: `/implement-feature` (data-only reward-pool tuning)

Weights are 10% Soul + 2 Artifacts, 30% Soul + 1 Artifact, 30% Soul + 1 Memory, and 30% Big Soul.
