---
slug: "bbee-trash-enemy-defense"
status: in-progress
source: manual
gdd_tags:
  - "enemies"
  - "bosses"
owner: "cursor-agent"
human_checkpoint: optional
next_agent: implementation-agent
blocked_by: []
---

# Task Card: BBEE-Style Trash Enemy Defense

## Player-Facing Goal

Normal enemies can have orange Super Armor (reduced damage, no stagger until broken) and attacks that dash alone cannot beat (undodgeable / grab). Bosses stay out of scope for this slice.

## Source

- Origin: manual prompt — start with normal enemies before bosses
- Requested by: user

## GDD Reference

- `@tag:bosses` — readable tells, dodge/reposition skill
- Content goals: trash / shield / elite archetypes

## Type

- [x] Feature

## Scope

### Systems Affected

- Enemy combat defense
- Enemy attack hitboxes
- Player incoming damage / invuln gate
- Player hitboxes (armor break flag)

### Files To Inspect First

- `Assets/Project/Scripts/AIBehaviour/EnemyController.cs`
- `Assets/Project/Scripts/Combat/EnemyAttackHitbox.cs`
- `Assets/Project/Scripts/Systems/PlayerCharacter.cs`
- `Assets/Project/Scripts/Character/HitFrameData.cs`

### Out of Scope

- Boss Break windows / purple boss patterns
- New enemy prefabs authored in-scene (opt-in on existing prefabs)
- Full BBEE roster / status CC overhaul

## Acceptance Criteria

- [x] Opt-in Super Armor on trash via EnemyController
- [x] Armored enemies take reduced damage and skip hit-reactions until armor breaks
- [x] Hits marked `breaksArmor` (or armor HP depleted) clear armor
- [x] EnemyAttackProperty Normal/Undodgeable/Grab gates dash i-frames
- [x] Existing Normal enemies unchanged when armor disabled and property = Normal
- [ ] Works on primary platform input model
- [ ] Does not break existing: Behavior Graph attacks, boss ignoreHitReactions

## Human Checkpoints

- [x] Design/game-feel direction approved (trash-first)
- [ ] Playtest one armored trash + one undodgeable trash swing
- [ ] PR review before merge

## Router Decision

Recommended workflow: `/implement-feature`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/bbee-trash-enemy-defense-design-spec.md`
