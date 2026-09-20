---
slug: "player-action-pseudo-super-armor"
status: ready-for-review
source: manual
gdd_tags:
  - "combat-player-interrupt-armor"
owner: "antigravity"
human_checkpoint: required
next_agent: orchestrator-agent
blocked_by: []
---

# Task Card: Player Action Interruption & Pseudo Super Armor

## Player-Facing Goal

When the player commits to an attack, skill, or ultimate action, taking damage during the active swing or recovery follow-through does not interrupt or stagger the player into hitstun. Damage is still taken, health is reduced, and shields/effects react, but the action completes cleanly (pseudo super armor). The only window an enemy attack can interrupt the player's action is during the initial wind-up (Starting Frame / Startup Phase). When the player is not performing an action (e.g. idle or running), taking damage interrupts into hitstun as usual.

## Source

- Origin: Manual Prompt
- Requested by: User
- Context: Redesign player action interruption so only the starting frames/startup phase can be interrupted.

## GDD Reference

- `@tag:combat-player-interrupt-armor` - Player frame-data actions feature pseudo super armor: taking damage outside the startup phase applies full damage without causing flinch or canceling the action into hitstun.

## Type

- [x] Feature
- [ ] Bug fix
- [x] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- `PlayerState` / `FrameDataPlayerState` (interruption eligibility query)
- `PlayerHitstunState` (always interruptible by damage)
- `PlayerCharacter` (TakeDamage pipeline checks state interruptibility before changing state)
- `EnemyAttackHitbox` / `EnemyRangedShot` (pass reaction context; decoupled direct animator trigger)

### Files To Inspect First

- `Assets/Project/Scripts/Player/PlayerState.cs`
- `Assets/Project/Scripts/Player/FrameDataPlayerState.cs`
- `Assets/Project/Scripts/Player/States/PlayerHitstunState.cs`
- `Assets/Project/Scripts/Systems/PlayerCharacter.cs`
- `Assets/Project/Scripts/Combat/EnemyCombatTypes.cs`
- `Assets/Project/Scripts/Combat/EnemyAttackHitbox.cs`
- `Assets/Project/Scripts/Combat/EnemyRangedShot.cs`

### Out of Scope

- Enemy poise/super armor changes (already governed by `EnemyCombatRole` / `EnemyKit`).
- Invulnerability iframe changes for Dash/Ultimate.

## Acceptance Criteria

- [x] Locomotion states (`PlayerIdleState`, `PlayerMoveState`, `PlayerFallingState`, `PlayerLandingState`) allow interruption by damage into `HitstunState`.
- [x] Frame-data action states (`FrameDataPlayerState` subclasses including `CombatPlayerState`, `PlayerAttackState`, `PlayerSkillState`, `PlayerUltimateState`, `PlayerDashState`) allow damage interruption ONLY during the starting frame / startup phase (`ActionPhase.Startup` or `currentFrame <= 1`).
- [x] In subsequent phases (`ActionPhase.Active`, `ActionPhase.Recovery`, `ActionPhase.Cancel`), incoming damage is applied (HP reduced, shield absorbed, float text displayed, invulnerability duration set), but does NOT interrupt into `HitstunState` (pseudo super armor).
- [x] `PlayerCharacter.HasSuperArmor = true` prevents interruption in any phase.
- [x] Lethal damage (`currentHP <= 0f`) always interrupts and defeats the character regardless of armor or phase.
- [x] `EnemyAttackHitbox` and `EnemyRangedShot` no longer force hurt animation on armored actions; animation is owned by `HitstunState`.
- [x] Follows `RULES_AND_POLICY.md` and passes test suite.

## Human Checkpoints

- Feel pass on Startup window duration across character combos.
