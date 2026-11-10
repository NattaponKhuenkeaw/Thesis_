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

## Biome 2 follow-up

The previous capture matches Combat08's closed confiner midpoint in both X and Y. Its first chamber is about 27 world units wide, while the Combat camera's minimum zoom spans about 29.5 units at 16:9. Restored the bounds-aware combat framing limit and fit the base lens too, so the first engagement cannot reintroduce the oversized view.

Room transitions previously wrote the player Transform and immediately snapped a camera proxy that reads the Rigidbody2D. The player uses interpolation and automatic physics transform synchronization is disabled. Both the normal entrance and the debug exit warp now move the motor body and sync the pose before explicitly binding and snapping the camera proxy. The normal transition gives Cinemachine a frame under black before starting the intro walk.

The lookup fallback now runs after scene Awake calls and prefers the movement component on a tagged child's parent. A Unity probe confirmed that the old Transform-only sequence leaves the body at its previous pose until physics synchronization.

Validation ran in a separate Unity project copy with batch mode. The 51-case run passed 49 cases, including all chamber-camera cases and actual Shop/Rest/Level scene loads back to Combat08 at Biome 2. Two new interpolated-entry assertions needed a small physics-contact tolerance adjustment. Both passed on rerun. The entry tests also verify that a stale camera body reference is replaced by the entering player's motor. Visual playtest in the user's active editor remains the human checkpoint.

## Boss112 premature arena seal

The Boss_02 layout's fight trigger can overlap the outgoing Shop player's position during the load coroutine's pre-placement frame. RoomWaveManager accepted that physics trigger while the transition was active, spawned the boss and replaced the open arena gate with a solid wall. Entrance placement then returned the player to the corridor outside the sealed arena. Automatic trigger entry now waits until the transition completes. TriggerStay still starts the encounter if the player finishes a transition inside a valid fight trigger.

Boss112EntranceTests now simulates an outgoing pose inside the fight trigger before entrance placement, verifies the gate stays open through loading, and verifies normal walking seals the gate behind the player.

All three Boss112EntranceTests passed in the isolated Unity batch run, including Main and CameraGirl walking into the arena after the simulated load overlap. Scoped diff validation passed. Full visual verification of the reported run remains pending.
