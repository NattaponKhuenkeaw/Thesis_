---
slug: player-action-startup-interruption
status: draft
source: manual
gdd_tags:
  - combat-player-interrupt-armor
owner: implementer
human_checkpoint: gameplay review
next_agent: qa-agent
blocked_by: []
---

# Player action startup interruption

Extend the existing hitstun protection to action entry and startup. A hit during a committed frame-data action can still reduce HP and defeat the character. Authored action timing and input cancel rules still govern when the player can chain or leave the action.

This supersedes the startup damage window in the earlier player action armor task card.

## Acceptance

- Take a nonlethal hit during wind-up and on the first frame of an action with zero startup. HP decreases and the action continues.
- Take a hit while idle to verify ordinary hitstun. Take a lethal hit during an action to verify defeat.
- Repeat on the ground and in the air. Verify the move's hitbox and cancel window still appear at their authored frames.
