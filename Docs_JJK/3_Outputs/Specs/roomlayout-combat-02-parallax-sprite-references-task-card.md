---
slug: roomlayout-combat-02-parallax-sprite-references
status: approved
source: manual
gdd_tags:
  - prototype-truth
owner: implementation-agent
human_checkpoint: not-required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Repair Combat 02 Parallax Sprite References

## Player-Facing Goal

Combat 02 displays its authored parallax background and ground layers when mounted from its room layout prefab.

## Source

- Origin: manual prompt
- Link: Current Codex task
- Requested by: project owner

## GDD Reference

- `@tag:prototype-truth` - Implemented prototype state; no parallax-specific behavior is documented.

## Type

- [x] Bug fix

## Scope

### Systems Affected

- Combat 02 room layout sprite references

### Files To Inspect First

- `Assets/Project/Resources/RoomLayouts/RoomLayout_Combat_02.prefab`
- `Assets/Project/Art/BG/B1/*.meta`

### Out of Scope

- Parallax architecture or movement behavior
- Other room layouts

## Acceptance Criteria

- [x] Every B1 SpriteRenderer reference in Combat 02 uses an imported sprite file ID.
- [x] The authored far, mid, background, and ground artwork is selected by the prefab references.
- [x] No unrelated room layout data is changed.

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- Implementation diff in `Assets/Project/Resources/RoomLayouts/RoomLayout_Combat_02.prefab`
