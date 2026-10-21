---
slug: damage-text-gnf-font
status: approved
source: manual
gdd_tags:
  - architecture
owner: implementation-agent
human_checkpoint: not-required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Use the Shared FCT Prefab for Combat Feedback

## Player-Facing Goal

Player and enemy floating combat feedback uses the serialized FCT prefab, including damage, healing, shields, DoT, additional damage, and artifact-particle damage.

## Source

- Origin: manual prompt
- Link: Current Codex task
- Requested by: project owner

## GDD Reference

- `@tag:architecture` - Presentation reacts to gameplay events, including damage numbers.

## Type

- [x] Bug fix

## Scope

### Systems Affected

- Enemy world damage text template
- Player world feedback template
- Floating combat text prefab references

### Files To Inspect First

- `Assets/Project/Prefab/CombatUI/FCT.prefab`
- `Assets/Project/Prefab/UI/EnemyWorldCanvasCore.prefab`
- `Assets/Project/Prefab/Player.prefab`
- `Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab`
- `Assets/Project/Scripts/UI/EnemyWorldUI.cs`
- `Assets/Project/Scripts/UI/WorldTextUtility.cs`

### Out of Scope

- Changing damage number styling or animation
- Changing fonts on unrelated UI

## Acceptance Criteria

- [x] The FCT prefab uses GNF TTF SDF and its matching material.
- [x] Player and enemy world UI serialize the FCT prefab as their shared feedback template.
- [x] Damage, healing, shield, DoT, additional, and artifact-particle text continue through the existing type styling and pooling paths.
- [x] Enemy UI keeps its local hidden text as a fallback if the FCT prefab reference is unset.

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- Implementation diff in the shared FCT prefab references and enemy UI template selection
