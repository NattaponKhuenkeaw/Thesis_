---
slug: "combat-stats-and-ui-refactoring"
status: draft
source: manual
gdd_tags:
  - "combat"
  - "characters"
  - "progression"
  - "system-ownership"
owner: "implementation-agent"
human_checkpoint: required
next_agent: game-design-agent
blocked_by: []
---

# Task Card: Character Stats, Combat Formula Refactor, and TeamBuilder UI Split

## Player-Facing Goal

Players should experience an expanded, RPG-style progression system with proper stat scaling (including Critical Hits, Cooldown Reduction, Skill/Damage Masteries, and Damage Resistance). During active runs, players will view their static Team Info, but they can only customize their characters and loadouts when in specific safe zones (like the Lobby or Soul Shop).

## Source

- Origin: Manual user request
- Requested by: User

## GDD Reference

- `@tag:characters` - Character Stats table and progression metrics.
- `@tag:combat` - Damage formulas, cooldown, and rotation rules.
- `@tag:progression` - Rogue-lite progression, Lobby NPC interactions, and loadout locking.

## Type

- [x] Feature
- [x] Refactor
- [ ] Bug fix
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- **Stat System**: `CharacterStats.cs` data storage, Character/Enemy runtime stats initialization (`PlayerCharacter.cs`, `EnemyController.cs`).
- **Combat System**: Attacker-to-target calculations in `PlayerCombat.cs`, `HitBoxDamage.cs`, and `EnemyAttackHitbox.cs`.
- **Movement System**: Speed (SPD) override in `PlayerMovement.cs`.
- **UI System**: `TeamBuilderUI.cs` and `TeamSlotUI.cs` split into static "Normal Team Data" and interactive "Team Builder" modes.

### Files To Inspect First

- `Assets/Project/Scripts/Character/CharacterStats.cs` (Modify/re-author stats layout)
- `Assets/Project/Scripts/Character/CharacterObject.cs` (Adapt ScriptableObject properties)
- `Assets/Project/Scripts/Systems/PlayerCharacter.cs` (Runtime character stats & damage receipt)
- `Assets/Project/Scripts/AIBehaviour/EnemyController.cs` (Runtime enemy stats & damage receipt)
- `Assets/Project/Scripts/Combat/PlayerCombat.cs` (Damage output, cooldown multiplier logic)
- `Assets/Project/Scripts/Combat/HitBoxDamage.cs` (Player hit resolver)
- `Assets/Project/Scripts/Combat/EnemyAttackHitbox.cs` (Enemy hit resolver)
- `Assets/Project/Scripts/Combat/PlayerMovement.cs` (Override speed with stats)
- `Assets/Project/Scripts/TeamBuilder/UI/TeamBuilderUI.cs` (Manage UI states/modes)
- `Assets/Project/Scripts/TeamBuilder/UI/TeamSlotUI.cs` (Adjust slot interactivity based on mode)

### Out of Scope

- Implementing actual Buff/Debuff dynamic gameplay systems (only preparing placeholders in formulas).
- Implementing new Artifact item models or generation loops (only adding stat hooks).
- Creating new UI textures, animations, or layouts.

## Acceptance Criteria

### 1. Stats and Sync
- [ ] Update `CharacterStats.cs` to include: ATK, DEF, SPD, CDR, CRIT, CRIT dmg, SKILL Mastery, DMG Mastery, and DMG Res.
- [ ] Runtime sync is established: character/enemy instances read and cache these values upon loading.
- [ ] SPD stat overrides default move speed from `MovementProfile` in the active movement calculations. (Note: SPD multiplier should be formatted as an integer with additions, e.g. `10(+2)`).
- [ ] CDR stat reduces active skill/ultimate cooldown durations using a proper cooldown rate multiplier formula.

### 2. Math Formula Preparation
- [ ] Prepare standard damage equations in combat logic:
  - Base damage scaled by ATK and attack multipliers.
  - Critical hits evaluated via CRIT rate and CRIT dmg.
  - DMG Mastery and SKILL Mastery applied to final outputs.
  - Defender DEF and DMG Res reducing incoming damage.
- [ ] Prepare variables and hooks inside the math calculation handler for Artifact Stats, Buffs/Debuffs, and Resistance handlers.

### 3. TeamBuilder UI Refactoring
- [ ] Refactor the Combat / Safe Zone UI to be a full-screen paused menu overlay.
- [ ] Split `TeamBuilderUI` into two distinct modes:
  1. **Normal Team Data (Combat UI)**: 
     - A static, read-only 3-Character Grid display.
     - Displays Character Name, Base & Additional Basic Stats (ATK, DEF, HP), and 3 small Artifact slots.
     - Drag-and-drop, character switching, and loadout edits are disabled.
     - Clicking a character opens an **Inspect** view showing: All Stats and Passive Skills, Equipped Artifact Stats and Passive Skills, Character Potential (current & levels 1-6), and Character Lore/Backstory.
  2. **Team Builder (Safe Zone UI)**: 
     - The interactive mode allowing full drag-and-drop team composition and layout modifications.
     - Usable only in safe zones like Shop/Lobby.
- [ ] UI elements respond contextually to the current game state/zone to toggle between these modes.
- [ ] Works on primary platform input model and follows `RULES_AND_POLICY.md` constraints.

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/implement-feature` followed by `/refactor`

Next artifact:
- `Docs_JJK/3_Outputs/Specs/combat-stats-and-ui-refactoring-design-spec.md`
