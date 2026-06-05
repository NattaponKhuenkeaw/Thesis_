---
slug: "artifact-build-system"
status: draft
source: manual
gdd_tags:
  - "progression"
owner: "game-design-agent"
human_checkpoint: required
next_agent: game-design-agent
blocked_by: []
---

# Task Card: Complete Artifact Build System

## Player-Facing Goal

The player can view all owned artifacts in an `ArtifactGridPanel` inside their inventory/Soul Shop menu. They can drag & drop these artifacts into any of the 3 `ArtifactSlots` on their characters. The system handles "SMART" data operations like swapping artifacts between slots, removing artifacts, and adding new ones. 

Equipping an artifact immediately updates the character's stats, displaying both their base stats and equipped artifact bonuses visually on the character's `InspectorUI`.

When the player clicks on any artifact in the `ArtifactGridPanel`, an `ArtifactPreviewPanel` (hidden by default) opens up to show the artifact's icon, stats, passive, and lore. The player can close/cancel this interaction by clicking anywhere outside of the panel (cancel interact via raycast).

## Source

- Origin: Manual prompt request
- Requested by: User

## GDD Reference

- `@tag:progression` - Artifact System (Passive power-ups, Soul Shop loadout settings).

## Type

- [x] Feature
- [ ] Bug fix
- [ ] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- UI Panels (`ArtifactGridPanel`, `ArtifactPreviewPanel`)
- Character Inspector (`InspectScreenUI`)
- Team Loadout & Team Slots UI (`TeamSlotUI`, `ArtifactSlotUI`)
- Loadout Data Persistence (`TeamLoadoutProfile`)

### Files To Inspect First

- `Assets/Project/Scripts/TeamBuilder/UI/TeamBuilderUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/TeamSlotUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/ArtifactSlotUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/InspectScreenUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/TeamLoadoutProfile.cs`
- `Assets/Project/Scripts/Inventory/Items/Artifact.cs`

### Out of Scope

- Implementing the Soul Shop purchase algorithms (this only handles loadout organization).
- Creating or editing the artifact graphics/sprites directly.

## Acceptance Criteria

- [ ] `ArtifactGridPanel` correctly lists all owned artifacts from `CodexInventory` / `PlayerInventory`.
- [ ] Artifact drag-and-drop works seamlessly from the grid panel to character slots.
- [ ] Smart data behaviors are fully operational:
  - **Add**: Dragging an artifact to an empty slot equips it.
  - **Swap**: Dragging an artifact to a filled slot swaps it with the currently equipped one; dragging from one character's slot to another's swaps their equipment.
  - **Remove**: Dragging an artifact out of a slot or right-clicking removes it.
- [ ] `InspectorUI` dynamically updates and visually displays additional stats (e.g., `ATK 100 (+15)`) in boost/green color.
- [ ] `ArtifactPreviewPanel` opens on click showing the artifact's details (Icon, Stat, Passive, Lore).
- [ ] Clicking outside the preview panel correctly hides it.
- [ ] Follows `RULES_AND_POLICY.md`

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/implement-feature`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/artifact-build-system-design-spec.md`
