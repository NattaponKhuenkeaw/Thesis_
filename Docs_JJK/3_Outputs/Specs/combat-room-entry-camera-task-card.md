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

Follow-up reports the camera pointing above the player in Biome 2 combat rooms and a visible right-to-left air run after returning from Shop, Rest, or Level. The earlier captured X also matches the midpoint of Combat08's closed first chamber.

Boss112 follow-up requires the encounter trigger to stay unarmed during room loading. A new layout can overlap the previous room's player pose before entrance placement, prematurely spawning the boss and sealing its arena gate outside the player.

- Keep the player visible as the room fades in with the first gate closed.
- Preserve the authored Explore lens and Combat group range through entry and room opening. Confine undersized rectangular rooms independently on each axis instead of lowering those values.
- Keep CamLookAhead as the manager's player camera reference through character swaps. Bind the movement body only as the proxy's position source.
- Move the player's physics body and snap the camera to that same body under black before starting the entrance walk.
- Verify at standard and ultrawide aspects with `CombatChamberCameraTests`. Review entrance framing in the running game.
