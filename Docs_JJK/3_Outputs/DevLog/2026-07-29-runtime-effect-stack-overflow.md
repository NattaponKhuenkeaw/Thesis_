---
slug: runtime-effect-stack-overflow
status: draft
gdd_tags:
  - buff-debuff
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Runtime Effect Stack Overflow

## Bugs Found

- Timed stack merging compared the full source-stat snapshot. Self-buffs changed that snapshot after each application, producing multiple capped instances with the same display stack key.

## Changes

- Matching stacks now reuse one runtime instance even when source stats change.
- The reused instance receives the latest source snapshot before its count and duration are updated.
- Added edit-mode regression coverage for the changed-source-stat case.
