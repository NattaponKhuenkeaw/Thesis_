---
slug: authored-action-facing-event
status: approved
source: manual
gdd_tags:
  - combat
owner: game-design-agent
human_checkpoint: completed
next_agent: implementation-agent
blocked_by: []
---

# Authored Action Facing Event — Design Spec

## Player Experience

Rugal completes the first wall slam, secures the enemy again, turns decisively without requiring directional input, and carries the enemy toward the opposite wall for the second slam. The turn reads as part of the authored EX cinematic rather than player-controlled locomotion.

## Interaction Flow

```text
Omega Ultimate EX starts
→ Rugal grabs and dashes toward the first wall
→ First wall slam resolves
→ Re-grab succeeds
→ Authored Opposite-facing event fires
→ Following MotionData dashes toward the other wall
→ Second wall slam resolves
```

## System Rules

- The facing event executes once at its authored absolute logic frame.
- `Opposite` resolves from the facing direction at the moment the event fires.
- `Left` and `Right` provide deterministic absolute alternatives for future actions.
- Player directional input does not override the event during the locked action step.
- The event changes facing only; it does not teleport, change velocity, alter action context, or release the grabbed enemy.

## Feedback

Use the existing animation, held-enemy motion, dash VFX, audio, and wall-slam feedback. This feature introduces no new numeric tuning values. The authored trigger frame should coincide with the animation’s visible turn or the frame immediately before the opposite dash begins.

## Edge Cases

- If the action is interrupted before the event frame, no turn occurs.
- If no enemy is grabbed, the facing event still executes; the existing route decides whether the follow-up continues.
- Re-entering or replaying the step clears event-trigger tracking, so the event fires once for that execution.
- Facing changes must not alter the captured Ground/Air action context.

## GDD Alignment

- `@tag:combat`: actions remain governed by authored frame timelines.
- `@tag:tech-stack`: `PlayerCombat` and `ActionResolver` retain action/step resolution while movement remains owned by `PlayerMovement`.
- Supports deterministic, data-driven actions without adding player input complexity.

## Playtest Checkpoint

Confirm that the turn visually happens before opposite-direction displacement, the grabbed enemy remains stable, and hitbox/VFX orientation matches the new facing.
