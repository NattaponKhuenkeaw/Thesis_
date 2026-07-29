---
slug: deployed-character-live-stats
status: implemented
source: manual
gdd_tags:
  - combat
  - characters
  - progression
owner: implementation-agent
human_checkpoint: approved
next_agent: human
blocked_by: []
---

# Task Card: Deployed Character Live Stats

## Goal

All deployed characters retain an authoritative runtime record while active or
off-field. Damage resolves from its recorded source character, never from the
currently controlled character.

## Rules

- Projectile and Additional Damage read the source's live effective stats when
  damage resolves.
- Buff, debuff, shield, and heal formulas snapshot source stats when applied
  unless explicitly authored as live-on-resolve.
- Removing a source from the deployed team falls back to the captured source
  snapshot.
- Runtime caches are not serialized into `RunTeamSlot`.
- Validation uses one E2E test only.

## Router Decision

Workflow: `/refactor`

The user approved the architecture on 2026-07-29 with “LGTM Process the plan.”

## Result

- Runtime-only records now cover every deployed character.
- Each deployed runtime now retains the artifact loadout that produced its
  effective stats; artifact lookup is resolved from character identity instead
  of the active or caller-supplied slot.
- Projectile and Additional Damage resolve live source stats at damage time.
- Ordinary formula-bearing effects retain their application snapshot.
- Off-field damage evaluates the source character's own Potential modifiers
  without borrowing the active character's shared passive controller.
- The approved E2E and the artifact-ownership switch regression both pass.
