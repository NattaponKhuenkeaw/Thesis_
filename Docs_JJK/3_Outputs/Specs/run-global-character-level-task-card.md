---
slug: run-global-character-level
status: implemented
source: manual
gdd_tags:
  - characters
  - ui
  - progression
  - scene-roles
  - architecture
  - prototype-truth
owner: implementation-agent
human_checkpoint: required
next_agent: qa-agent
blocked_by: []
---

# Task Card: Run-Global Character Level

## Player-Facing Goal

Replace per-character EXP management with a casual run-wide level: a Level room event awards 1-3 shared levels and immediately strengthens every character, including characters not currently owned in the run.

## Source

- Origin: Manual prompt
- Link: Current Codex task
- Requested by: Project owner

## GDD Reference

- `@tag:characters` - Replace the current per-character Character Level and EXP Potion rules with one run-global Character Level.
- `@tag:ui` - Remove Character Level panel and EXP Potion management from the active Character Inspector direction.
- `@tag:progression` - Replace EXP Potions with Level-room event progression.
- `@tag:scene-roles` - Add the target `Level` safe-room interaction and presentation sequence.
- `@tag:architecture` - Keep run state authoritative and presentation reactive to level changes.
- `@tag:prototype-truth` - Record the new prototype path and mark the former individual-level path legacy.

## Type

- [x] Feature
- [ ] Bug fix
- [x] Refactor / legacy migration
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Run-scoped progression state and reset flow
- Character stat resolution and deployed runtime stat refresh
- Admin/prototype controls
- Character Inspector and EXP Potion legacy path documentation
- GDD character, UI, progression, scene-flow, architecture, and prototype-truth sections

### Files To Inspect First

- `Assets/Project/Scripts/Shop/PlayerRunInventory.cs`
- `Assets/Project/Scripts/Character/`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`
- `Assets/Project/Scripts/UI/AdminConsole.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/Panels/CharacterLevelPanel.cs`
- `Docs_JJK/1_Inputs_Templates/GDD_Neo-JJK.md`

### Out of Scope

- Building the complete `Level` room scene, background UI, camera timeline, animation, or final audio assets
- Deleting the individual level, EXP, Character Level panel, or Leveling Potion implementation
- Changing permanent Character Potential or Artifact Transcendence

## Acceptance Criteria

- [x] One run-global Character Level is authoritative for every character and starts at Lv.1.
- [x] Resetting/losing the run resets the global level to Lv.1.
- [x] A public admin `OnClick` method performs the prototype flow: trigger, weighted roll, apply, then refresh character stats.
- [x] The awarded increment is 1 at 65%, 2 at 25%, and 3 at 10%.
- [x] The increment is clamped to the authored Character Level cap.
- [x] All character stat resolution uses the same run-global level, whether or not the character is acquired or deployed.
- [x] Deployed characters refresh effective stats after the event without losing the correct runtime HP state.
- [x] Existing individual EXP, Leveling Potion, and Character Level panel code remains present but is explicitly marked legacy and is no longer the active design path.
- [x] GDD documents the current global-level direction and the future `Level` safe-room sequence.
- [x] Automated tests cover weighted-roll boundaries, clamping, reset behavior, and stat refresh integration where practical.
- [ ] Works on the primary PC KB+M / controller input model.
- [ ] Does not break Character Potential, Artifact equipment, team deployment, or run inventory persistence.
- [ ] Follows `RULES_AND_POLICY.md`.

## Human Checkpoints

- [x] Design direction supplied in the manual request
- [x] Run-global ownership supplied in the manual request
- [ ] Final game-feel approval for Level-room timing/camera/audio
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/implement-feature`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/run-global-character-level-design-spec.md`

## Verification Summary

- [x] Unity runtime and EditMode test assemblies compile in Unity 6000.3.4f1.
- [x] Focused Run Global Level suite: 12 passed, 0 failed, 0 skipped.
- [x] Full EditMode suite: 17 passed, 0 failed, 0 skipped.
- [ ] PC KB+M/controller navigation and in-scene `OnClick` binding require a human PlayMode checkpoint.
- [ ] Potential, Artifact, deployment, persistence, and current-HP behavior require the manual regression pass in the approved test plan.
- [ ] The separate `Level` room implementation and final game-feel approval remain deferred by scope.
