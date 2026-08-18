---
slug: route-offer-always-three-random
status: implemented-pending-playtest
source: manual
gdd_tags:
  - scene-roles
  - core-loop
owner: agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: RouteOffer Always 3 Paths + More Random

## Player-Facing Goal

After every room clear (except the boss gate), the exit choice shows **3 paths**. Combat layouts can repeat; waves, difficulty, and rewards roll again so the three cards feel different.

## Source

- Origin: manual prompt (polish queue item 2)
- Requested by: team
- Playtest owner: อาย

## GDD Reference

- `@tag:scene-roles` — "generates a randomized Room and Reward for the player to choose from (3 Choices)"
- Linear = side-scroll corridor layouts, not a 2-fork map

## Type

- [x] Bug fix
- [x] Feature

## Root Cause

`Manager.prefab` serialized `offerCount: 2`. Combat rooms were also removed from the pool after a visit, so later choices shrank to 2 then 1.

## Fix

- Clamp / serialize offer count to 3
- Keep combat layouts in the pool every choice (unique only within the current 3 cards)
- Pad with a freshly rolled duplicate layout if MapSO cannot supply 3 unique rooms
- Reroll combat difficulty so the three cards are less likely to share Easy/Medium/Hard
- Boss remains a single offer

## Out of Scope

- Rogue-lite act rewrite
- Map prefab size polish
- Enemy BlazBlue warp AI

## Acceptance Criteria

- [ ] Normal clear → 3 cards
- [ ] Later rooms still show 3 (layouts may repeat, content should differ)
- [ ] Shop/Level/Rest still appear as at most one safe among the three when the act asks for them
- [ ] Boss gate is still one card
- [ ] Mid-run death / lobby reset still works

## Human Checkpoints

- [ ] อาย playtest: `Docs_JJK/3_Outputs/TestPlans/route-offer-always-three-random-regression.md`
