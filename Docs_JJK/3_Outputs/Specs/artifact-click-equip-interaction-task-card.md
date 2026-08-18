---
slug: "artifact-click-equip-interaction"
status: implementation-validation
source: manual
gdd_tags:
  - "ui"
  - "items"
owner: "implementation-agent"
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Artifact Click-to-Equip Interaction

## Player-Facing Goal

Select an Artifact destination slot first, then click an inventory Artifact to equip it. Inventory cards must not drag. Equipped Artifacts retain drag, swap, cancel/outside-drop removal, and right-click removal.

## Source

- Origin: Manual prompt on 2026-08-17
- Requested by: User

## GDD Reference

- `@tag:ui` - Character inventory already uses destination-first click assignment while equipped Team slots remain draggable.
- `@tag:items` - Artifact instance identity and draft equipment authority follow ADR-009.

## Type

- [x] Feature
- [ ] Bug fix
- [x] Refactor

## Scope

### Systems Affected

- Team Builder Artifact inventory-card input
- Team-slot Artifact destination selection
- Equipped Artifact slot drag/drop and removal regression behavior

### Files To Inspect First

- `Assets/Project/Scripts/TeamBuilder/UI/ArtifactGridItemUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/ArtifactSlotUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/TeamBuilderUI.cs`

### Out of Scope

- Artifact instance/save authority, roll generation, filters, Favourite persistence, or deployed `TeamLoader` behavior
- New input bindings, sounds, assets, or equipment rules

## Acceptance Criteria

- [ ] Left-clicking a writable Artifact slot selects and highlights that exact destination and opens the Artifact grid.
- [ ] Left-clicking an inventory Artifact equips it to the selected destination and closes the grid.
- [x] Artifact inventory cards implement no drag interfaces and create no drag ghost.
- [ ] A generic Artifact-grid click still opens preview when no destination is selected.
- [ ] Favourite Mode still turns inventory-card clicks into Favourite toggles.
- [ ] Closing/changing category/entering combat cancels the selected destination.
- [ ] Equipped Artifact slots retain slot-to-slot drag/swap, outside-drop removal, and right-click removal.
- [x] No Artifact instance, draft, save, or deployment authority changes.
- [x] Follows `RULES_AND_POLICY.md`.

## Human Checkpoints

- [x] Design/game-feel approved by the user's explicit destination-first request.
- [x] Architecture approved: reuse the existing Character selection pattern and ADR-009 authorities; no new architecture decision.
- [ ] PR review before merge.

## Router Decision

Recommended workflow: `/implement-feature` because the requested input behavior changes even though its implementation is a refactor.
