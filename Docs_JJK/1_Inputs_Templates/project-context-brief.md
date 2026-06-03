# Project Context Brief

> Filled 2025-06-03. Source: architecture review + GDD_Neo-JJK.md.

---

## 1. Identity

| Field | Value |
|-------|-------|
| Project Name | Neo-JJK |
| Genre | 2D Side-Scrolling / Action / Hack-and-Slash / Rogue-lite |
| Core Fantasy | Clear chaotic side-scrolling rooms with a team of flashy characters, swapping to spend skills and burst enemies. |
| Tone keywords | Fast, flashy, supernatural, arcade-like, lightweight rogue-lite |
| Primary Platform | PC (Keyboard + Mouse) |
| Secondary Platform | PC (Controller) |
| Camera Style | 2D side-scrolling combat plane |

## 2. Tech Stack

| Area | Value |
|------|-------|
| Engine + Version | Unity 6 (6000.x LTS) |
| Render Pipeline | URP 17.x |
| 2D or 3D | 2D |
| Input System | Unity Input System 1.8.x |
| Camera System | Cinemachine 3.x |
| UI Framework | Unity UI Canvas + TextMeshPro 4.x |
| Networking | None (single-player) |

## 3. Scenes in Build

```
- MainMenu → Title screen, settings, save slot
- Lobby → Walkable hub, Soul Shop, team builder (Blazblue Entropy Effect style)
- CombatRoom → Side-scrolling room-clear with enemy waves
- RewardRoom → Upgrade choice, heal, event encounter
- BossRoom → Pattern-based boss fight
- Result → Run win/fail summary, return to lobby
```

## 4. Core Systems — File Map

| System | Script Path | State | Notes |
|--------|------------|-------|-------|
| Player FSM | `Assets/Project/Scripts/Player/` | Works | 12 states, priority transitions, input buffer |
| Combat Engine | `Assets/Project/Scripts/Combat/` | Needs refactor | Adapt ActionResolver, add FSM ground/air overriding, retain grabs |
| Character Data | `Assets/Project/Scripts/Character/` | Works | SO-driven: stats, combos, skills, progression |
| Enemy AI | `Assets/Project/Scripts/AIBehaviour/` + `Behavior/` | Works | Unity Behavior Graph nodes |
| Team System | `Assets/Project/Scripts/Systems/` | Works | TeamLoader, PlayerCharacter facade |
| Shop/Inventory | `Assets/Project/Scripts/Shop/` + `Inventory/` | Works | Buy/sell, codex, character items |
| Camera | `Assets/Project/Scripts/Cameras/` | Works | Cinemachine explore/combat switching |
| UI | `Assets/Project/Scripts/UI/` | Works | HUD, skill tree, world UI, debug |
| Parallax | `Assets/Project/Scripts/Parallax/` | Works | Background scrolling |
| Editor Tools | `Assets/Project/Scripts/Editor/` | Works / Keep | Good infrastructure, adapt logic for simpler Neo direction |

## 5. Architecture Reality

### Current Pattern

Custom FSM + ScriptableObject data + frame-data combat engine. `PlayerCharacter` facade owns all subsystems. `PlayerStateMachine` manages state transitions with priority and input buffering. Combat is data-driven through `CharacterObject` → `ActionResolverTable` → combo trees. Enemies use Unity Behavior Graph. Team of 3 managed by `TeamLoader`.

### Known Tech Debt
- `PlayerCombat.cs` (48 KB) and `CombatPlayerState.cs` (32 KB) are oversized monoliths
- Fighting-game combo branching contradicts Neo direction (adapt ActionResolver and FSM overriding instead)
- Filename/class mismatches (`PlayerInputHandler.cs` ↔ `InputHandler_Player`)
- Debug shortcuts bypass progression in production input handler
- Static `InputLockManager` can permanently lock input
- 0% test coverage

## 6. Design Boundaries

### Experience Pillars
1. **Fast-Paced Hack & Slash** — moment-to-moment action and flashy visuals
2. **Beat Them and Acquire Them** — defeat bosses to unlock characters
3. **Simple Team Rotation** — DPS → support → buff → burst → repeat

### Hard Rules
- No fighting-game combo routing or frame traps
- No air attack systems or plunge attacks
- Bosses are pattern-based, not combo targets
- Low development effort is a design requirement
- VFX impact over animation complexity

### Architecture Rules
1. Data-driven character definitions via ScriptableObject
2. Cooldown timers over complex combo states
3. Simple enemy state machines, not fighting-game AI
4. Presentation reacts to gameplay events (VFX/SFX hooks)
5. Keep systems small, do not over-engineer

## 7. Commit Scopes

```
player · combat · enemy · character · team · shop · inventory · ui · camera · vfx · audio · scene · build · docs
```

---

> Generated from architecture review + GDD_Neo-JJK.md. Ready for bootstrap workflow.
