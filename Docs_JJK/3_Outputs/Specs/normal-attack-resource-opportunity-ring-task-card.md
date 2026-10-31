---
slug: normal-attack-resource-opportunity-ring
status: approved
source: manual
gdd_tags: [ui, combat]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Normal Attack resource opportunity ring

Workflow: `/fix-bug`. Reported by Apricha in chat on 2026-10-31.

## Player-facing issue

When Main has cached a Dimension Cut through Dash, the action can be offered on
the Normal Attack route, but the NA icon has no opportunity ring.

## Expected behavior

- Show the shared NA ring for a selected, available resource-backed standalone
  Normal action, including Main's cached Dimension Cut.
- Let existing resolver checks decide availability, including resource cost,
  action context, target and authored conditions.
- Clear the ring when the action is consumed or no longer selected/available.
- Preserve existing timed-window rings and unrelated action/UI behavior.

## Root cause

`AbilityUIManager.GetFollowUpWindowState` only checked timed tag modifiers.
Main's cached Dimension Cut is selected through its resource counter and has no
timed tag window, so it remained visually unmarked while resolvable from Normal
Attack.

## Verification

Updated the existing manual test plan. Unity compilation and runtime verification
were not run during this fix.
