---
slug: main-menu-save-shell
status: implementing
source: manual
gdd_tags: [scene-roles]
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: Main Menu Save Shell

## Player-Facing Goal

Boot into a title screen. Continue or New Game enters the Lobby. Settings change volume and fullscreen. The menu does not save a mid-run dungeon.

## Source

- Origin: manual prompt (Vertical Slice shell)
- Requested by: project owner

## GDD Reference

- Scene flow target: MainMenu → Lobby
- Meta progression persists; run inventory does not persist across sessions

## Type

- [x] Feature

## Scope

### Systems Affected

- Main menu scene and boot order
- PlayerMetaProgression load/clear
- PlayerRunInventory authored lobby reset
- AudioListener / SFX gain / fullscreen prefs

### Out of Scope

- Mid-run checkpoints
- Multiple save slots
- Pause menu
- Credits / gallery
- BGM playlist beyond an optional menu loop

## Acceptance Criteria

- [x] `MainMenu` is first in Build Settings
- [x] Continue is available only when a meta save exists
- [x] Continue loads meta, resets run inventory, enters `Test_Lobby`
- [x] New Game clears meta, resets run inventory, enters `Test_Lobby`
- [x] Settings persist master / BGM / SFX / fullscreen
- [ ] Human Play Mode: New Game → Potential → quit → Continue still has Potential
