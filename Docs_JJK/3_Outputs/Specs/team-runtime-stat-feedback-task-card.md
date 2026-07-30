---
slug: "team-runtime-stat-feedback"
status: approved
source: manual
gdd_tags:
  - "progression"
owner: "implementation-agent"
human_checkpoint: required
next_agent: qa-agent
blocked_by: []
---

# Task Card: Team Runtime Stat Feedback

## Player-Facing Goal

Artifact equip, removal, and swaps visibly communicate upgrades or downgrades,
and closing Team UI leaves the active character, HUD, HP limits, cooldown
calculations, Potential, and equipped Artifact stats synchronized with
`TeamLoader`.

## Source

- Origin: Manual prompt
- Requested by: User
- Date: 2026-06-09

## GDD Reference

- `@tag:progression` - Team Builder loadout, Character Potential, Character
  Level, and Artifact Transcendence rules.

## Type

- [ ] Feature
- [x] Bug fix
- [ ] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Team Builder stat feedback
- Artifact slot drag and drop
- TeamLoader runtime artifact storage and stat refresh
- Runtime team-effect stat calculation and source-stat lookup
- Character Level and Artifact Transcendence design documentation

### Files To Inspect First

- `Assets/Project/Scripts/TeamBuilder/UI/TeamSlotUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/ArtifactSlotUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/TeamBuilderUI.cs`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`
- `Docs_JJK/1_Inputs_Templates/GDD_Neo-JJK.md`

### Out of Scope

- Implementing EXP Potion inventory, level curves, or Character Level UI.
- Implementing Artifact Fragment inventory, Transcendence costs, or permanent
  save files.
- Changing authored Artifact balance values.

## Acceptance Criteria

- [x] ATK, DEF, and HP text pulse green on an effective increase and red on an
  effective decrease.
- [x] Artifacts that only modify non-card stats still trigger visible TeamSlot
  upgrade or downgrade feedback.
- [x] Cross-character Artifact swaps refresh both source and destination slots.
- [x] TeamLoader stores equipped Artifacts in Unity-serializable per-slot data.
- [x] Closing Team UI rebuilds runtime stats, clamps HP/AE to refreshed limits,
  reloads the active character, and publishes refresh events.
- [x] Character Potential changes are reapplied when runtime state refreshes.
- [x] A self-targeted stat effect remains isolated to its owner when an allied
  runtime effect queries that owner's source stats.
- [x] Shermie Skill Potential Lv.2 may shield allies without copying Shermie's
  self-only +50% DEF to them.
- [x] Shermie Ultimate Lv.0 grants +50% Max HP only to Shermie, including while
  a Shermie-sourced allied effect is active.
- [x] Character Level and Artifact Transcendence rules are documented as target
  design without claiming the unimplemented systems exist.
- [x] Works on the primary PC input model.
- [x] Does not break Team Builder draft/deploy authority.
- [x] Follows `RULES_AND_POLICY.md`.

## Human Checkpoints

- [x] Design direction supplied in the source prompt
- [x] Existing artifact runtime architecture already approved
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- `Docs_JJK/3_Outputs/TestPlans/team-runtime-stat-feedback-regression.md`
