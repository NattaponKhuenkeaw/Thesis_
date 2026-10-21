---
slug: player-hud-serialized-health-references
status: draft
source: manual
gdd_tags:
  - ui
owner: implementation-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Restore Player HUD Data and Status Bindings

## Player-Facing Goal

Keep player identity, HP, and timed status icons connected after HUD hierarchy changes.

## Source

- Origin: Manual prompt
- Requested by: User

## GDD Reference

- `@tag:ui` - The active HUD displays current HP.

## Type

- [x] Bug fix

## Scope

### Systems Affected

- Player HUD character, health, and runtime status display

### Files To Inspect First

- `Assets/Project/Scripts/UI/PlayerHUD.cs`
- `Assets/Project/Prefab/CombatUI/Player_UI.prefab`

### Out of Scope

- Changes to health bar behavior, timing, or appearance.

## Acceptance Criteria

- [x] Player HP, delayed damage sliders, HUD root, and text labels are assigned through serialized prefab references.
- [x] Runtime hierarchy/name lookup remains available as a fallback.
- [x] The HUD resolves its live `PlayerCharacter` through the existing `TeamLoader` or `PlayerCharacter` singleton.
- [ ] Existing HUD health and status icon behavior is confirmed in Unity.

## Human Checkpoints

- [ ] PR review before merge

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- Implementation diff
