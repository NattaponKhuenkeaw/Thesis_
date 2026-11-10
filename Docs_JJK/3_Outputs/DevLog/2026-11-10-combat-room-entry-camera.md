---
slug: combat-room-entry-camera
status: needs-human
gdd_tags:
  - scene-roles
owner: agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Combat room entry camera

## Bugs found

The room transition installs the first closed-gate confiner before revealing the Explore camera. Without the runtime zoom fit, the authored lens can exceed the confiner and Cinemachine falls back to a collapsed camera area that can frame above the player. Combat uses a different virtual camera, and clearing the chamber widens the confiner.

Restored the Explore lens fit before the entry snap and during bounds changes. It checks both dimensions of the active box, invalidates Cinemachine's lens cache when the size changes, and returns to the authored size as space allows.

Unity C# compilation and the scoped diff check passed. The Combat01 prefab geometry check confirmed that the authored viewport exceeds the closed chamber width at 21:9 and the fitted viewport stays inside it. The lobby-to-room playtest is pending.
