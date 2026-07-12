---
slug: scene-transition-manager
status: approved
source: manual
gdd_tags:
  - scene-roles
  - combat
owner: game-design-agent
human_checkpoint: required
next_agent: architect-agent
blocked_by: []
---

# Design Spec: Room Boundary Transitions and Training Damage Metrics

## Player Experience

Entering a room feels like arriving from beyond the stage: the player runs in
from the left while the camera can temporarily include the entrance path. Once
the player reaches the stop point, the stage closes behind them and neither the
player, enemies, nor camera can drift off the left side.

The right side stays closed until progression says the room is clear. On clear,
the exit space opens and the camera eases briefly to the right as a readable
"you may leave now" cue. The player then walks into the already-authored exit
trigger to transition normally.

Training mode makes team contribution readable without changing combat. Every
active team member remains visible in a compact table, including members that
have dealt zero damage in the current sample.

## Interaction Flow

```text
SceneTransitionManager loads a room
  -> entrance boundary opens for PlayerIntroState
    -> player reaches EntrancePoint.StopPosition
      -> entrance boundary seals to the normal playable edge

Room-clear source or debug/shop button unlocks exit
  -> exit wall moves out and right confiner expands
    -> camera drifts briefly to the right
      -> player walks into TransitionTrigger

Player damages training target
  -> final accepted hit retains source character
    -> team row updates total, team share, and max hit
      -> 6.7 seconds without a hit clears the complete sample
```

## Feedback Loops

| Trigger | Visual | Audio | Timing |
| --- | --- | --- | --- |
| Intro begins | Camera can include the outside-left spawn path | Existing transition audio | Existing intro duration |
| Intro completes | Left stage boundary silently seals behind the player | None required | Same frame as Intro -> Idle |
| Exit unlocks | Right boundary opens and camera eases right | None in this module | Short cue, authored in scene settings |
| Training hit | Matching character row updates immediately | Existing hit audio | Same frame as accepted damage |
| Training timeout | All sample values return to zero together | None | 6.7 seconds after last hit |

## Juice Specification

- Camera: one short additive rightward cue; it must not change player facing or
  permanently alter normal horizontal look-ahead.
- Wall/confiner changes: no forced player movement and no teleport.
- UI: compact fixed-column table in the existing training debug overlay.
- Audio and haptics: unchanged for this module.

## Edge Cases

- A missing player or entrance still leaves the room sealed after transition cleanup.
- An intro timeout seals the entrance after the existing fallback returns to Idle.
- Repeated exit-unlock calls are idempotent and do not replay feedback unless requested.
- Directly starting a room scene initializes both sides locked to the authored playable bounds.
- A locked trigger rejects the player even if collider placement is bypassed.
- Switching target resets the full damage sample before recording the first hit on the new target.
- Team changes rebuild row order and remove stale characters without retaining their damage.
- Zero team damage displays `0%` for every member and never divides by zero.

## GDD Alignment

- `@tag:scene-roles`: room progression stays locked until room clear, then exposes the exit.
- `@tag:combat`: the table observes the shared accepted-damage path and does not alter combat formulas.
- The camera cue needs a Play Mode feel check for distance and duration before merge.

