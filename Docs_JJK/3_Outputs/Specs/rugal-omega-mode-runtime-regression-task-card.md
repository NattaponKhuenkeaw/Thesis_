---
slug: rugal-omega-mode-runtime-regression
status: approved
source: manual
gdd_tags:
  - buff-debuff
owner: implementation-agent
human_checkpoint: not-required
next_agent: qa-agent
blocked_by: []
---

# Rugal Omega Mode Runtime Regression

## Workflow

`/fix-bug`

## Symptoms

- Rugal's base Skill does not increase `rugal.skill_ult_used`.
- EX routing can become available without applying the Omega Mode self buff.

## Root Cause

- The base Skill counter required an action ID that is not consistently present on the observed Skill event path.
- Omega entry and exit conditions were changed from their action-event triggers to `Always`.
- Runtime-tag stat notifications rebuilt stats without reconciling tag-gated Always runtime effects.

## Expected Behavior

- Base Skill and base Ultimate each add one use.
- The fifth use adds `rugal.omega_mode`, resets the use counter, enables EX routing, and applies one Omega Mode self effect.
- Ending Skill EX or Ultimate EX removes the tag and the Omega Mode self effect.

## Acceptance Criteria

- Five base Skill uses enter Omega Mode.
- A mixed total of five base Skill/Ultimate uses enters Omega Mode.
- Omega Mode produces exactly one self runtime effect.
- EX completion removes routing and the self runtime effect.
