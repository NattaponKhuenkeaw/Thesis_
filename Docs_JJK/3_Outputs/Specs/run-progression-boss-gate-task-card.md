---
slug: run-progression-boss-gate
status: implemented-pending-playtest
source: manual
gdd_tags:
  - rooms
  - bosses
owner: human+agent
human_checkpoint: playtest-feel
next_agent: human
blocked_by: []
---

# Task Card: Run Progression (N Combat → Boss)

## Player-Facing Goal

After clearing a set number of Combat/Elite rooms, the exit choice offers only the boss room.

## Source

- Origin: manual prompt (Run Progression MVP)
- Decisions: Choice UI with boss-only offer (1A); depth counts Combat/Elite clears only (2A)

## Scope

### Systems Affected

- RunProgressionManager (new)
- RoomChoiceDirector offer generation
- RunEndController reset path
- RoomExitPresenter ensure path

### Out Of Scope

- Auto-warp without Choice UI
- Full act map / branching biomes
- TransitionTrigger.useDynamicProgression (not needed with Choice UI)

## Acceptance Criteria

- [ ] After `roomsBeforeBoss` Combat/Elite clears, Choice UI shows only `Boss_01`
- [ ] Shop/Reward clears do not deepen the run
- [ ] Clearing Boss resets depth for the next segment
- [ ] Run end / lobby reset clears depth
- [ ] Existing multi-offer Combat/Elite/Reward/Shop flow still works before boss is due

## Setup Notes

See `Docs_JJK/3_Outputs/Specs/run-progression-mvp-setup.md`.
