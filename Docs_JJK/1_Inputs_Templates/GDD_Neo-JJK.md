# Neo-JJK - Game Design Document

## Purpose

This document is the current design authority for **Neo-JJK**.

`GDD_JJK.md` is a legacy plan. Keep it as historical reference, but do not use it as the active combat direction unless a human explicitly asks for legacy comparison work.

Neo-JJK has shifted away from action/fighting-game hybrid combat and into a simpler **2D side-scrolling action hack-and-slash rogue-lite**. The design target is fast, readable, flashy, and easy to build.

The most important production constraint is:

- **Low development effort**
- **Casual low skill floor**
- **Casual low skill ceiling**
- **Simple logic over clever systems**
- **VFX impact over animation complexity**

This Design aim for Casual Game with Low Floor. Low Ceiling. High Impact in Flashy visuals.

---

<!-- @tag:identity -->
## Project Identity

| Field | Direction |
| --- | --- |
| **Working Title** | **Neo-JJK** |
| **Genre** | **2D Side-Scrolling / Action / Hack-and-Slash / Rogue-lite** |
| **Core Fantasy** | Clear chaotic side-scrolling combat rooms with a small team of flashy characters, swapping them to spend skills, apply buffs, and burst enemies down. |
| **Player Promise** | Fast basic action, readable attacks, big VFX, simple upgrades, and easy team rotations. |
| **Game Design Philosophy** | **Casual low floor and low ceiling. fast-paced reward and execution.** |
| **Development Philosophy** | Highly Systematic and Architecture like senior programmer Clean Code. Prefer simple, reliable systems rather than hard-coded. The codebase should short and modular to prevent Bug. |
| **Platform Priority** | PC-Focus (Keyboard/Controller) |
| **Camera / Play Plane** | 2D side-scrolling combat plane. |
| **Tone** | Fast, flashy, supernatural, arcade-like, lightweight rogue-lite pressure. |
| **Story Intent** | "Shin" died and was transported into Death Game Cursed Realm by Spirit Waifu name "Blind" and discovered that all players contain a Cursed Technique called "Aura" that amplify their fighting spirit to fight Monster "Fiend". All of you are rival and your enemy is your time. No matter you died in Curse Realm you will respawn at the beginning but only  1 out of 50 Players can survive and brought back to life so if you are getting back behind, you will forget who you are and become a one of "Fiend". |

### Experience Pillars

1. **Fast-paced Combat Hack & Slash**
   - The gameplay prioritize **Moment-to-Moment Action and Flashy Visuals**.
   - **Simple Learning Curve** : No character reqiured much learn to mastery. High skill come from Team-Building focus

2. **Beat Them and Acquire Them**
   - **Defeat Bosses**: Defeating other players (bosses) in the Cursed Realm unlocks them as playable characters in your pool.
   - **Soul Shop**: Shop for selling what you get from previously run (like *Dead Cell*) such as Character, Artifact, Items etc and Purchase and recruit defeated players to build your team.
   <img src="https://digitalchumps.com/wp-content/uploads/2024/01/Jin.gif?x96629" alt="Blazblue Entropy Effect Combat]" width="800">

3. **Rogue-lite Mechanics**
   - **Soul System (Character Potential)**: Upgradable character progression that unlocks new combo routes, modifies passive loops, and changes skill utilities.
   - **Artifact System**: One-time-owned Soul Shop equipment that adds runtime stats to a deployed character.

---

<!-- @tag:visual-audio -->
## Visual, Mood, and Audio Direction

### Mood Board Keywords

<img src="https://i.redd.it/p4scnbqwrp991.gif" alt="Concept art" width="800">

- 2D side-scrolling dungeon action
- Flashy supernatural slashes and impacts
- Fast room clears
- Readable boss telegraphs
- Hollow Knight-like boss readability
- Dungeon Slasher / Skul-like action pacing

### Art Direction

Neo-JJK should prioritize readable silhouettes, large hit VFX, strong attack shapes, and simple enemy tells. Character animation can be short and economical as long as the VFX sells the hit.

Avoid animation requirements that demand precise martial-arts readability, frame-perfect cancel points, or long hand-authored combo strings.

### Combat Presentation Rules

- Use large slash arcs, burst circles, impact flashes, hit-stop, and camera shake to sell attacks.
- Keep enemy danger zones visually clear.
- Prefer simple wind-up, active, and recovery presentation over detailed frame-data logic.
- Boss attacks should telegraph through clear shapes, warning zones, projectiles, and arena patterns.

### Audio Direction

Sound should make combat easy to read:

- Basic hits need short, punchy feedback.
- Skills and ultimates need distinct charge/release sounds.
- Swap actions need a quick identity cue.
- Boss danger tells should be audible before the attack becomes active.

---

<!-- @tag:platform-input -->
## Platform and Input Philosophy

### Primary Rule

**PC Keyboard & Mouse is the authoritative input model.** Gamepad controller support is a mapped port layer mirroring the exact same verb set and timing windows, without redesigning combat around precision differences.

### PC Keyboard & Mouse Controls

| Input | Combat Verb | Description / Behavior |
| --- | --- | --- |
| **A / D** | Move Left / Right | Horizontal locomotion. Dash cancels can be directed by holding A or D. |
| **Space** | Jump | Double jump supported. Press in air to double jump. |
| **LMB** | Basic Attack (BA) | Performs state-based basic attack chains. |
| **RMB** | Skill | Triggers character-specific skill. |
| **Shift** | Dash | Invincibility-frame dash. |
| **Q** | Ultimate | Triggers character ultimate. |
| **S (on platform)** | Drop down | Drop down to the platform below. |
| **1, 2, 3** | Team Swap | Swaps active character to slot 1, 2, or 3. |

### Input Rules

1. **Buffer Window**: The system maintains a **12-frame input buffer**. Inputs registered within 12 frames of an active action ending are queued and executed on the first possible frame.
2. **Priority Input Type over Input Order** : Input Type Hierarchy is **"System/Swap/Reaction" -> "Skill/Ultimate" -> "Dash/Jump" -> "Basic Attack" -> "Basic Movement"**. If the player presses different input type in the buffer window, the input type with higher priority will be executed and clear the cache.
3. **Team Swap** : When press Team Swap will clone another character into field with **Swap animation sequence** while the previous character will continue to execute their last action until end then disappear(this called **Team Swap Cancel**) and new character will Inheritance the last Input buffer of previous character to continue chaining.

---

<!-- @tag:core-loop -->
## Core Game Loop

<img src="https://digitalchumps.com/wp-content/uploads/2024/01/Gorgeous-action-packed-battles.gif?x96629" alt="Blazblue Entropy Effect Combat]" width="800">

1. Choose a team, character loadout, or starting modifier.
2. Enter a side-scrolling combat room.
3. explore and clear enemies.
4. Pick a reward, potential, stat boost, or effect upgrade.
5. Continue through more rooms, events, elites, and bosses.
6. Win the run or fail, then return to the run start/hub flow.

### Moment-To-Moment Combat Loop

The active combat rhythm is:

1. Approach enemies and basic attack.
2. Use a character skill or ultimate when ready.
3. Swap to a buffer/support character.
4. Apply a buff, debuff, shield, heal, or setup effect.
5. Swap back to the DPS character.
6. Spend burst damage.
7. Dodge hazards and repeat.

This is similar to a simplified Genshin-style rotation:

**DPS -> spend skill/ult -> swap buffer/support -> apply buff -> swap DPS -> burst -> swap back -> repeat**

### Progression Cadence

