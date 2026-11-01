---
slug: enemy-reaction-spacing
status: draft
source: manual
gdd_tags:
  - enemies
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Enemy Deterministic Reaction Positioning

## Player-Facing Goal

During a hit combo, each reaction moves the enemy to a fixed, designer-authored position for that hit, keeping follow-up hits aligned and predictable.

## Source

- Origin: direct prompt on 2026-11-02
- Requested by: Apricha

## GDD Reference

- `@tag:enemies` — basic enemies can stagger or be knocked back lightly.

## Type

- [x] Bug fix

## Scope

### Systems Affected

- `HitFrameData` reaction authoring
- `HitBoxDamage` reaction payload construction
- `EnemyHitReactionHandler` position motor
- `EnemyLocomotion` reaction ownership handoff
- Reaction data for every playable character and their attacks/projectiles

### Migration Scope

- Inventory all character-authored `HitData` reaction fields across character action tables, projectile data, and other character-owned attack assets.
- Migrate horizontal and vertical reaction displacement authoring into deterministic target-position data. Preserve authored combo placement, launch height, and landing intent as position targets rather than speed/force values.
- Cover legacy `horizontalSpeed`, `launchSpeed`, and `reactionVelocity` values so no character remains on the old displacement path after migration.
- Keep timing and presentation fields such as impact freeze, active frames, recovery, reaction kind, and animation selection intact unless a separate design decision changes them.

### Existing Position Data Path

- `HitFrameData.useSnapToAnchor` remains opt-in. When enabled, the target is the active hitbox center plus its authored `snapOffset`.
- When `useSnapToAnchor` is disabled, the hit must provide a separate authored target position. The field/data path for this target is to be defined before implementation.
- The target is fixed for that reaction once resolved from hit data; each combo hit supplies its own target.
- The position motor moves the enemy to that target over the authored reaction frames. `horizontalSpeed` does not determine the destination.

### Files To Inspect First

- `Assets/Project/Scripts/Character/HitFrameData.cs`
- Character `ActionResolverTable` and projectile data assets under `Assets/Project/Character/Hero/`
- `Assets/Project/Scripts/Combat/HitBoxDamage.cs`
- `Assets/Project/Scripts/Character/HitReactionData.cs`
- `Assets/Project/Scripts/AIBehaviour/EnemyHitReactionHandler.cs`
- `Assets/Project/Scripts/Editor/Tests/EnemyPositionReactionTests.cs`

### Out of Scope

- Treating reaction speed or physics force as a substitute for an authored destination.
- Guessing per-combo target coordinates during migration; preserve or explicitly review each authored reaction intent.

## Root Cause

The current implementation uses authored anchor snapping only during impact freeze for opt-in hits, then can derive subsequent displacement from `horizontalSpeed` and active frames. Other hits have no authored target path. This does not guarantee that every combo step reaches its designer-authored position.

## Acceptance Criteria

- [ ] Preserve opt-in `useSnapToAnchor` + `snapOffset` targeting.
- [ ] Define an explicit authored target path for hits without `useSnapToAnchor`.
- [ ] Inventory and migrate horizontal and vertical reaction authoring for every playable character and character-owned attack asset.
- [ ] Preserve each migrated hit's intended combo position, launch/air position, and landing behavior.
- [ ] Audit that no character-authored hit still relies on legacy speed/velocity fields for reaction placement.
- [ ] Resolve each hit's target once and move to it deterministically over reaction frames.
- [ ] Keep locomotion from overriding reaction-owned position movement.
- [ ] Do not use `horizontalSpeed` or physics force as the target position.
- [ ] Human playtest confirms follow-up hits remain aligned and predictable.

## Human Checkpoints

- [x] Design/game-feel direction recorded from Apricha's clarification
- [ ] Architecture approval before implementation
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- Architecture plan for the authored target path of non-snap hitboxes
