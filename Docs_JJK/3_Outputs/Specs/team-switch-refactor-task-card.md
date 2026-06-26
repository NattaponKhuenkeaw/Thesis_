---
slug: "team-switch-refactor"
status: draft
source: manual
gdd_tags:
  - "progression"
  - "combat"
owner: "implementation-agent"
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Team Switch and State Refactoring

## Player-Facing Goal

A more robust and performant team switching and UI state representation without any visual bugs or lagging. The team switching mechanics remain identical in gameplay, but memory allocations, redundant update frames, and script complexity are heavily reduced.

## Source

- Origin: Manual request
- Requested by: User
- Date: 2026-06-19

## GDD Reference

- `@tag:ui` - Team Builder & Team Lookup Flow, Drag & Drop rules
- `@tag:characters` - Character Stats scaling with Artifacts

## Type

- [ ] Feature
- [ ] Bug fix
- [x] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- `TeamLoader` runtime state management
- `TeamSwitchHUD` state and event updates
- `TeamSlotUI` artifact stat calculations and animations
- `PlayerCharacter` character data loading and caching

### Files To Inspect First

- `Assets/Project/Scripts/Systems/TeamLoader.cs`
- `Assets/Project/Scripts/Player/UI/TeamSwitchHUD.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/TeamSlotUI.cs`
- `Assets/Project/Scripts/Systems/PlayerCharacter.cs`

### Out of Scope

- Modifying player state machine switching rules or adding gameplay features.
- Modifying UI styles or layouts in prefabs.

## Acceptance Criteria

- [ ] Eliminates per-frame string signatures in `TeamSwitchHUD` Update loop.
- [ ] Streamlines `TeamLoader` dictionaries and consolidation/cleanup methods.
- [ ] Refactors background ultimate spawning to a static factory in `BackgroundUltimateExecutor`.
- [ ] Removes redundant coroutine animations for stat numbers in `TeamSlotUI` to update instantly.
- [ ] Simplifies `PlayerCharacter` Awake component bootstrapping and initialization.
- [ ] Passes compilation and preserves original combat swapping functionality.
- [ ] Follows `RULES_AND_POLICY.md`.

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/refactor`

Next artifact:
- `Docs_JJK/3_Outputs/Specs/team-switch-refactor-impact-analysis.md`
