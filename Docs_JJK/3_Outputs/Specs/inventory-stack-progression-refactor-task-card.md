---
slug: inventory-stack-progression-refactor
status: approved
source: manual
gdd_tags:
  - progression
  - ui
owner: codex
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Inventory Stack And Progression Refactor

## Player-Facing Goal

Players can buy, preview, cancel, and consume stacked EXP Potions cleanly, while duplicate Characters and Artifacts convert into upgrade fragments instead of creating duplicate usable units.

## Source

- Origin: manual prompt
- Requested by: Apricha
- Summary: Refactor the current inventory ScriptableObject shape so consumables stack, duplicate Characters and Artifacts become fragments for Potential or Transcendence, and run-scoped state is separated from permanent progression state.
- Approval note: Duplicate Character and Artifact fragments are saved through MetaProgression as unused permanent upgrade materials until spent by the Potential or Transcendence systems.

## GDD Reference

- `@tag:progression` - Character Level is run-scoped and removed on run loss, while Character Potential and Artifact Transcendence are permanent.
- `@tag:progression` - Duplicate Characters and Artifacts should convert into fragment materials for permanent upgrades.
- `@tag:ui` - Character Level Panel previews EXP Potion consumption before confirming the level-up.

## Type

- [ ] Feature
- [ ] Bug fix
- [x] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Inventory ownership data
- Shop purchase and sell flow
- Character Level / EXP Potion UI
- Character Potential duplicate material flow
- Artifact Transcendence duplicate material flow
- Run reset and permanent progression boundaries

### Files To Inspect First

- `Assets/Project/Scripts/Shop/PlayerShopInventory.cs`
- `Assets/Project/Scripts/Shop/ShopManager.cs`
- `Assets/Project/Scripts/Shop/UI/ShopUI.cs`
- `Assets/Project/Scripts/Shop/UI/ShopPreviewPanel.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/Panels/CharacterLevelPanel.cs`
- `Assets/Project/Scripts/Inventory/Items/BaseItem.cs`
- `Assets/Project/Scripts/Inventory/Items/Item.cs`
- `Assets/Project/Scripts/Inventory/Items/CharacterItem.cs`
- `Assets/Project/Scripts/Inventory/Items/Artifact.cs`
- `Assets/Project/Scripts/Character/CharacterObject.cs`

### Out of Scope

- Final Potential and Transcendence balance values.
- Rebuilding the whole Backpack UI.
- Full Artifact Transcendence stat restoration snapshots.

## Acceptance Criteria

- [ ] Stackable consumables are stored by item identity plus quantity, not by repeated list entries.
- [ ] `CharacterLevelPanel` can preview multiple consumptions from the same potion stack and remove the confirmed quantity atomically.
- [ ] First-time Character and Artifact acquisition grants the actual run-owned unit/equipment.
- [x] Duplicate Character acquisition converts into that Character's Potential fragment currency.
- [x] Duplicate Artifact acquisition converts into that Artifact's Transcendence fragment currency.
- [x] Duplicate fragments persist in MetaProgression as unused upgrade material until consumed by an upgrade system.
- [x] Run-scoped inventory state and permanent progression state have separate owners.
- [x] Character Level and EXP are stored as run-scoped state instead of being mutated by the Level Potion UI on `CharacterObject`.
- [x] Team display and combat runtime stat consumers read Character Level from run state.
- [x] Existing character grid, artifact grid, shop preview, and sell checks continue to work during migration.
- [x] Canonical run inventory saves and loads current-run stacks, team slots, equipped artifacts, currency, and run character level state.
- [ ] Follows `Docs_JJK/0_User_Manual/RULES_AND_POLICY.md`.

## Human Checkpoints

- [ ] Architecture approval before implementation.
- [ ] PR review before merge.

## Router Decision

Recommended workflow: `/refactor`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/inventory-stack-progression-refactor-impact-analysis.md`
