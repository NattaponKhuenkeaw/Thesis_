---
slug: save-select-biome-action-panel
status: needs-human
source: manual
gdd_tags:
  - progression
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Save Select Biome Action Panel

## Player-Facing Goal

When a player opens a save slot's action panel, it shows that save's current biome. The save window remains fully opaque while the action panel is open.

## Source

- Origin: Direct user request
- Link: Current Codex task
- Requested by: Project owner

## GDD Reference

- `@tag:progression` - MainMenu is the title screen and includes save-slot selection.

## Type

- [x] Feature
- [x] Bug fix

## Scope

### Systems Affected

- Save selection UI and its shared save actions panel

### Files To Inspect First

- `Assets/Project/Scripts/UI/SaveSelectPanel.cs`
- `Assets/Project/Prefab/UI/SaveSelectPanel.prefab`
- `Assets/Project/Scripts/Save/SaveSlotStore.cs`

### Out of Scope

- Save data format or progression behavior
- Save card layout and currency display

## Acceptance Criteria

- [x] The action panel displays the selected save's 1-based biome number.
- [x] Opening the action panel keeps the Window CanvasGroup alpha at 1.
- [x] Opening the action panel still prevents clicks on the underlying save window.

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [x] Review before merge
- [ ] Team status publish approval

## Router Decision

Workflow: `/fix-bug`

Root cause: the controller overwrote the action panel's Biome value with currency and set the Window alpha to `.45` whenever the panel opened.
