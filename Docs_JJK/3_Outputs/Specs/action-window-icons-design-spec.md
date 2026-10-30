---
slug: action-window-icons
status: approved
source: manual
gdd_tags: [ui, combat, platform-input]
owner: Codex
human_checkpoint: required
next_agent: architect-agent
blocked_by: []
---

# Action-window icon design

## Player experience

Each existing NA / Skill / Ult slot shows the authored action offered on that input.
When a temporary action becomes available, its picture replaces the ordinary picture
for that action's window. The existing window overlay drains on the gameplay timer.
When consumed or expired, the ordinary picture returns. Dash retains its current icon
and movement cooldown presentation.

## Proposed mapping for approval

Use input route/category to choose the HUD slot. `CombatAction.actionType` currently
controls interrupt policy and Ultimate hit flash only, per the approved
`action-type-input-decoupling-task-card.md`. For example, Main's Ultimate-type
Dimension Slash appears on Skill because that is its input and availability window.
Moving that icon to Ult would advertise a different input from the one that executes it.

An optional action Sprite is authored alongside Action Name in the existing editor.
The resolver's matching action and priority determine the picture; timed availability
does not require a parallel UI countdown or a second action-selection policy.
When no replacement is available, restore the matching ordinary action's authored
picture, falling back to the prefab picture when no art is assigned.
Unavailable ordinary actions retain a dimmed picture and existing cooldown/AE cues.

## Interaction flow

Player opens a follow-up opportunity through existing combat
  -> Gameplay creates its existing availability window
  -> The matching input slot displays that action's picture and remaining-window cue
  -> Player commits the action, or the window expires
  -> The ordinary slot picture and existing readiness display return.

## Feedback and timing

Reuse existing cooldown fill, countdown, dimming, ready glint/pulse and rejection
wiggle. A window icon switch adds no new sound, particles, camera motion or haptics;
the gameplay action retains its existing audio/visual feedback. Rebinding, swapping
and reopening the HUD establish the current state silently. No timing values change.

NA should gain the same window-duration cue when a timed NA replacement is available.
Dash has no resolver category today; preserve its authored prefab icon rather than
introducing an unrelated dash action catalog in this task.

## Edge cases and readability

- Rapid rejection must return to the same slot position (separate maintenance fix).
- Window expiry/consumption: picture and overlay update together with no stale icon.
- Competing windows: show the resolver's highest-priority matching action.
- Ground/Air or target/counter changes: show the eligible replacement for that input.
- Cooldown/AE gate: a temporarily unusable replacement must not look ready.
- Missing picture: original prefab art remains available; never retain the previous
  character's replacement picture.
- Character swap, hidden HUD and re-enable: reconstruct current display silently.
- Merely previewing the HUD must not consume counters, AE, cooldowns or windows.

## Experience evaluation and playtest

Clarity: the picture names what the input offers; existing readiness cues show gates.
Response: selection reads gameplay state without altering commitment or buffering.
Satisfaction and fit: retain the game's established HUD feedback and combat audio.
Motivation: make temporary opportunities visible during combat rotation.
This is presentation only; player state transitions remain unchanged.

New player/readability: identify which input launches a displayed follow-up.
Stress: spam rejected dash and alternate actions through window boundaries.
Skill: open, execute and intentionally let a follow-up expire.
Abuse: repeatedly open/close HUD and swap characters; ensure no resources are consumed
and no gameplay window is refreshed by presentation.
Pass: displayed picture and remaining-window cue match the gameplay opportunity in
each scenario, and slots return to their resting positions after feedback.

## GDD alignment and checkpoint

Extends @tag:ui's cooldown/readiness/follow-up visibility on the primary PC input
model in @tag:platform-input. Keeps @tag:combat context and resource rules intact.
Apricha approved this design in chat on 2026-10-30: input-route mapping, temporary
icon replacement, prefab fallback, NA window cue and retaining the existing Dash icon.
