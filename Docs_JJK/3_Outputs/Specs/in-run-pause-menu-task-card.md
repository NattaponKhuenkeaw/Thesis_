---
slug: in-run-pause-menu
status: implementing
source: manual
gdd_tags: [scene-roles]
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: In-Run Pause Menu

## Player-Facing Goal

During a run (or lobby), Escape / Start pauses the game. Resume continues. Settings match the title screen. Return to title abandons the run, keeps meta, and does not save the dungeon.

## Source

- Origin: manual prompt after main-menu save shell
- Requested by: project owner

## GDD Reference

- `Docs_JJK/1_Inputs_Templates/GDD_Neo-JJK.md`: Pause menu with settings
- สำเพ็ง GDD: run progress resets; Potential / Fragments persist. No mid-run save spec.

## Type

- [x] Feature

## Scope

### Systems Affected

- Pause overlay prefab (`Resources/UI/PauseMenu`)
- GameFreezeManager pause hold vs hitstop
- Return-to-title teardown of run managers
- Team builder Tab while paused

### Out of Scope

- Mid-run checkpoints
- Quit-to-desktop on pause
- Control remapping

## Acceptance Criteria

- [x] Pause Canvas is an authored prefab, not built in `Awake`
- [ ] Escape / Start toggles pause in Lobby and in-run scenes, not MainMenu
- [ ] Settings persist master / BGM / SFX / fullscreen
- [ ] Return to title keeps meta, resets run inventory, loads MainMenu
- [ ] Hitstop / Team UI timeScale does not unpause the overlay
