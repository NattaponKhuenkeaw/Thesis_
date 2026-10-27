---
slug: ui-prefab-merge-relink
status: approved
source: manual
gdd_tags:
  - ui
owner: implementation-agent
human_checkpoint: not-required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Restore UI Prefab Sources After Merge

## Player-Facing Goal

Room selection and save selection continue to show their authored UI after the merge, with all prefab references resolving in Unity.

## Source

- Origin: manual prompt
- Link: N/A
- Requested by: User

## GDD Reference

- `@tag:ui` - General UI context. The GDD does not specify the save-selection or room-choice prefab structure; this fix restores existing assets and does not change their intended behavior or design.

## Type

- [ ] Feature
- [x] Bug fix
- [ ] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Resources-backed RoomChoiceUI loading
- SaveSelectPanel loading and the MainMenu serialized reference

### Files To Inspect First

- `Assets/Project/Resources/UI/RoomChoiceUI.prefab`
- `Assets/Project/Resources/UI/SaveSelectPanel.prefab`
- `Assets/Project/Scenes/MainMenu.unity`
- `Assets/Project/Scripts/Editor/UI/RuntimeUiPrefabLinkEditor.cs`

### Out of Scope

- UI redesign or changes to room-choice/save-selection behavior.
- Runtime code, build settings, or dependency changes.

## Acceptance Criteria

- [x] Restore the authored `Prefab/UI` source prefabs with their original `.meta` GUIDs.
- [x] Keep the Resources variants linked to those sources and keep the MainMenu reference resolving.
- [x] Make no unrelated UI or gameplay changes.
- [x] Follow `RULES_AND_POLICY.md`.

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- Implementation diff on `fix/ui-prefab-merge-relink`
