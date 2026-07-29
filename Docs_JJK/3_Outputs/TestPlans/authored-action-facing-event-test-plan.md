---
slug: authored-action-facing-event
status: ready
source: manual
gdd_tags:
  - combat
owner: qa-agent
human_checkpoint: required
next_agent: human
blocked_by:
  - in-editor-playtest
---

# Authored Action Facing Event Test Plan

## Automated

- `Opposite` resolves right to left.
- `Opposite` resolves left to right.
- Absolute `Left` and `Right` modes resolve deterministically.

## Animation Editor

1. Open Rugal's `Grab FollowupEX`.
2. Confirm its Events card contains `Turn Opposite`.
3. Confirm Event Type is `Set Facing Direction`, Mode is `Opposite`, and Trigger Frame is `1`.
4. Change the mode and confirm Undo restores the previous value.

## PlayMode

1. Enter Omega Mode and execute Rugal's Ultimate EX from both starting facings.
2. Confirm the first wall slam transitions into `Grab FollowupEX`.
3. Confirm Rugal and the held enemy turn before frame-2 displacement.
4. Confirm the second dash travels toward the opposite wall.
5. Confirm the second wall slam connects and Omega Mode cleanup still occurs.

## Regression

- Run a normal non-Omega ultimate and confirm no forced turn occurs.
- Confirm existing overlay, teleport, impulse, and after-image action events remain unchanged.
