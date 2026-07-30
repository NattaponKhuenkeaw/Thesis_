---
slug: runtime-effect-stack-overflow
status: approved
source: manual
gdd_tags:
  - buff-debuff
owner: implementation-agent
human_checkpoint: not-required
next_agent: code-review-agent
blocked_by: []
---

# Runtime Effect Stack Overflow

## Workflow

`/fix-bug`

## Problem

Repeated applications of Rugal's Increase ATK recipe can display more than its five-stack limit. Reapplication captures the already-buffed source stats, causing the merge check to create another runtime instance. The HUD aggregates those matching identities into a count above five.

## Expected Behavior

Matching stack identity, source, and duration use one runtime instance. Reapplication updates the source snapshot, increments the instance stack count only up to the authored limit, and refreshes duration when enabled.

## Acceptance Criteria

- A source-stat change does not create a second instance for the same stack identity.
- The instance stack count respects the recipe's stack limit.
- Reapplication refreshes duration according to the recipe policy.
- Different targets, sources, recipe identities, or durations remain separate.

## Scope

- Runtime effect merge behavior in active and off-field receivers.
- Edit-mode regression coverage.
