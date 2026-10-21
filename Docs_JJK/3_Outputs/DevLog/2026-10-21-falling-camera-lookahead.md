# Falling Camera Look-Ahead

## Bugs Found

- The falling proxy used its 0.25-second return smoothing while moving down to the predicted fall offset, delaying the camera lead at fall start.
- The combat camera used the player transform in its target group, so it did not follow the look-ahead proxy.

## Changes

- Added a dedicated 0.08-second fall look-ahead smooth time to the camera proxy and camera prefab. The existing 0.25-second return smoothing remains in use outside falls.
- Routed the combat target group's player member through the same proxy used by the explore camera, preserving enemy group targets and framing weights.

## Validation

- Unity playtest not run; downward camera feel, combat framing and room-boundary behavior need a human playtest.
