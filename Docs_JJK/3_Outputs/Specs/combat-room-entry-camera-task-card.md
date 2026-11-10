---
slug: combat-room-entry-camera
status: needs-human
source: manual
gdd_tags:
  - scene-roles
owner: agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Combat room entry camera

Workflow `/fix-bug`

Enter Combat01 through the normal room transition. Before the first hit, the Explore camera can sit high above the player. Combat framing or opening the gate makes the view recover.

- Keep the player visible as the room fades in with the first gate closed.
- Fit the Explore view to the active bounds before the entry snap and as the room opens up. Recover the authored zoom when the view fits again.
- Verify at standard and ultrawide aspects with `CombatChamberCameraTests`. Review entrance framing in the running game.
