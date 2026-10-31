---
slug: action-type-input-decoupling
status: approved
source: manual
gdd_tags: [combat, characters, platform-input, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Action Type for interrupt policy and Ultimate hit flash

Workflow: `/refactor`. Apricha explicitly narrowed the scope on 2026-10-29: Action Type
is for interrupt hierarchy and Ultimate white flash. Existing Skill/Ultimate input routes,
AE costs, cooldowns, damage/passive categories and ability windows stay authoritative.
Dimension Slash remains a zero-AE EX Skill on Skill input/window, classified Ultimate for
interrupts and MAX CANCEL. The earlier unified-catalog/executor proposal is withdrawn.

## Approved scope

- Add CombatAction.actionType: FromInput (legacy default), Normal, Skill, Ultimate.
- Use its resolved value for source policy and candidate interrupt classification.
- Detect Ultimate-type → distinct Ultimate-type Recovery MAX CANCEL at shared step commitment,
  including cross-input transitions. Keep opt-out, conditions, costs and structural chains.
- Snapshot type into execution/projectile and off-field presentation context for the existing
  Ultimate per-hit white flash. Keep accepted-hit/provenance behavior intact.
- Expose Action Type in the Animation Editor; author Dimension Slash as Ultimate type.
- No unified catalog, new executor, cost schema, automatic AE cost/invulnerability or list migration.

Acceptance: Skill-bound Ultimate type uses Ultimate policy, still resolves on Skill with zero AE,
retains its Skill window, participates in cross-input MAX CANCEL, and gets the white hit flash.
Default actions preserve their current behavior. Rejected transitions keep the original action.
The original meter/QTE/full-goal verification work remains required.
