---
slug: menu-action-pause
status: approved
source: manual
gdd_tags: [architecture, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: qa-agent
blocked_by: []
---

# Menu pause during a player action

The player can become stuck after rapidly opening and closing Team Builder or Character Inspector during an attack, dash, or animation. The report includes actions that make no enemy contact.

- Team Builder saved and restored the global time scale independently of combat. In a safe zone it also redeployed unchanged loadouts, which reset transient combat and movement state.
- Inspector inferred whether it was open from its opacity. Reopening during a close fade could leave the close callback active and let it dismiss the reopened panel.
- The shared menu hold suspends action timing, including delayed and active slow motion and timed and scoped freezes. UI fades use real time so the player can close the panel while paused.

Verify rapid panel toggles during an attack that misses, a grounded and airborne dash, and an authored slow-motion or freeze action. On final close, the action should continue from its paused state and return movement and attack control. Repeat with both panels in succession and close an unchanged loadout in a safe zone. Full-scene verification is pending.