- Run Scaling: Enemy health, speed, and aggression scale over time.
- Level Exploration: Explore more rooms as you progress into game and beat the new and more challenging bosses.
- Level Selection : Player can Choose Normal or Nightmare mode to exponential the difficulty and more punishing to player.

### Failure / Recovery Pattern

Players should fail because they mistimed dodges, stood in boss patterns, chose risky upgrades, or got overwhelmed by room pressure.

---

<!-- @tag:combat -->
## Combat Direction

### Team-Combat Rules

Team combat is **rotation-based**, not combo-driven.

Characters are tools in a simple loop:

- DPS stays active and deals most damage.
- Buffer/support swaps in briefly.
- Buffer/support spends skill or ultimate.
- DPS returns to use stronger damage.

**Role Classification**:

- **Main DPS**: High on-field presence, executes core combos and sustained loops.
- **Sub DPS**: Quick-swap burst, dumps Skills/Ultimates.
- **Support**: Defensive utility (shields/heals) and team-wide offensive buffs.

### Swap Rules

Allowed:

- **Team Swap Cancel** : When press Team Swap will clone another character into field with **Swap animation sequence** while the previous character will continue to execute their last action until end then disappear, the movement of the previous character will also be preserved and executed normally.
- **Team Swap Inheritance** : the new character will Inheritance the last Input buffer of previous character to continue chaining.
- **Optional** swap-in effect, such as small damage, shield, heal, buff, or particle burst depend on Character Passive or Artifact.

<!-- @tag:combat-scope -->
## Combat Scope and Removed Mechanics

### Active Combat Model

Neo-JJK combat is:

- Fast-paced
- Side-scrolling
- Hack-and-slash
- Skill cooldown based
- Ultimate burst based
- Team-rotation based
- VFX-heavy
- Simple to tune

### Removed Entirely

The following are not part of the active design:

- Fighting-game DNA as a system goal
- KOF / BlazBlue-style combo routing
- Frame data rules
- Frame advantage logic
- Strict cancel policy
- Link timing
- Air attack systems
- Plunge attack systems
- Real knock-up juggle systems
- Real knock-fall combo sessions
- Complex hit reaction trees
- Defensive/offensive stance micro loop
- ZZZ-like Perfect Swap
- Combo-driven character potential upgrades

### Hit Reaction Policy

Allowed:

- Hit-stop
- Short stagger
- Knockback
- Fake knock-up animation
- Fake knockdown animation
- Boss flinch VFX for feedback

Not allowed:

- Real juggle state machines
- Extended airborne combo rules
- Enemy launch physics as a core system
- Boss stagger-lock loops
- Combo counter requirements for damage

Hit reactions should be presentation-first. If the player sees a knock-up, it can simply be an animation/VFX trick rather than a deep gameplay state.

---
<!-- @tag:characters -->
### Character Kit Design Principles

Each character should stay small:

| Kit Part | Budget |
| --- | --- |
| Basic Attack | 2-3 simple states/hits maximum |
| Skill | 1 simple active skill |
| Ultimate | 1 flashy high-impact action |
| Passive | Simple stat/effect rule |
| Swap Effect | Optional, simple, non-precision |

Characters should feel different through:

- Range
- Cooldown
- Area shape
- Damage type
- Buff/debuff role
- VFX identity
- Simple status effects

Characters should not feel different through:

- Long combo lists
- Stance routing
- Air routes
- Frame advantage
- Character-specific cancel rules

### Combat Action System

<img src="https://wutheringlab.com/wp-content/uploads/2023/06/8.gif" alt="Wuthering Wave Combat]" width="800">
Every character has actions governed by distinct Wind-up (SF), Active (AF), and Recovery (RF) animation frames:

- **Basic Attack (BA)**: Simple Linear State Attack 2-3 State Hit like MMORPG. Fast and Loopable.
- **Skill**: Custom ability. Cancels BA. Cooldown-gated. A/D inputs steer/alter trajectory or form.
- **Ultimate (Ult)**: High-damage finisher. Cancels Skill. Requires the character's Aura Energy meter to reach the authored Ultimate cost.

All Action can be done on Ground or Air, The Player FSM should stay the same result.

Remove any Character Lock position/gravity on Action (Except Dash/Jump) to prevent character stuck loop.

### Basic Attack

- 2-3 hit loop maximum.
- Simple forward-facing hitboxes.
- Can be interrupted only if implementation remains simple.
- Damage and VFX matter more than animation depth.

### Skill

- One active skill per character.
- Cooldown-based.
- Should have obvious area, projectile, buff, shield, or movement purpose.
- Avoid alternate branches and strict timing.

### Ultimate

- Flashy burst action.
- High impact VFX.
- Aura Energy resource gate; Ultimate is not cooldown-gated.
- Each Ultimate authors its own Aura Energy cost. The character's maximum Aura Energy equals its highest authored Ultimate cost.
- Cooldown Reduction lowers the effective Aura Energy cost, capped at 50% cost reduction.
- Should be easy to trigger and understand.

### Aura Energy

- Aura Energy is stored separately for each team character.
- Aura Energy is granted only after an attack confirms damage on an enemy. Starting or missing an action grants nothing.
- Every damaging hit independently rolls the character's Aura Energy Rate: Low 5%, Medium 10%, or High 15%.
- A successful chance roll grants a base 10 Aura Energy, multiplied by the character's Aura Energy Multiplier.
- Chance-based grants are limited by a configurable number of grants within a short time window so dense multi-hit actions cannot refill the meter without limit.
- A combat action may author one guaranteed `AE Reward`. It is claimed once by the first damaging hit from that action, across melee, projectile, and background-Ultimate delivery.
- Defeating an enemy does not provide a separate Aura Energy reward.

### Damage Resolution

- Combat actions may author a total action damage multiplier. Each hitbox uses a clamped 0-100% ratio of that total budget.
- Legacy actions without a total damage budget continue to use each hitbox ratio directly.
- Physical primary damage uses the Normal routine and Damage Mastery.
- Non-physical primary damage uses the Elemental routine and Elemental Multiplier instead of Damage Mastery.
- Skill and Ultimate primary damage also use Skill Mastery.
- Normal and Elemental primary damage can critically hit.
- Additional Damage from passives or artifacts is resolved as a separate damage event and cannot critically hit.
- Melee hitboxes, projectiles, and background Ultimates use the same per-enemy damage and Aura Energy rules.

### Dodge / Dash

- Simple survival verb.
- Can include short invulnerability if easy to implement and tune.
- Should be readable and responsive.

### Swap

- Changes active character.
- May have cooldown if needed.
- May trigger a simple swap effect.
- Does not create perfect-swap gameplay.

### Buff / Debuff

- Use simple timers and stat modifiers.
- Avoid stacking rules that need spreadsheets.
- Make UI feedback readable.

### Character Data

#### Character Stats

<img src="https://upload-os-bbs.hoyolab.com/upload/2025/01/28/13693861/d7cf74b844c6ff690f37810e2610c852_2399325505013166081.jpg?x-oss-process=image%2Fresize%2Cs_1000%2Fauto-orient%2C0%2Finterlace%2C1%2Fformat%2Cwebp%2Fquality%2Cq_70" alt="Wuthering Wave Combat]" width="800">

| Stats | Value | Description |
| --- | --- | --- |
| ATK | 10 | Attack |
| DEF | 10 | Defense |
| HP | 100 | Health |
|SPD | X | Speed |
| CDR | XX% | Cooldown Reduction Mastery |
| CRIT | XX% | Critical Rate |
| CRIT Dmg | XX% | Critical Damage |
|DMG Mastery| XX% | Damage Mastery |
|DMG Res| XX% | Damage Resistance |
| Elemental Multiplier | X.XX | Multiplier used by non-physical Elemental damage instead of Damage Mastery |
| Skill Mastery | XX% | Bonus applied to Skill and Ultimate primary damage |
| Aura Energy Multiplier | X.XX | Multiplier applied to chance-based Aura Energy gains |

