# DEF and QTE combat incentive trial

## Intended experience

The opening biome permits normal-attack clears. Later encounters reward selecting a suitable response: normal combos against Enm101 groups, aerial reach against Enm102, positioning or guard break against Enm103, and DEF bypass or secondary damage against Enm101Elite. Character damage coefficients remain the owner's tuning work.

Owner-supplied base values: Enm101 145 HP, Enm102 100 HP, Enm103 600 HP, Enm101Elite 350 HP / 300 DEF.

## Implemented QTE rule

The shared damage resolver ignores numeric DEF when the originating execution has `isQteIntro` set. This includes hitboxes and projectiles carrying that execution, even after the originating character leaves the field. Explicit per-request DEF bypass remains supported. Subsequent ordinary attacks use their own execution and receive ordinary mitigation.

This rule applies to the QTE entry move. Input-driven follow-ups that the existing state machine classifies as ordinary attacks do not inherit entry privileges. It does not bypass resistance, elemental reduction, directional shields, or the enemy's separate armor layer. Existing guard-break flags and positioning remain relevant.

DOT already bypasses numeric DEF; Additional delivery already bypasses DEF and general damage resistance in the current resolver. Elemental reduction is still applied. ADI is the stat that scales Additional damage, not a delivery type itself. Elemental Primary hits still receive DEF mitigation.

## Why DEF needs its own budget

Current formula, before rounding and the minimum-damage floor:

`Primary damage after DEF = incoming damage * 100 / (100 + DEF)`

| DEF | Primary damage retained | Effective HP against Primary without bypass |
| ---: | ---: | ---: |
| 0 | 100% | HP x1 |
| 100 | 50% | HP x2 |
| 300 | 25% | HP x4 |
| 600 | 14.3% | HP x7 |

The owner's 350 HP / 300 DEF elite already requires approximately 1,400 raw Primary damage, versus 350 before other mitigation for a DEF-bypassing attack. Raising both HP and DEF multiplies their effects. Relative to bypass damage, high DEF penalizes ordinary non-bypassing skills as well as normal attacks.

## Proposed scaling, not implemented

Biome 10 target supplied by the owner: ordinary non-flyer enemies around 300 DEF, and Enm101Elite around 900 DEF, for a level 15–20 player with SSR artifact progression. This is a target, not a measured balance result. Enm102 is exempt. Ramp toward it evenly so later biomes ask players to adapt progressively. The elite starts from its existing 300 DEF identity.

| Biome | Enm101 DEF | Enm102 DEF | Enm103 DEF | Enm101Elite DEF |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 0 | 0 | 0 | 300 |
| 2 | 33 | 0 | 33 | 367 |
| 3 | 67 | 0 | 67 | 433 |
| 5 | 133 | 0 | 133 | 567 |
| 10 | 300 | 0 | 300 | 900 |

This linear ramp is a starting proposal, not an approved runtime change. With the existing normal HP curve `1.18^(biome - 1)`, biome 10 has about 4.44 times base HP. That makes Enm101 approximately 643 HP / 300 DEF, or about 2,572 raw Primary damage to defeat before other reductions. Enm101Elite becomes approximately 1,552 HP / 900 DEF, or about 15,522 raw Primary damage. QTE entry, DOT, and Additional delivery avoid the numeric DEF part of those totals, subject to their remaining mitigation and hit behavior.

Against the owner's Lv.15 duo dummy results (1,800–3,500 team DPS), those totals represent about 0.7–1.4 seconds of continuous Primary damage on Enm101, and 4.4–8.6 seconds on the elite. These are arithmetic comparisons, not predicted encounter times: actual uptime, QTE damage per cast, and the LMB-only rate at this build are unknown. The 300 DEF Enm101 target may still be defeatable through sustained LMB; test normal-attack share and encounter time before increasing it further. Avoid changing HP during the first DEF comparison, and give Enm103 a clear positioning or guard-break opening so its shield and 300 DEF do not compound into an unreadable wall. Boss DEF needs a separate budget.

The existing game currently passes a DEF multiplier of 1 to non-boss enemies; the table above is not active.

## Encounter introduction order, proposed

Teach enemy roles before stacking their counters. Biome 1 begins with Enm101 and Enm102 so normal attacks and aerial reach are clear. Introduce Enm103 in a later, lower-pressure encounter with familiar support enemies, so players can see its shield and learn to reposition or use a guard-break move before fighting the elite.

Start placing Enm101Elite in Hard encounters from biome 2 onward. Ensure the Enm103 teaching encounter has already happened earlier in the run, or put a simple Enm103 encounter before the first elite in the same biome. Do not make the elite the player's first shield encounter. Keep the first elite encounter's composition small enough that its DEF bypass lesson is readable; add harder enemy combinations in later biomes.

This encounter order is implemented through phase composition: biome 1 starts without guards, adds one Enm103 after the first cleared combat room, and Hard gains its single final-chamber elite from biome 2 onward. Room quota and spawn cap stay unchanged. The first Hard elite also leaves the Enm103 role present in its final chamber.

Assumption: a fresh team can access a relevant QTE or other counter before the first elite. Impact: DEF is a tactical choice only when the counter is available. If wrong: high DEF becomes a resource wait or mandatory character requirement. Validate by entering the first elite encounter from a fresh profile with depleted resources, and testing trigger availability against the elite itself.

## Playtest and adjustment

- Compare LMB-only, one-character full kit, and team rotation using the same build and encounter at biomes 1, 2, and 3. Record clear time, incoming damage, QTE opportunities, and time spent waiting for resources.
- Starting pass criterion: team rotation clears the elite encounter at least 20% faster than LMB-only without greater incoming damage. If it fails despite successful QTE use, inspect QTE contribution and action downtime before raising DEF. If regular enemies become repetitive chores, reduce their DEF first.
- New player/readability: the tester can identify the elite's DEF and explain why the counter works, using readable visual and audio feedback. If not, improve the cue and teaching encounter before escalating DEF.
- Skill: Enm101 groups remain satisfying to combo; Enm102 rewards aerial reach; Enm103 can be solved through positioning as well as available guard breaks. DEF bypass alone must not erase its shield.
- Stress/abuse: delayed QTE projectiles retain bypass after a swap, while later ordinary attacks do not inherit it. Check repeated QTE triggering and whether a QTE becomes the only productive action.
- Replay: repeat with developed Memory to ensure upgrades still feel stronger. Avoid scaling enemy DEF automatically to the equipped team's current stats.

## Verification

Focused regression cases cover QTE versus ordinary mitigation, preservation of resistance and type reduction, explicit bypass, detached execution, and the following ordinary attack. Runtime and Editor assemblies compiled with the project's Unity compiler response files. Unity test execution and encounter playtests remain pending.
