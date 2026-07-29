---
slug: authored-action-facing-event
status: implemented
source: manual
gdd_tags:
  - combat
  - tech-stack
owner: implementation-agent
human_checkpoint: completed
next_agent: qa-agent
blocked_by: []
---

# Authored Action Facing Event

## Workflow

`/implement-feature`

## Goal

Allow an action step to force the character to face left, right, or the opposite of its current facing at an authored logic frame without player input.

## Rugal Use Case

During `rugal.ultimate.ex`, after the first wall slam and re-grab:

1. Flip Rugal to the opposite facing direction.
2. Preserve the active grab session.
3. Let subsequent MotionData move toward the opposite wall.
4. Perform the second wall slam.

## Acceptance Criteria

- Facing changes once at the authored frame.
- The event does not depend on live directional input.
- Local-space MotionData, hitboxes, VFX offsets, and held-enemy positioning use the new facing afterward.
- The event does not change Ground/Air action context or interrupt the action.
- Existing actions without the event are unchanged.

## Implementation

- Added `SetFacingDirection` with `Opposite`, `Left`, and `Right` modes.
- Exposed the mode in the Animation Editor Action Event property card.
- Authored `Turn Opposite` on Rugal's `Grab FollowupEX` at frame 1.
- Rugal's opposite-direction MotionData begins at frame 2.
