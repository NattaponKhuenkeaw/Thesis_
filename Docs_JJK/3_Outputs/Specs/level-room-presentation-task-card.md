---
slug: level-room-presentation
status: implemented
source: manual
gdd_tags:
  - scene-roles
  - progression
  - ui
owner: implementation-agent
human_checkpoint: required
next_agent: qa-agent
blocked_by: []
---

# Task Card: Level Room Presentation

## Player-Facing Goal

Enter a calm Level safe room, approach the background display, confirm once, watch a 1-3 reveal, and receive a shared Run Global Level award that refreshes every character.

## Source

- Origin: Manual follow-up after `run-global-character-level`
- GDD: `@tag:scene-roles` Level Room workflow
- Domain APIs already on branch from `core_character`

## Scope

### In Scope

- `RoomKind.Level`
- Level-room interaction zone + selected/idle language
- Non-interruptible presentation sequence (hide UI, lock input, camera focus, cycle 1-3, apply once, restore)
- One-claim-per-visit default
- Editor factory for layout + RoomData
- Choice UI color + offer fill kind support

### Out of Scope

- Final authored art/SFX/camera timing numbers (human feel checkpoint)
- Dedicated Level scene (uses hybrid layout swap on `combat_1` shell)
- Removing admin `TriggerGlobalLevelUpEvent` prototype

## Acceptance Criteria

- [x] Confirm starts sequence only when idle and not already claimed this visit
- [x] Sequence locks input, hides gameplay canvases, focuses camera on display
- [x] Display cycles 1-3 then settles using `RunGlobalLevelReward`
- [x] Result applies once through `PlayerRunInventory.AddGlobalLevels`
- [x] Cap at Lv.30 communicates no growth instead of fake gain
- [x] Cleanup restores UI/input/camera on disable/destroy/scene leave
- [x] Menu `Project/Rooms/Create Level Room Layout` builds wiring assets
- [ ] Human PlayMode feel pass for timing/SFX/controller

## Verification

- EditMode: existing Run Global Level suite + Level reward clamp tests
- Manual: create layout via menu, enter Level room, claim once, re-approach shows claimed, exit unlocks as SafeZone
