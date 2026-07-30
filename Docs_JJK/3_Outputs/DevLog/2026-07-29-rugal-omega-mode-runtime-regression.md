---
slug: rugal-omega-mode-runtime-regression
status: draft
gdd_tags:
  - buff-debuff
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Rugal Omega Mode Runtime Regression

## Bugs Found

- Rugal's Omega entry and exit conditions no longer matched the action events that mutate their counters.
- Tag-driven stat refreshes did not reconcile Always runtime-effect bindings.

## Changes

- Restored Omega entry to Skill/Ultimate action start and removal to EX action end.
- Matched the base Skill counter by its stable authored action name.
- Reconciled Always runtime effects when a character runtime tag reports a stat change.
- Made action-window tag consumption report a stat change so an EX action removes its tag-gated Omega buff immediately.