Artifact bonuses are added to a character's base stats when the team is deployed. Runtime HP, movement speed, damage, cooldown calculations, Aura Energy, HUD values, projectiles, and background Ultimates read the resolved runtime stats.

---

<!-- @tag:progression -->
## Rogue-Lite Progression

### Soul Shop System

- **Shop Lore** : a opportunist devil that collect a dead soul on Cursed Realm and sell for high price.
- **Shop Rule** : The Soul Shop act as a safe haven for players. It is the only place where players can change, replace, or purchase their loadout (Character/Artifact) before venturing into the next Cursed Realm.  
- **Shop Algorithm**: The shop algorithm will use the pool of character, artifact that the player have unlocked and the pool of enemy in the Cursed Realm that the player have defeated or explored. it never present what you never seen.
- **Shop Reroll**: Player can Reroll shop Item but the new Item price will overpriced increase every time you reroll.
- **Duplicate Progression Target**: Reacquiring an owned Character advances its
  permanent Potential path. Reacquiring an owned Artifact during a run converts
  the duplicate into that Artifact's Fragment for Transcendence.
- **Current Implementation Note**: One-time ownership filtering still treats
  owned Characters and Artifacts as sold out. Duplicate conversion is not
  implemented yet. Consumable Items remain repeatable purchases.

### Team Builder Loadout Flow

- The Team Builder supports up to three character slots, with three Artifact slots per character.
- Owned Characters and Artifacts are shown in separate inventory grids.
- Artifacts can be dragged from the grid into a slot, moved between characters, swapped with an occupied slot, or removed by dragging them outside a valid slot.
- One Artifact instance cannot be equipped in more than one slot at the same time.
- Character and Inspect screens show base stats plus positive or negative Artifact bonuses.
- Team edits are a session-only draft. Opening a fresh Team Builder session loads the current runtime team, active slot, and equipped Artifacts.
- Switching between Team Builder panels preserves the in-session draft.
- `Deploy` opens a confirmation dialog. Confirming is the only action that writes the staged characters, active slot, and Artifacts to the runtime team.
- `Cancel` restores the runtime loadout. Closing the Team UI clears undeployed draft changes.
- In Combat Zone mode, loadout editing is locked while inspection remains available.

### Utility Resource

| Entity | Description | Rogue-lite Reset |
| --- | --- | --- |
|Currency | The In-Game Utility Currency for **Soul Shop** | Lose |
| Soul Fragment | The Currency of the game can only acquire by defeating the Elite/Boss enemy on the Cursed Realm Run. | Retain |
| Character | The Characters that can use in the game. | Lose but retain the upgrade |
| Artifact | Equipment assigned to character loadouts. | Lose on run failure; permanent Transcendence is retained. Current implementation still retains ownership. |
| EXP Potion | Consumable Character EXP used only from Character Inspector. | Lose |
| Artifact Fragment | Artifact-specific duplicate material used for Transcendence. | Persistence on run loss is TBD |
| Items | The consumable items that can use during the Cursed Realm runs. | Lose |

P.S Lose but retain Upgrade mean you lose that Item/Character/Artifact if you die in Cursed Realm Run but if you able to buy again, they will come with latest of your unlocked upgrade(e.g Instead of new Fresh Potential Lv.0 they come with Potential Lv.5 and Artifacts also the same rule.)

### Character Level System

Character Level is run-scoped growth for each acquired character. It is
separate from permanent Character Potential.

- Every character starts each run at **Lv.1**.
- A character also starts with the permanent Potential rank the player has
  already unlocked for that character.
- Character Level, current EXP, and unspent EXP Potions are removed when the
  run is lost or reset.
- The maximum Character Level is **Lv.30/30**.
- Characters gain EXP only from **EXP Potions**. Defeating monsters does not
  directly grant EXP.
- EXP Potions can be obtained from the **Soul Shop** and **Room Clear Rewards**.
- EXP Potions are consumed only through the Character Level System UI.
- The Character Level System UI is accessible only through the Character
  Inspector UI.
- When current EXP reaches or exceeds the current level EXP cap, the character
  levels up. Excess EXP carries into the next level.
- Leveling increases only the character's basic **ATK, DEF, and Max HP**.
- EXP curves, potion values, per-level stat growth, and level-up costs require
  a separate balance specification before implementation.
- Runtime Character Level and EXP belong to the run save and TeamLoader runtime
  state. Permanent Potential belongs to permanent progression data.

### Character Potential Rule

