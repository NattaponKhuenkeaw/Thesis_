---
slug: boss-death-flow
status: in-progress
source: manual
gdd_tags:
  - bosses
  - rooms
owner: human+agent
human_checkpoint: playtest-feel
next_agent: human
blocked_by: []
---

# Task Card: Boss Death Flow

## Player-Facing Goal

When a boss dies: hit-stop + slow-mo, clear leftover hazards, absorb an energy orb reward (no floor loot scatter), free-move with a click prompt to the next room, and arrive fully healed (team revive/heal happens during scene transition — never in the boss arena).

## Source

- Origin: manual prompt (Boss Death Flow brief)
- Requested by: project owner

## GDD Reference

- `@tag:bosses` — boss encounter pacing / telegraph → punish → clear
- Run progression: boss clear resets segment depth via existing RoomChoiceDirector path

## Type

- [x] Feature

## Scope

### Systems Affected

- Boss death orchestration (`BossDeathSequence`)
- Enemy despawn timing for bosses
- Room clear exit presentation (suppress portal/choice UI in boss rooms)
- Projectile / telegraph / hitbox hazard cleanup
- Scene transition team heal/revive

### Files To Inspect First

- `Assets/Project/Scripts/AIBehaviour/EnemyController.cs`
- `Assets/Project/Scripts/AIBehaviour/Boss/BossPatternController.cs`
- `Assets/Project/Scripts/Core/ZoneState/RoomWaveManager.cs`
- `Assets/Project/Scripts/Core/ZoneState/SceneTransitionManager.cs`
- `Assets/Project/Scripts/Combat/GameFreezeManager.cs`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`

### Out of Scope

- Boss dissolve VFX (use normal death animation)
- Result / summary UI
- Floor item scatter rewards
- Healing / revive inside the boss arena

## Acceptance Criteria

- [ ] Boss HP 0 triggers short hit-stop then slow-mo, then normal time
- [ ] Projectiles, ground telegraphs, and boss attack hitboxes clear immediately
- [ ] Energy orb spawns from boss, pops, then homes to the player (no floor loot)
- [ ] After orb absorb: free movement + prompt "กดคลิกซ้ายเพื่อไปด่านต่อไป"
- [ ] Left click calls `SceneTransitionManager.TransitionToRoom(...)`
- [ ] Team heal/revive runs during transition load, not in the boss room
- [ ] Normal combat room clear (portals + choice UI) still works

## Human Checkpoints

- [x] Design brief provided by owner (this card)
- [ ] Playtest timing (hit-stop / slow-mo / orb lerp)
- [ ] PR review before merge

## Router Decision

Recommended workflow: `/implement-feature`
