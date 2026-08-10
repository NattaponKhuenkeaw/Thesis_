---
slug: "edge-snap-dash-sequencing"
status: approved
source: manual
gdd_tags:
  - "combat-scope"
  - "platform-input"
owner: "game-design-agent"
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Design Spec: Edge Snap and Dash Sequencing

## Player Goal and Context

The player can commit to an attack while crossing a ledge or immediately after dropping through a platform and can trust that the character remains airborne. Dash communicates one committed sequence: Startup and Active perform the burst, Recovery remains locked, and the authored Cancel phase returns control. A dash input on touchdown takes priority over landing recovery.

## System Rules

- Genuine ledge loss and authored drop-through override a ground-started `MatchInput` output to Air at completion or an accepted cancel.
- Explicit `Ground` output remains authoritative and may perform its authored ground correction.
- Positive-Y MotionData does not count as ledge loss.
- Dash Recovery accepts Attack, Skill, Ultimate, and Jump but keeps walking/facing locked.
- Ground Dash returns walking/facing/locomotion animation at Cancel; Cancel retains the full action window. Air Dash keeps its existing airborne movement policy.
- While contact reports Ground, a short downward support ray at `GroundC.x - 1` must still find ground before that contact may keep a previously grounded player in Ground context.
- Landing accepts Dash immediately and clears its remaining landing lock before Dash begins.

## State Machine Checklist

- Entry: Edge case begins from a ground-owned combat action; Landing Dash begins from physical grounded Landing.
- Exit: Ground departure resolves Air at a legal action boundary; Landing exits directly to Dash.
- Interruptibility: Dash Recovery accepts Attack, Skill, Ultimate, and Jump only; Cancel uses the full authored cancel policy.
- Chained actions: Edge/drop actions route to the matching Air action root or Falling; Landing routes to Dash.
- Resource cost: Existing dash cooldown and air-dash count rules remain unchanged.
- Edge cases: authored lift, explicit Ground output, one-way drop-through, lingering edge contact, rapid buffered input, Air Dash Recovery.

## 5-Component Evaluation

- Clarity: Physical ledge/drop behavior matches the visible character position.
- Response: Landing Dash responds immediately; dash cancel timing follows the visible authored boundary.
- Satisfaction/Fit: No distant snap or animation theft undermines combat weight.
- Motivation: Unchanged; this is a movement/combat reliability fix.

## Risks and Abuse Cases

- A broad physical-air check could incorrectly convert authored lift; transition cause must remain authoritative.
- Clearing Landing must not bypass dash cooldown or air-dash limits.
- Explicit Ground actions must retain their authored correction.

## Playtest Scenarios

- New-player: attack while walking off a ledge, then observe a natural airborne finish.
- Stress: repeat drop-through plus Attack and Landing plus Dash on adjacent input frames.
- Skill: cancel Dash at the first authored Cancel frame and verify earlier inputs stay buffered/locked.
- Abuse: attempt Landing Dash while dash cooldown is active.
- Readability: confirm the action/dash animation is not replaced before its legal boundary.

## Tuning Priority

No timing values change. If feel remains wrong, inspect authored Dash cancel-frame placement before changing code or numbers.
