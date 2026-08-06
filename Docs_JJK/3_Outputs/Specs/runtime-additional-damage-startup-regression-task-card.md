---
slug: runtime-additional-damage-startup-regression
status: approved
source: manual
workflow: /fix-bug
owner: implementation-agent
next_agent: implementation-agent
human_checkpoint: not-required
blocked_by: []
gdd_tags:
  - combat
  - progression
---

# Runtime Additional Damage Startup Regression

## Goal

Ensure unlocked always-on runtime additional-damage passives are active on the first valid hit of every gameplay run.

## Reported Reproduction

1. Start gameplay with Orochi passive level 0 or Shermie passive level 2 unlocked.
2. Repeat from a fresh game start several times.
3. On some starts, deal a hit that should trigger the passive.
4. Observe that no runtime additional damage is produced.

## Expected Result

The unlocked passive runtime effect is reconciled after deployed-team, run-inventory, and progression state are ready, without creating duplicate runtime-effect instances.

## Scope

- Trace startup ordering for always-on runtime-effect recipes.
- Fix the smallest lifecycle/reconciliation defect that can leave the effect absent.
- Preserve source-character ownership and live-stat damage resolution.
- Add an end-to-end regression test for startup availability.

## Out of Scope

- Passive damage-value tuning.
- Re-authoring Orochi or Shermie progression data unless the assets are proven invalid.
- Broad runtime-effect architecture changes.

## Acceptance Criteria

- Orochi passive level 0 and Shermie passive level 2 runtime additional damage are available on the first valid hit after game start.
- Repeated reconciliation does not duplicate an always-on effect.
- Switching characters does not transfer effect ownership or artifact stats.
- The targeted end-to-end test passes and Unity reports no new compile errors.

