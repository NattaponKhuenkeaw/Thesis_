---
slug: main-shadow-meter-action-id
status: approved
source: manual
gdd_tags: [characters, progression]
owner: implementation-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Main Shadow Meter Stops Increasing on Normal Hits

## Report

After the kit-information migration, Main's Normal Attacks no longer generate Shadow Meter.

## Investigation

The migration assigns stable IDs to combo actions. Main's combat meter rules still identify Normal actions by the legacy action names `Ground` and `Air`. `CharacterCombatRules.MeterGainRule.Matches` prefers a supplied action ID over its action name, so these rules stop matching once the combo actions have stable IDs.

## Fix

Update legacy meter-rule keys to the unique matching stable action ID after action IDs are assigned. Apply the same conversion when updating already-migrated character data. Do not change meter amounts or hit handling.

## Acceptance

- Main's Ground and Air meter rules match their stable combo action IDs.
- Existing per-hit values and step-specific gains remain unchanged.
- Re-running the authoring migration is idempotent.
- Gameplay regression is left for manual Unity playtest; do not claim it was playtested.

## Workflow

- Selected workflow: `/fix-bug`.
- Related feature: `kit-information-task-card.md`.
- No branch switch, commit, merge, or publishing is included in this workspace repair.