![Character Potential Rule](https://static.wikia.nocookie.net/gensin-impact/images/3/3f/Constellation_Menu.png/revision/latest?cb=20220411014019)

#### Permanent Potential Upgrade

- it represent a system like Constellation or Aetherium Core in Honkai Star Rail. this will use to permanent upgrade that character. Linear path no seperation, each upgrade will always benefit to the character in a different way.
- Potential upgrade only acquired when you buy that character from Soul Shop again, the price will increase every time you upgrade.
- Max upgrade is 1/6 step

#### Potential can

- Increase stats
- Increase effect strength
- Reduce cooldown
- Increase skill area
- Add a simple projectile
- Add a simple status chance
- Improve shield/heal/buff value
- Improve ultimate charge or damage

#### Potential must not

- Add new combo routes
- Add new cancel rules
- Add new air actions
- Add new input strings
- Change the character into a complex fighting-game kit

### Upgrade Design Rules

Good upgrade examples:

- Basic attack damage +15%
- Skill cooldown -10%
- Ultimate damage +20%
- Dash leaves a small damage trail
- Buff duration +2 seconds
- Skill area becomes wider
- First hit after swap deals bonus damage
- Gain shield when using ultimate

Bad upgrade examples:

- Unlock an extra launcher branch
- Enable jump cancel after hit 2
- Add a perfect-swap counter route
- Add frame advantage bonuses
- Add combo-only damage scaling
- Require a strict input sequence

### Reward Categories

| Reward Type | Role | Intended Effect |
| --- | --- | --- |
| Stat Boost | Simple power growth | Increase damage, health, crit, defense, speed, or cooldown efficiency. |
| Skill Modifier | Light build identity | Change area, projectile count, duration, or status chance. |
| Team Modifier | Rotation support | Improve buffs, swap effects, energy gain, or support uptime. |
| Survival Modifier | Forgiveness | Add shield, heal, damage reduction, revive, or hazard resistance. |
| Economy Modifier | Run pacing | Increase currency, reward choices, rerolls, or upgrade quality. |

---

<!-- @tag:items -->
## Canonical Gameplay Content - Items and Upgrades

Items should be understandable in one sentence. Avoid hidden formulas and combo conditions.

### Charm Types

| Item / Upgrade Type | Role | Intended Effect |
| --- | --- | --- |
| Damage Charm | DPS scaling | Increase basic, skill, or ultimate damage. |
| Cooldown Charm | Skill uptime | Reduce cooldowns or refund cooldown after room clear. |
| Shield Charm | Survival | Give shield on swap, skill use, or boss phase transition. |
| Burst Charm | Ultimate support | Increase ultimate damage or energy gain. |
| Status Charm | Simple synergy | Add burn, slow, bleed, curse, or other straightforward status effects. |
| Reward Charm | Rogue-lite pacing | Add rerolls, extra choices, or increased currency. |

### Cursed Realm Artifacts

Artifacts are equipment that can be assigned to a character before deployment.
Each character has three Artifact slots. Equipped Artifact `bonusStats` are
added to that character's runtime stats. Artifact data can also author separate
Additional Damage effects, although the four currently authored Artifacts do
not contain an Additional Damage effect.

Passive descriptions are currently display text unless the behavior is also represented by implemented stat bonuses or an authored runtime effect.

### Artifact Transcendence

Artifact Transcendence is the permanent Artifact upgrade system.

- Each Artifact has a maximum Transcendence Level of **+5/5**.
- When the player obtains a duplicate of an Artifact they already own during a
  run, the duplicate converts into that Artifact's specific Fragment.
- Artifact Fragments are spent only in the Artifact Transcendence System UI.
- The Artifact Transcendence System UI is accessible only through **Character
  Inspector UI -> Artifact Section**.
- Transcendence improves the Artifact's numeric stats and its authored passive
  effect.
- Transcendence Level is permanent across runs and must be restored when the
  Artifact is reacquired.
- Runtime equipped-Artifact data must include both the Artifact identity and
  its permanent Transcendence Level.
- Fragment costs, duplicate conversion quantity, fragment behavior on run loss,
  stat scaling, and passive scaling require a separate balance specification
  before implementation.

| Implemented Artifact | Price | Implemented Runtime Bonuses | Displayed Passive Description |
| --- | ---: | --- | --- |
| **Blind's Boundless Ribbon** | 2000 | +1 SPD, +10 CDR, +0.15 Aura Energy Multiplier | Damage resistance and swap-heal text are display-only. |
| **Fiend-Flesh Mask** | 650 | +15% ATK, -5% DEF | Defeat-stack and dark-slash text are display-only. |
| **Soul-Merchant's Scales** | 1200 | +10% DMG Mastery | Soul Fragment and reroll-discount text are display-only. |
| **Broken Chrono-Relic** | 950 | +15% CRIT Damage | Room-entry ATK decay and cooldown-refund text are display-only. |

#### Planned Artifact Concepts

These speculative Artifacts remain part of the design direction. They are not claims about current runtime implementation.

| Artifact Name | Type / Role | Stats | Passive Effect | Description | Lore / World Setting |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Blind's Boundless Ribbon** | Support / Healing | +10% CDR, +10% SPD | **Spirit Guide:** Active characters receive 15% damage resistance. Swapping leaves a trail of energy healing the incoming character for 8% of their missing HP. | Stabilizes team health during rotations, reflecting Blind's protective yet eerie guidance. | *A strip of black silk from Blind's blindfold. "Follow my thread, or lose yourself to the fog."* |
| **Fiend-Flesh Mask** | Damage / Risk | +15% ATK, -5% DEF | **Monstrous Hunger:** Defeating an enemy increases DMG Mastery by 2% for 6s (up to 5 stacks). Max stacks release a small dark slash that staggers trash enemies. | High-impact offensive mask that rewards aggressive clearing but lowers defense. | *Carved from a freshly slain Fiend, who was once one of the 50 players left behind in the death game.* |
| **Soul-Merchant's Scales** | Economy / Status | +10% DMG Mastery, +15% Item Drop Rate | **Toll of the Dead:** Defeating Elites/Bosses yields 25% more Soul Fragments; rerolling the Soul Shop costs 10% less in-run currency. | Economy-boosting charm that allows players to upgrade character potentials faster. | *Used by the opportunist devil of the Soul Shop. "Everything has a price here, kid... even memories."* |
| **Broken Chrono-Relic** | Time Pressure | +15% CRIT Dmg | **Time Limit:** Gain +20% ATK on entering a room, decaying by 1% every 2s. Clearing before decay refunds 20% Ultimate cooldown. | Promotes rapid room clearing under the Cursed Realm's core "time limit" pressure. | *In the Cursed Realm, time is your ultimate executioner. Run faster, kill faster, or forget your name.* |
| **Aura-Resonating Shard** | Rotation / CC | +15% DMG Mastery | **Synergy Wave:** Swapping characters triggers an Aura burst dealing 100% ATK as damage and knocking back nearby enemies. | Directly leverages team-swap mechanics to turn character swaps into offensive tools. | *Clashing Auras from player swaps release shockwaves of raw fighting spirit that Fiends fear.* |
| **Rival's Broken Crest** | Boss Counter / Ult | +10% CRIT Rate, +10% ATK | **Cruel Competition:** Deal 20% more damage to Bosses. Defeating a boss instantly and fully charges the active character's Ultimate. | Tailored for boss encounters, turning rival players into ultimate battery sources. | *Only 1 of 50 players can return to the living world. If you do not break their crest, they will shatter yours.* |
| **Shroud of Deprivation** | Utility / CC Immunity | +15% DEF, +10% DMG Res | **Blind Faith:** Swapped-in characters are immune to crowd control effects (slow, freeze, stagger) for 3s. | Guarantees safe setup and execution of skills/ultimates upon entry. | *A dark wrap that blocks out sight. "The horrors of this place are easier to fight when you shut out the terror of seeing them."* |
| **Memory-Draining Urn** | Survival / Shield | +15% Max HP | **Amnesia Shield:** Landing 10 Basic Attacks grants a Shield equal to 8% Max HP (12s CD) which explodes for physical damage when broken/expired. | Converts sustained combat into defensive shielding and passive area damage. | *Urn catching fragments of forgotten pasts leaked during death and respawn, hardening them into armor.* |
| **Shin's Rusty Dagger** | Crisis / Low-HP | +10% ATK, +5% SPD | **Desperate Struggle:** Deal up to 30% more damage based on missing HP (scaling linearly below 50% HP). | High-risk, high-reward crisis multiplier that helps survive close-call encounters. | *Shin's starting blade. Useless against giant Fiends, yet his refusal to let go gave birth to his Aura.* |
| **Aura-Charged Shackle** | Cooldown / Rotation | +12% CDR, +10% Max HP | **Rebirth Chain:** Using an Ultimate reduces the skill cooldowns of all off-field characters by 4s. | Keeps off-field support skills running, accelerating team rotation setups. | *A heavy shackle chaining the team's souls. No matter how many deaths, it drags them back to start.* |
| **Blind's Whispering Bell** | Utility / Stealth | +10% CDR, +5% SPD | **Spirit Shroud:** Swapping in grants 2.5s stealth. Using a Skill/Ultimate breaks stealth early but deals +25% damage. | Grants temporary safety upon swap-in for tactical positioning. | *A silent bell. "Silence your Aura, Shin. If they cannot sense your spirit, they cannot target your heart."* |
| **Gravely-Wounded Band** | Risk / Life Steal | +20% CRIT Dmg, -10% Max HP | **Last Stand:** When HP falls below 30%, gain +25% CRIT Rate and +15% Life Steal on all Basic Attacks. | High-risk survival accessory that converts near-death situations into offensive comebacks. | *Bloodstained band of a player who fought with fractured limbs, stealing strength from Fiends' flesh.* |
| **Fiendish Tendril** | Crowd Control | +12% DMG Mastery, +5% DEF | **Vortex Grasp:** Landing a Skill pulls trash enemies toward the target point and inflicts 'Decay' (reducing enemy damage by 15% for 4s). | Adds crowd-control grouping to characters' skills, setting up targets for DPS burst. | *A writhing appendage harvested from a giant Fiend, twitching in response to living Aura.* |
| **Coin of the False Winner** | Economy / Risk | +25% Currency Gain, -15% DMG Res | **Double or Nothing:** Clearing a room without taking damage doubles currency. Taking damage reduces it by 50%. | High-stakes economy charm for players confident in their movement and dodging. | *Minted by the Soul Shop's devil. "Play perfectly, or pay the toll," shown by the crown and skull.* |
| **Shin's Cracked Hourglass** | Cooldown / Trigger | +15% CDR | **Temporal Paradox:** Every 30s, the next Skill used has its cooldown instantly reset. | Allows double-casting of character skills, adding massive burst potential to rotation loops. | *Shin watched the sand slip away, knowing delay meant forgetting. Smashed to bend time to his urgency.* |
| **Phantasmal Mirror** | Rotation / Clone | +10% ATK, +10% SPD | **Doppelganger:** Swapping leaves a spectral clone of the outgoing character that executes one final Basic Attack dealing 50% damage. | Enhances team rotation by maintaining a brief offensive presence from swapped-out characters. | *A mirror reflecting your Aura. In the Cursed Realm, your shadow is the only ally that won't betray you.* |
| **Crown of the Usurper** | Ultimate / Risk | +30% Ultimate Damage, -10% Max HP | **Blood Tithe:** Using an Ultimate consumes 10% current HP but grants +20% Ultimate CRIT Rate. | Sacrifices health to guarantee critical hits on high-impact ultimate animations. | *Worn by the 'King' of the 50 rivals who drained his team to power an ultimate, dying at the lobby gates.* |
| **Tether of the Damned** | Defensive / Support | +15% DEF, +10% DMG Res | **Shared Burden:** When an off-field character takes damage (e.g., from traps), the active character gains a Shield equal to 12% Max HP. | Mitigates environmental and trap damage by protecting the active character. | *A leash of Aura binding the party's souls. In this death game, your teammates are your shield.* |
| **Blind's Piercing Sight** | Precision / Flanking | +15% CRIT Rate | **Backstab:** Attacking enemies from behind increases CRIT Damage by 35% and ignores 15% DEF. | Rewards tactical flanking and positioning, especially during boss recovery windows. | *Blind senses weaknesses in Aura. "They have a blind spot, Shin. Strike there, and make it quick."* |
| **Soul-Eater's Goblet** | Survival / Healing | +15% Max HP | **Reap:** Defeating an Elite restores 15% active Max HP; defeating a Boss fully restores the entire team's HP. | Reliable sustain tool for long runs, ensuring recovery from high-threat encounters. | *A dark cup drinking the spilling Aura of dying rivals. Keeps the user's memories fresh and sweet.* |

### Current Implementation Replacements and Gaps

| Previous or Planned Behavior | Current Game State |
| --- | --- |
| Ultimate cooldown or generic resource gate | Replaced by per-character Aura Energy. Maximum capacity is derived from the authored Ultimate cost. |
| Aura Energy granted when an action starts, misses, or defeats an enemy | Replaced by confirmed-damage rewards: a per-hit chance reward plus one optional guaranteed action reward. |
| Independent hitbox damage values for multi-hit actions | Replaced by an optional total action damage budget distributed through per-hit ratios. |
| One shared damage formula | Replaced by Normal, Elemental, and non-critical Additional Damage routines. |
| Character base stats used directly during combat | Replaced by per-team-slot runtime stats built from the character plus equipped Artifact bonuses. |
| Team Builder edits immediately changing runtime state | Replaced by a session draft. Only confirmed Deploy writes the loadout to `TeamLoader`. |
| Character Level and EXP Potion progression | Target design documented; runtime level state, EXP curves, potion inventory, UI, and reset flow are not implemented yet. |
| Artifact Transcendence and Artifact Fragments | Target design documented; permanent levels, fragment conversion, upgrade costs, passive scaling, and save data are not implemented yet. |
| Artifact passive descriptions automatically executing | Not implemented generically. Only numeric `bonusStats` and explicitly authored Additional Damage effects affect combat. |
| The four displayed Artifact passive effects | Not authored as runtime effects in the current Artifact assets. |
| Finding Artifacts during Cursed Realm runs and losing them on run failure | Not implemented by this branch; current code covers Soul Shop ownership, loadout management, and deployment. |

---

<!-- @tag:enemies -->
## Canonical Gameplay Content - Enemies and Threats

### Trash Enemy

**Role:** Basic target for fast room-clearing.

**Design intent**

- Dies quickly.
- Teaches attack range and crowd control.
- Can stagger or knock back lightly.

**Counterplay**

- Basic attacks, skill area damage, and simple dodging.

### Ranged Enemy

**Role:** Forces movement and target priority.

**Design intent**

- Shoots readable projectiles.
- Stays behind melee enemies when possible.
- Low health, moderate danger.

**Counterplay**

- Dash through projectiles, close distance, or clear with area skills.

### Shield / Guard Enemy

**Role:** Slows down button-mashing without requiring complex counters.

**Design intent**

- Blocks or reduces frontal damage.
- Vulnerable after attacking or from behind.
- Should not require parry systems.

**Counterplay**

- Dodge around, use skill area damage, or swap into burst.

### Elite Enemy

**Role:** Adds mini-boss pressure inside rooms.

**Design intent**

- Has 2-3 clear attacks.
- Uses larger telegraphs than trash enemies.
- Drops stronger rewards.

**Counterplay**

- Respect telegraphs, dodge, burst during recovery.

### Hazard / Projectile Pattern

**Role:** Adds rogue-lite room pressure.

**Design intent**

- Creates simple Touhou-lite avoidance moments.
- Uses clear danger indicators.
- Does not require pixel-perfect dodging.

**Counterplay**

- Move, dash, reposition, kill the source.

---

<!-- @tag:bosses -->
## Boss Battle Direction

### Boss Fantasy

Bosses are no longer traditional fighting-game opponents.

They should feel closer to **generic 2D action bosses**:

- Large body
- Strong silhouettes
- Clear attack tells
- Big area attacks
- Bullet/projectile patterns
- Phase changes
- Short punish windows
- High VFX spectacle

Hollow Knight is a better boss reference than KOF, BlazBlue, or ZZZ.

### Boss Rules

Bosses should:

- Resist stagger-lock.
- Ignore real juggle logic.
- Use large readable AOE attacks.
- Use projectile or pattern pressure.
- Force dodging and repositioning.
- Give obvious openings after major attacks.

Bosses should not:

- Be combo dummies.
- Be thrown or grabbed.
- Be launched into extended air combos.
- Require perfect swap reactions.
- Require frame-trap knowledge.
- Depend on player combo optimization to be fun.

### Boss Encounter Loop

1. Boss telegraphs.
2. Player dodges or repositions.
3. Boss recovery creates a damage window.
4. Player uses DPS, support buff, skill, ultimate, or swap burst.
5. Boss changes pattern or phase.
6. Repeat with increasing hazard density.

---

<!-- @tag:inspirations -->
## Inspirations and Design Boundaries

### Primary Inspirations

- **Dungeon Slasher** - fast side-scrolling action, room pressure, simple combat readability.
- **Oblivion Override** - action rogue-lite pacing and quick build growth.
- **Skul: The Hero Slayer** - character identity through small kits and run upgrades.
- **Genshin Impact** - simplified team rotation model: DPS, support, buff, burst, repeat.
- **Hollow Knight** - boss readability, telegraphs, AOE patterns, punish windows.

### What To Borrow

- Fast room-clearing.
- Simple attack verbs.
- Upgrade choices that are easy to understand.
- Bosses with clear tells and pattern pressure.
- Team rotation based on skill cooldowns and buffs.
- Big VFX that makes simple attacks feel good.

---

<!-- @tag:tech-stack -->
## Project Stack & Architecture

### Technical Stack

| Area | Value |
|------|-------|
| Engine | Unity 6 (6000.x LTS) |
| Language | C# (.NET Standard 2.1) |
| Render Pipeline | URP 17.x |
| Dimension | 2D side-scrolling |
| Input | Unity Input System 1.8.x (`PlayerInput` component) |
| Camera | Cinemachine 3.x (explore / combat priority switching) |
| UI | Unity UI Canvas + TextMeshPro 4.x |
| Physics | Physics2D (`Rigidbody2D`, triggers, `OverlapCircle`) |
| VFX | VFX Graph 17.x + Shader Graph 17.x |
| Level Tools | ProBuilder 6.x |
| AI | Unity Behavior Graph (enemy FSM nodes) |
| Networking | None — single-player |
| Version Control | Git + GitHub |
| CI | GitHub Actions (stub) |
| Testing | Unity Test Framework 1.x (0 % coverage) |

### Architecture Pattern

**Custom FSM + ScriptableObject Data + Frame-Data Combat Engine (under simplification)**

| Layer | Responsibility |
|-------|---------------|
| `PlayerCharacter` (Facade) | Owns active `CharacterObject`; caches movement, combat, animation, audio, passive, FSM |
| `PlayerStateMachine` (FSM) | Priority-based state transitions with 12-frame input buffer |
| `FrameDataPlayerState` | Phase lifecycle — being simplified to wind-up / active / recovery only |
| `PlayerCombat` (Executor) | Hitbox activation, projectiles, VFX, audio — adapting grab/combo-tree code |
| `CharacterObject` (SO) | Primary data container: stats, combos, skills, movement, audio, progression |
| `ActionResolver` | Combo tree walker — being replaced with simple linear BA chain |
| `EnemyController` + Behavior Graph | Enemy FSM via Unity Behavior nodes |
| `TeamLoader` | Runtime party of up to 3 characters with HP tracking |

### Codebase Scale

| Subsystem | Scripts | Role |
|-----------|--------:|------|
| AIBehaviour | 2 | Enemy controller + hit reaction |
| Animation | 3 | Player / enemy animation adapters |
| Behavior | 23 | Unity Behavior tree nodes |
| Cameras | 1 | Cinemachine coordinator |
| Character | 23 | SO data: frame data, combo trees, stats, progression |
| Combat | 50 | Hitboxes, projectiles, movement, input, managers |
| Editor | 9 | Custom authoring windows |
| Inventory | 5 | Item / codex SO data |
| Parallax | 3 | Background scrolling |
| Player | 24 | FSM + states + input buffer |
| Shop | 6 | Shop inventory + runtime + UI |
| Systems | 4 | PlayerCharacter facade, TeamLoader |
| TeamBuilder | 5 | Team composition UI |
| UI | 10 | HUD, skill tree, world UI, debug |
| **Total** | **168** | |

### Key ScriptableObjects

| SO Type | Purpose |
|---------|--------|
| `CharacterObject` | Character data: stats, combos, skills, movement, audio, progression |
| `ActionResolverTable` | Combo resolution config (being simplified) |
| `AttackStepData` | Single attack step: frame timing + hit / motion / VFX / audio blocks |
| `ComboTree` / `ComboNode` | Linked-list combo routing (being replaced with linear chains) |
| `MovementProfile` | Movement tuning: speed, jump, gravity |
| `CharacterProgressionDefinition` | Skill tree node definitions |
| `CodexInventory` | Master item catalog |
| `ReactionRuleTable` | Enemy hit reaction rules (being simplified) |
| `ProjectileData` | Projectile configuration |

### Folder Convention

```text
Assets/Project/Scripts/
├── AIBehaviour/     # EnemyController, EnemyHitReactionHandler
├── Animation/       # PlayerAnimator, AnimationHandler_Enemy
├── Behavior/        # Unity Behavior tree nodes
│   ├── Actions/     # Chase, Patrol, Attack, Detect
│   ├── Conditions/  # IsAlive, HasTarget, CanAttack
│   └── Reactions/   # TakeDamage, Die
├── Cameras/         # CameraManager (Cinemachine)
├── Character/       # All CharacterObject SO definitions
├── Combat/          # PlayerCombat, HitBox*, Projectile*, VFX, Audio, Freeze
├── Editor/          # Authoring windows
├── Inventory/       # BaseItem, CharacterItem, Artifact, CodexInventory
├── Parallax/        # ParallaxController, Background, Layer
├── Player/          # PlayerStateMachine, all PlayerState subclasses
├── Shop/            # ShopManager, PlayerShopInventory, ShopUI
├── Systems/         # PlayerCharacter, TeamLoader, EntitySortingManager
├── TeamBuilder/     # TeamBuilderUI, slots, grid
└── UI/              # PlayerHUD, SkillTreeUI, EnemyWorldUI, InputLockManager
```

### Implementation Bias

- Data-driven character definitions via ScriptableObject.
- Cooldown timers over complex combo states.
- Stat modifier lists over custom upgrade logic.
- Simple enemy state machines via Behavior Graph.
- Clear VFX hooks through existing feedback pipeline.
- Minimal inheritance, minimal global manager growth.

---

<!-- @tag:prototype-truth -->
## Current Prototype Truth

> Inspected via architecture review (2025-06-02) — 168 scripts under `Assets/Project/Scripts`.

### Runtime Shape

| Subsystem | State | Neo-JJK Action |
|-----------|-------|----------------|
| PlayerStateMachine (24 scripts) | Working FSM, 12 states, priority transitions, input buffer | **Keep** — adapt air-attack, retain grab states for future use |
| PlayerCombat (50 scripts) | Frame-data engine, combo trees, hitboxes, projectiles, grab | **Refactor** — adapt ActionResolver and simplify combos, retain grabs |
| CharacterObject SOs (23 scripts) | Rich data: combos, skills, ultimates, stats, progression | **Simplify** — reduce to 2-3 hit BA, 1 skill, 1 ultimate per character |
| Enemy AI (25 scripts) | Unity Behavior Graph: patrol, chase, attack, react, die | **Keep** — good foundation for simple enemy FSM |
| Team System (9 scripts) | TeamLoader manages 3-char party, HP tracking, swap | **Keep** — simplify to instant swap, remove swap-cancel |
| Shop / Inventory (11 scripts) | Full shop, codex inventory, character items | **Keep** — aligns with Soul Shop design |
| Camera (1 script, 871 lines) | Cinemachine explore / combat switching, shake, framing | **Keep** — reduce combat camera complexity |
| UI (10 scripts) | PlayerHUD, SkillTreeUI, EnemyWorldUI, TeamSwitchHUD | **Keep** — update for simplified combat |
| Parallax (3 scripts) | Background parallax scrolling | **Keep** — fits 2D side-scrolling |
| Editor Tools (9 scripts) | Animation editor, projectile editor, action tree controller | **Keep** — retain good infrastructure, adapt logic for simpler needs |

### Fighting-Game DNA — Removal Targets

| System | Scripts | Why Remove |
|--------|---------|------------|
| Branching Combo Trees | `ComboNode`, `ComboRoute` | Neo uses 2-3 hit BA loops (adapt ActionResolver logic instead of removing) |
| Frame-data precision | `FrameDataPlayerState`, `ActionFrameData` | Neo uses simple phases, not 60 fps tick logic |
| Grab / throw sessions | `ThrowContext`, grab logic in `HitBoxDamage` | Retained as useful tool for future development (do not strip) |
| Complex air routing | `PlayerAirAttackState` | Shift to FSM ground <-> air overriding and simple gravity policy |
| Combo conditions | `DirectionActionCondition`, `GroundedActionCondition` | No conditional branching |
| Complex hit reactions | `ReactionResolver`, `ReactionRuleTable` | Neo uses hit-stop + knockback only |
| Trigger windows | `TriggerWindow` in combo routing | No timing-window mechanics |

### Prototype Debt

- `PlayerCombat.cs` (48 KB) — needs decomposition into simple attack executor.
- `CombatPlayerState.cs` (32 KB) — frame-data shared logic needs simplification.
- `CameraManager.cs` (871 lines) — over-featured for Neo needs.
- Filename / class mismatches: `PlayerInputHandler.cs` → `InputHandler_Player`, `AnimetionEventRelay.cs` → `AnimationEventRelay`.
- Debug shortcuts (number keys) bypass skill progression in `PlayerInputHandler.cs`.
- Static `InputLockManager` can permanently lock input on panel close failure.
- 0 % test coverage.

### What Works Well

- ScriptableObject-driven character data pipeline.
- Unity Behavior Graph for enemy AI.
- Cinemachine camera integration.
- Team loading and character switching.
- Shop / inventory with ownership tracking.
- Input System integration (modern API).
- VFX / audio feedback pipeline.

---

<!-- @tag:scene-roles -->
## Scene Flow

**MainMenu → Lobby → In-Game (CombatRoom ↔ RewardRoom → BossRoom) → Result → Lobby**

| Scene | Purpose |
|-------|---------|
| `MainMenu` | Title screen, settings, save slot |
| `Lobby` | Walkable hub — Soul Shop, team builder, run prep (Blazblue Entropy Effect style) |
| `CombatRoom` | Side-scrolling room-clear with enemy waves |
| `RewardRoom` | Upgrade choice, heal, event, or shop encounter |
| `BossRoom` | Pattern-based boss fight |
| `Result` | Run win / fail summary, return to lobby |

---

<!-- @tag:system-ownership -->
## Canonical Future System Ownership

| System | Ownership |
| --- | --- |
| `PlayerController` | Movement, dodge, active character routing, player input. |
| `CharacterRuntime` | Current character stats, cooldowns, skill/ultimate use, active buffs. |
| `TeamController` | Character swap, team roster, shared team resources if any. |
| `AttackResolver` | Hit detection, damage application, hit-stop, simple knockback/stagger. |
| `UpgradeSystem` | Run rewards, potentials, stat/effect modifiers. |
| `EnemyController` | Enemy movement, attacks, health, simple state behavior. |
| `BossController` | Boss phases, telegraphs, AOE/projectile pattern sequencing. |
| `RoomFlowController` | Room start, spawn waves, clear condition, reward transition. |
| `VfxAudioPresenter` | Combat feedback, impact effects, skill/ultimate presentation. |
| `UICombatHud` | Health, cooldown, ultimate, swap, reward choices. |

### Hard Rule

Do not rebuild fighting-game architecture unless a human explicitly changes the design direction again.

Do not dump all future gameplay into one global script just because it is faster today. Keep systems small, but do not over-engineer them.

---

<!-- @tag:architecture -->
## Target Architecture Direction

### Architecture Principles

1. **Simple combat verbs first**
   Build basic attack, skill, ultimate, dodge, and swap before adding any extra mechanics.

2. **Data-driven enough, not data-driven forever**
   Character stats, cooldowns, VFX references, enemy stats, and upgrades should be authorable data when practical. Do not build a giant toolchain before the game loop works.

3. **Presentation reacts to gameplay**
   VFX, SFX, animation, camera shake, damage numbers, and hit-stop should respond to simple gameplay events.

4. **Cooldowns and modifiers over combos**
   Complexity should live in stats and simple effects, not in input timing or combo state.

5. **Readable boss patterns**
   Boss logic should be pattern/phase based, not fighting-game AI based.

6. **Amateur-friendly maintenance**
   A future tired developer should understand a feature quickly.

### Data-Driven Content Targets

- `CharacterDefinition` - health, movement, attack data, skill, ultimate, role, VFX/audio references.
- `SkillDefinition` - cooldown, damage, area, effect, status, VFX/audio.
- `UpgradeDefinition` - reward text, stat modifiers, simple triggered effects.
- `EnemyDefinition` - health, movement, attack list, reward value.
- `BossPatternDefinition` - telegraph, attack shape, projectile/area pattern, recovery.

### Avoid Overbuilding

Do not create:

- Full fighting-game frame data tables.
- Deep combo scripting languages.
- Advanced cancel graphs.
- Real-time character action editors.
- Custom physics juggle stacks.
- Perfect-swap state machines.

---

<!-- @tag:guardrails -->
## Contributor Guardrails

### Preserve These Non-Negotiables

- Neo-JJK is a **2D side-scrolling action hack-and-slash rogue-lite**.
- Combat is **rotation-based**, not combo-driven.
- Team swap is simple character swapping, not Perfect Swap.
- Character kits are small.
- Upgrades increase stats/effects, not moveset complexity.
- Bosses are pattern-based action bosses, not fighting-game opponents.
- Low development effort is a design requirement.

### You May Refactor Aggressively

- Old combo assumptions.
- Fighting-game terminology in future specs.
- Perfect-swap planning.
- Character potential that expands movesets.
- Complex defensive/offensive loop descriptions.

### Do Not Do These

> See `@tag:combat-scope` for the full removed-mechanics list. Summary:

- No frame-data rules, air / plunge combat, real juggle, complex cancel logic, ZZZ-like swap timing.
- No bosses as stagger-lockable combo targets.
- No upgrades requiring combo mastery.

### AI Safety Rule

Every future implementation must clearly separate:

- **Current implementation truth**, confirmed by inspected files.
- **Target design direction**, defined by this Neo GDD.

Do not hallucinate completed architecture, engine details, scene names, or implemented systems.

---

<!-- @tag:roadmap -->
## 6-Month Development Roadmap

> 12 × 2-week sprints. End-of-sprint = playable build + thesis note.
> Direction: Refactor fighting-game prototype → Neo-JJK hack-and-slash rogue-lite.

### Phase Overview

| Month | Phase | Focus |
|-------|-------|-------|
| 1 | **Strip & Stabilize** | Audit codebase, remove fighting-game DNA, stabilize simple combat |
| 2 | **Combat Core** | Lock simple BA / Skill / Ult / Dodge loop with one character |
| 3 | **Team & Swap** | Team rotation, simple swap, per-character cooldowns |
| 4 | **Rogue-Lite Loop** | Room flow, reward choices, Soul Shop, upgrades |
| 5 | **Boss & Content** | Pattern-based boss, enemy archetypes, content scaling |
| 6 | **Polish & Thesis** | VFX / audio polish, QA, performance, thesis write-up |

---

### Month 1 — Strip & Stabilize

#### Sprint 1: Audit + Architecture Cleanup (Weeks 1–2)

**Goal:** Map every fighting-game system, tag for removal.

- Fix filename / class mismatches (`PlayerInputHandler` ↔ `InputHandler_Player`, `AnimetionEventRelay` ↔ `AnimationEventRelay`)
- Gate debug shortcuts behind `#if UNITY_EDITOR`
- Fix `InputLockManager` — disposable lock handles
- Add Assembly Definitions per subsystem folder
- Create `_Deprecated/` folder for removed fighting-game scripts
- Document surviving vs. deprecated combat scripts

**Accept:** Project compiles with .asmdef. Debug inputs gated. Lock system safe.

#### Sprint 2: Simplify Combat & Retain Infrastructure (Weeks 3–4)

**Goal:** Retain Editor tools / ActionResolver, but adapt logic for simple Hack & Slash needs.

- Adapt `ActionResolver` — remove complex branching, support simple linear BA chain (2-3 hits)
- Implement FSM ground <-> air overriding and establish gravity policy
- Retain `AnimationEditor` and `ProjectileEditor` — leverage existing good player infrastructure
- Retain `ThrowContext` and grab hitbox logic in `HitBoxDamage` for potential future use
- Simplify `FrameDataPlayerState` — keep wind-up / active / recovery, drop 60 fps tick precision
- Simplify `ReactionResolver` — keep hit-stop + knockback only
- Remove `ComboRoute` branching, `TriggerWindow` timing
- Reduce `PlayerCombat.cs` and `CombatPlayerState.cs` file sizes where possible

**Accept:** Player has 2-3 hit BA loop, one skill, one ultimate. Editors and grabs are retained and functional. FSM overriding in place.

---

### Month 2 — Combat Core

#### Sprint 3: Simple Combat Loop (Weeks 5–6)

**Goal:** Lock core action verbs — BA, Skill, Ultimate, Dodge.

- BA: 2-3 hit forward-facing loop, fast, loopable
- Skill: 1 active per character, cooldown-gated, A / D steers direction
- Ultimate: flashy burst, gated by the character's full Aura Energy cost
- Dodge / Dash: i-frame dash, simple survival verb
- Hit detection: `OverlapCircle` / `OverlapBox`, `NonAlloc` variants
- Damage formula: `base × modifier × defense` (keep `DamageCalculator` pure)

**Accept:** One character fights one enemy. All 4 verbs responsive. No legacy routing.

#### Sprint 4: Game Feel + Feedback (Weeks 7–8)

**Goal:** Make simple combat feel impactful.

- Hit-stop on all landed hits (`GameFreezeManager` — already works)
- Camera shake tiered by attack weight (`CameraManager` — already works)
- Slow-mo on ultimate activation
- VFX: slash arcs, burst circles, impact flashes per attack
- SFX: punchy hit feedback, skill / ult charge-release sounds
- Damage numbers — world-space popup, crit colour
- Enemy stagger / knockback visual (animation only, no state machine)
- Screen flash on ultimate hit (`UltimateFlashOverlay` — already works)

**Accept:** Combat feels juicy. Every input → immediate visual + audio response.

---

### Month 3 — Team & Swap

#### Sprint 5: Team Rotation (Weeks 9–10)

**Goal:** 2-3 character team with simple swap.

- `TeamLoader`: manage 3-character party (simplify existing)
- Swap keys 1 / 2 / 3: instant character change
- Swap animation: clone-in new, old continues last action then fades
- Per-character resources: Skill cooldown and Ultimate Aura Energy are tracked independently
- Per-character HP via `TeamLoader` (already works)
- Input buffer inheritance: new character inherits previous buffer

**Accept:** Player swaps 3 characters mid-combat. Cooldowns per-character. Swap responsive.

#### Sprint 6: HUD + Team UI (Weeks 11–12)

**Goal:** Combat HUD showing team state.

- Active character HP bar + skill cooldown + ultimate charge
- Team portraits with HP and cooldown state
- Swap cooldown indicator (if applicable)
- Enemy HP bar (world-space, `EnemyWorldUI` — already works)
- Damage feedback / combo counter (minimal)
- Pause menu with settings

**Accept:** All combat info visible at a glance. HUD clean and readable.

---

### Month 4 — Rogue-Lite Loop

#### Sprint 7: Room Flow + Scene Pipeline (Weeks 13–14)

**Goal:** Room-to-room progression flow.

- Scene flow: MainMenu → Lobby → CombatRoom → RewardRoom → BossRoom → Result
- Lobby: walkable hub with Soul Shop NPC and team builder (Blazblue Entropy Effect style)
- CombatRoom: spawn wave → clear → reward transition
- Room clear condition: all enemies defeated
- Room scaling: enemy HP / speed / aggression increase per room
- Room selection from authored room prefabs

**Accept:** Player enters lobby, starts run, clears 3+ rooms, reaches boss or fails.

#### Sprint 8: Rewards + Soul Shop (Weeks 15–16)

**Goal:** Rogue-lite reward choices and persistent shop.

- Post-room reward: pick 1 of 3 upgrades
- Upgrade types: stat boost, skill modifier, team modifier, survival, economy
- Soul Shop: spend currency on characters / artifacts
- Soul Fragments (retained on death) + in-run currency (lost on death)
- Shop reroll with increasing price
- Character Potential: buy same character again → permanent upgrade (1–6 levels)

**Accept:** Upgrades change run feel. Shop provides between-run progression.

---

### Month 5 — Boss & Content

#### Sprint 9: Boss Pattern System (Weeks 17–18)

**Goal:** Pattern-based boss with clear telegraphs.

- Boss controller: phase-based pattern sequencer
- Phase 1: slow melee + telegraph AoE
- Phase 2: add ranged projectile patterns
- Phase 3: faster patterns, shorter windows, higher density
- Boss resists stagger-lock (super armor during attacks)
- Clear danger indicators: ground markers, charge-up VFX
- Damage windows after major attack recovery

**Accept:** Boss feels like Hollow Knight — telegraph → dodge → punish → repeat.

#### Sprint 10: Content Expansion (Weeks 19–20)

**Goal:** Scale content after the loop works.

- 3–5 playable characters with distinct kits (range, damage type, role)
- 3+ enemy archetypes: trash, ranged, shield, elite
- 5+ room layouts with varying compositions
- 10+ upgrades across all reward categories
- 3+ artifacts with runtime stat bonuses; passive descriptions require separately authored runtime effects
- Difficulty scaling per room depth

**Accept:** Runs feel varied. Characters differ. Upgrades create build identity.

---

### Month 6 — Polish & Thesis

#### Sprint 11: Polish Pass (Weeks 21–22)

**Goal:** Visual and audio polish.

- Unique skill / ult VFX per character
- Combat SFX library, boss phase music
- UI polish: smooth transitions, responsive feedback
- Camera polish: smooth follow, boss intro cinematic
- Performance: ≥ 60 FPS at 1080p, NonAlloc physics, VFX pooling
- Bug-fixing pass

**Accept:** Polished and responsive. No GC spikes. Stable framerate.

#### Sprint 12: QA + Thesis Delivery (Weeks 23–24)

**Goal:** Ship thesis-quality build.

- Full playthrough: lobby → run → boss → result → repeat
- Unit tests: damage calculator, cooldown system, upgrade application
- Integration tests: team swap, room transitions, shop transactions
- Thesis write-up: architecture docs, design rationale, results
- Clean release build for submission

**Accept:** Thesis submitted. Build runs clean. Systems verified.

---

### Milestone Gates

| Gate | Criteria | Target |
|------|----------|--------|
| M1 — Clean Slate | Fighting-game DNA removed, simple combat compiles | End Month 1 |
| M2 — Combat Lock | BA / Skill / Ult / Dodge feels good with one character | End Month 2 |
| M3 — Team Works | 3-character swap with HUD and cooldowns | End Month 3 |
| M4 — Loop Runs | Full room → reward → boss flow with Soul Shop | End Month 4 |
| M5 — Content Ready | Multiple characters, enemies, upgrades, bosses | End Month 5 |
| M6 — Ship | Polished, tested, thesis submitted | End Month 6 |

---

## Final Instruction To Contributors

Neo-JJK is **not** a fighting game. The target is a **fast 2D hack-and-slash rogue-lite** with simple team rotation, flashy VFX, and low implementation burden.

When in doubt: choose simple over complex, VFX over animation depth, rogue-lite stat growth over combo expression. Build what an amateur dev can finish.
