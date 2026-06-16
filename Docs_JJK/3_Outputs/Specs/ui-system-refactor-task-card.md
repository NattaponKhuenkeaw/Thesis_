---
slug: "ui-system-refactor"
status: draft
source: manual
gdd_tags:
  - "Team Builder & Team Lookup Flow"
  - "Character Inspector System"
owner: "orchestrator-agent"
human_checkpoint: required
next_agent: orchestrator-agent
blocked_by: []
---

# Task Card: Refactor TeamBuilder and InspectorUI

## Player-Facing Goal

Players will experience a consistent, clean, and intuitive user interface when interacting with the Character Inspector, TeamBuilder, and TeamLookup. The interfaces will correctly adhere to Safe Zone / Combat Zone rules (e.g., disabling team editing in combat) and display character/artifact information cleanly based on modular, data-driven backends.

## Source

- Origin: manual prompt
- Link: N/A
- Requested by: User

## GDD Reference

- `### Team Builder & Team Lookup Flow` - Defines Safe Zone rules (editing only in Safe Zones) and TeamLookup grid-box behavior.
- `### Character Inspector System` - Defines modular layout containing Character Slider Panel, Character Visual Panel, and Content Panel with its specific sub-windows (Level, Potential, Equipment, etc.).

## Type

- [ ] Feature
- [ ] Bug fix
- [x] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- TeamBuilder / TeamLoadUp
- Character Inspector System (InspectorUI)

### Files To Inspect First

- `Assets/...` (Unity UI prefabs and scripts for InspectorUI and TeamBuilder)
- `Docs_JJK/1_Inputs_Templates/GDD_Neo-JJK.md`

### Out of Scope

- Adding new UI features outside of the GDD specification for these interfaces.
- Modifying core gameplay combat logic or character base stats.

## Acceptance Criteria

- [ ] TeamBuilder and TeamLoadUp are refactored into cleanly separated, modular components.
- [ ] InspectorUI is refactored from a monolithic script into supported sub-modules (Slider, Visual, Content Panels).
- [ ] UI properly enforces Safe Zone vs Combat Zone accessibility rules.
- [ ] Systems are fully data-driven rather than relying on hard-coded values.
- [ ] Works on primary platform input model
- [ ] Does not break existing: UI navigation, rendering, and scene transitions
- [ ] Follows `RULES_AND_POLICY.md`

## Human Checkpoints

- [x] Design/game-feel approval
- [x] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/refactor`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/ui-system-refactor-design-spec.md`
