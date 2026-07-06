---
slug: "character-potential-panel"
status: ready-for-review
source: manual
gdd_tags:
  - "progression"
  - "ui"
owner: "codex"
human_checkpoint: satisfied-by-user-prompt
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Character Potential Panel

## Player-Facing Goal

Players can open a selected character's full-screen Potential panel from Character Info, inspect 12 track nodes, view fragment requirements, and permanently upgrade eligible nodes.

## Source

- Origin: manual prompt
- Requested by: Apricha

## GDD Reference

- `@tag:progression` - Character Potential is permanent and separate from run-scoped Character Level.
- `@tag:ui` - Character Potential Panel is a full-screen change with nodes, preview context, item needed, and ascend confirmation.

## Type

- [x] Feature
- [ ] Bug fix
- [ ] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- TeamBuilder Inspector UI
- Character Potential UI
- PlayerMetaProgression fragment spending

### Files To Inspect First

- `Assets/Project/Scripts/TeamBuilder/UI/InspectScreenUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/Panels/CharacterInfoPanel.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/Panels/CharacterPotentialPanel.cs`
- `Assets/Project/Scripts/Progression/PlayerMetaProgression.cs`

### Out of Scope

- New save-file/new-game profile selection
- Final art/layout pass for authored prefab node positions
- Changing fragment balance values

## Acceptance Criteria

- [x] Character Info can route the currently selected character into Character Potential.
- [x] Info/Potential page switches use a black fade transition.
- [x] Potential panel supports Header, draggable-content area, and Upgrade Window sections.
- [x] Potential content resolves 12 nodes from `CharacterObject.kitDefinition`.
- [x] Nodes show locked, fragment-blocked, upgradeable, and upgraded states.
- [x] Upgrade Window shows node name, rich description text, fragment count/cost, and confirm button.
- [x] Confirmed upgrades spend Character Potential fragments from `PlayerMetaProgression`.
- [x] Upgraded nodes hide requirement/confirm controls and show `Upgraded`.

## Human Checkpoints

- [x] Design/game-feel approval via user prompt
- [x] Architecture approval via existing MetaProgression authority
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/implement-feature`

Next artifact:

- `Docs_JJK/3_Outputs/DevLog/2026-07-05-character-potential-panel.md`
