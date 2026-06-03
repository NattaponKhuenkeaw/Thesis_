# Neo-JJK — Project Stack & Context

> Universal bootstrap for AI agents. Under 400 tokens.

## Identity
- **Game:** Neo-JJK — 2D side-scrolling action hack-and-slash rogue-lite
- **Engine:** Unity 6 (6000.x LTS), URP 17.x, C#
- **Platform:** PC-first (KB+M / Controller)
- **Fantasy:** Clear chaotic rooms with a flashy character team, swapping to spend skills and burst enemies.

## Tech Stack
- Input System 1.8.x · Cinemachine 3.x · Unity UI + TMP · Physics2D
- VFX Graph + Shader Graph 17.x · Unity Behavior Graph (enemy AI)
- No networking. Single-player.

## Core Systems
| System | Location | State |
|--------|----------|-------|
| Player FSM | `Assets/Project/Scripts/Player/` | Works |
| Combat | `Assets/Project/Scripts/Combat/` | Refactoring (adapt ActionResolver and FSM) |
| Character SO | `Assets/Project/Scripts/Character/` | Works |
| Enemy AI | `Assets/Project/Scripts/AIBehaviour/` + `Behavior/` | Works |
| Team | `Assets/Project/Scripts/Systems/` | Works |
| Shop | `Assets/Project/Scripts/Shop/` + `Inventory/` | Works |
| Camera | `Assets/Project/Scripts/Cameras/` | Works |
| UI | `Assets/Project/Scripts/UI/` | Works |

## Architecture Rules
1. Adapt ActionResolver for simple combos, add FSM ground/air overriding
2. Data-driven via ScriptableObject (`CharacterObject`)
3. Cooldown timers over combo states
4. VFX impact > animation complexity
5. Simple enemy FSM via Behavior Graph
6. Keep systems small, do not over-engineer

## Key Docs
- GDD: `Docs_JJK/1_Inputs_Templates/GDD_Neo-JJK.md` — use `@tag:` sections
- Architecture Review: `ARCHITECTURE_REVIEW.md`
- Rules: `Docs_JJK/0_User_Manual/RULES_AND_POLICY.md`

## Commit Convention
```
type(scope): description
Types: feat, fix, refactor, docs, style, test, chore, juice
Scopes: player · combat · enemy · character · team · shop · inventory · ui · camera · vfx · audio · scene · build · docs
```

## Context Manifest
| File | Load When |
|------|-----------|
| `Docs_JJK/1_Inputs_Templates/project-stack.md` | Every agent call |
| `Docs_JJK/1_Inputs_Templates/GDD_Neo-JJK.md @tag:{section}` | Design intent needed |
| `ARCHITECTURE_REVIEW.md` | Codebase understanding needed |
