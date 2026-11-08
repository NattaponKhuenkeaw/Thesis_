# Balance and economy recovery proposal

Date: 2026-11-07 (Asia/Bangkok)

Status: first combat and economy pass implemented. The remaining sections describe proposed starting values and playtest targets.

## First pass in the current build

- Regular enemy HP compounds by 18% per biome. Boss HP starts at 8,000 and reaches 42,000 in biome 10. Both health curves hold at their biome 10 values beyond the tuned range. Enemy attack still follows its existing curve.
- The active Medium and Hard routes distribute budgets of 16 and 20 enemies across the whole layout. The Easy palette has a 12-enemy budget when used.
- Medium combat clears award 175, 250, or 350 Soul in biomes 1–3, 4–6, or 7 onward. Hard clears award 219, 313, or 438. The current Hard artifact patterns and Character Memory draw accompany that fixed Soul payout.
- Boss victories offer one permanent Character Memory for an eligible owned character. The picker follows the death presentation and resolves before the boss continue prompt. The existing Character Memory track has 54 steps.

Shop prices, level cadence, artifact cadence, and selectable Nightmare remain proposed work.

## Constraints and player goal

- Preserve character DPS for the owner's own polishing.
- Max-investment desired output: approximately 10,000–20,000 dummy DPS.
- Lv.30/full SSR/zero Potential reference: approximately 1,200 DPS on a zero-DEF/resistance dummy over approximately 30 seconds.
- Potential remains permanent and supports replayability. The future 36-node reward system is not implemented by this proposal.
- No artifact or fragment selling, and no income assumptions based on those items.
- Address the unpleasant experience already reported in biomes 1–10.

Design intent comes from the GDD's `@tag:enemies` fast-clearing trash, `@tag:bosses` readable dodge/punish encounters, documented Normal/Nightmare direction, run-scoped levels, and permanent Potential. Evidence and current implementation details are in [the analysis](balancing-economy-analysis-design-spec.md).

Player goal: make a useful build during a satisfying run, then retain meaningful Potential progress and choose a greater challenge on later runs.

## 1. Separate run maturity from permanent profile power

Biome progress determines level, equipment maturity, encounter mix, and shop budgets. Permanent Potential determines a profile's available power and kit development across runs. There is no universal biome at which every player should reach 10k DPS.

At the stated 1,200-DPS zero-Potential reference, an 8–10x increase yields 9,600–12,000 DPS. That approaches the lower portion of the owner's desired max-investment band. Reaching 20,000 from exactly 1,200 requires about 16.7x combined output; additional character polishing or stronger combinations would have to supply that difference. This proposal does not alter character output to force that result.

Starting target: a developed permanent profile can assemble its late-run 10k–20k build around biomes 9–10. A fresh profile can finish Normal at zero Potential with the approximately 1.2k late-run reference. It earns permanent progress for subsequent attempts. Full permanent investment is not a requirement for a Normal first clear.

## 2. Give the first ten biomes a useful progression arc

ASSUMPTION: calibrate the first ten biomes as a chapter with a starting total-duration target of roughly 40–50 minutes. This does not declare the current endless progression finished at biome 10.

IMPACT: level, loot quality, enemy budgets, and shop purchasing power can be tested against a common endpoint.

IF WRONG: a substantially shorter or longer intended run requires different reward cadence and investment milestones.

VALIDATE: complete the chapter with a fresh-profile reference build and a developed-profile build, recording total duration and time spent in combat, traversal, shops, and reward screens.

Starting level rule: guarantee +3 shared character levels once per biome, capped at Lv.30. The existing Level event should be a guaranteed stop with a concise presentation; access to this baseline growth should not depend on accepting an optional route. Replace the current random +1/+2/+3 baseline for this trial.

| End of biome | Character level target | Equipment direction |
| ---: | ---: | --- |
| 1 | 4 | Start filling equipment; R with some SR opportunities |
| 3 | 10 | Establish an SR build |
| 5 | 16 | Start replacing selected items with SSR |
| 7 | 22 | Mostly developed build; several SSR items |
| 9 | 28 | Full SSR loadout becomes attainable |
| 10 | 30 | Complete late-run build |

These are useful-equipment rarity targets, not guarantees of perfect stat rolls. Test both two- and three-character teams, since they have different equipment demand.

Starting encounter targets: ordinary rooms 30–45 seconds including their traversal; bosses 45–75 seconds excluding intros for the typical intended build. Four combats and one boss per biome imply 27.5–42.5 minutes of fighting across ten biomes before other interactions. The total-duration target is a separate measured goal, not a guaranteed consequence of those ranges.

## 3. Use a fresh-profile Normal baseline and an explicit Nightmare choice

A permanent 8–10x damage advantage creates a comparable reduction in damage time against fixed health. A single unchanged enemy health budget cannot keep both profiles equally challenged. The GDD's documented Normal/Nightmare direction provides a place for greater challenge while preserving visible permanent growth.

Normal should be clearable by the zero-Potential reference profile. Returning developed profiles are allowed to overpower Normal. Offer a clearly labeled Nightmare choice, with recommended permanent development, for players seeking longer-lived encounters and more pressure. Difficulty is selected at run entry and does not secretly scale against equipped DPS.

Starting Nightmare health multiplier: 12x Normal for normal enemies, elites, and bosses. This is an initial hypothesis for the 1.2k versus 10k–20k bands. Do not apply the same multiplier to enemy attack, defense, or resistances. Character HP/DEF do not have a demonstrated corresponding 12x increase.

Keep in-run Soul purchasing power broadly comparable between modes. Greater challenge can reward additional permanent progress or challenge accomplishments; multiplying all currency alongside HP would undermine shop budgeting. Exact permanent reward rates depend on the future Potential acquisition design.

The gap between the two mode bands may later justify intermediate challenge settings for partially developed profiles. Do not assume a partially developed profile is ready for the full starting Nightmare multiplier.

## 4. Slow normal HP growth and author boss health separately

Starting normal HP curve: `base HP * 1.18^(biome - 1)` within the ten-biome calibration chapter. Keep each role's authored health relationship. The approximately 4.44x endpoint is close to the 1.2k/275 = 4.36x increase between the measured fresh-profile start and late-run benchmarks. Intermediate power does not necessarily follow this smooth curve; validate the intermediate milestones.

| Biome | Normal HP multiplier | Melee HP, Normal |
| ---: | ---: | ---: |
| 1 | 1.000 | 200 |
| 3 | 1.392 | 278 |
| 5 | 1.939 | 388 |
| 7 | 2.700 | 540 |
| 9 | 3.759 | 752 |
| 10 | 4.435 | 887 |

The current melee value at biome 10 is approximately 4,132 HP. This starting curve substantially reduces individual enemy lifetimes while allowing composition, positioning, guards, and attack overlap to carry encounter difficulty. Reassess the elite role's health separately if it still becomes an extended boss-like duel inside a room.

Starting boss health uses explicit milestones because the measured DPS gain is uneven: 250–300 early, 500–900 at Lv.10/SR, and about 1,200 at Lv.30/SSR/zero Potential. A single linear percentage does not express those milestones well.

| Biome | Boss HP, Normal | Boss HP, Nightmare at 12x |
| ---: | ---: | ---: |
| 1 | 8,000 | 96,000 |
| 3 | 24,000 | 288,000 |
| 5 | 29,000 | 348,000 |
| 7 | 34,000 | 408,000 |
| 9 | 39,000 | 468,000 |
| 10 | 42,000 | 504,000 |

For a first authored table, intermediate biomes can interpolate between these milestones. Executioner and Observer may need separate values after their actual attack opportunities are measured.

Illustration only: with an assumed 60% effective damage-delivery rate, 42,000 HP at 1,200 dummy DPS represents about 58 seconds of damage time. At 504,000 HP, 10k–20k dummy DPS represents about 84–42 seconds; 15k represents 56 seconds. The 60% figure is not measured. Opening burst, conditional boss bonuses, target access, and scripted phases must be recorded before committing these values.

Enemy attack requires a separate reference-health budget at each milestone. Do not couple it to either health curve or the Nightmare health factor. Test whether ordinary mistakes allow recovery and whether strong telegraphed attacks remain meaningfully dangerous; repeated ordinary one-hit kills indicate a failed damage budget. Avoid using player loadout changes to auto-adjust incoming damage.

Do not extrapolate either curve endlessly past biome 10 as the final game design. Further chapters or difficulty progression require new player-power and encounter targets, especially after level and equipment quality stop increasing.

## 5. Budget whole rooms instead of charging for every chamber

Starting total enemy quotas per combat room: Easy 12, Medium 16, Hard 20. Distribute that quota across the layout's chambers; the current three-to-six-chamber difference should not automatically double the quota.

These totals are trials, not promises that all geometry takes equal time. If a long layout still exceeds the room-duration target because of traversal or repeated gating, reduce that overhead or explicitly compensate its authored reward budget. Measure Soul per minute at comparable risk instead of treating layout name and difficulty as sufficient workload measures.

Preserve readable role mixes and a bounded active enemy count. Difficulty should come from understandable combined threats rather than individual trash requiring prolonged rotations. Rapidly killing a weak enemy is useful evidence of player growth, not automatically a balance defect.

## 6. Guarantee Soul and make artifacts additional growth opportunities

Starting direct-income budgets, before Hard bonuses:

| Biomes | Soul per combat clear | Soul per boss clear | Guaranteed total for four combats + boss |
| --- | ---: | ---: | ---: |
| 1–3 | 175 | 300 | 1,000 |
| 4–6 | 250 | 500 | 1,500 |
| 7–10 | 350 | 600 | 2,000 |

Keep the existing 1,000 starting Soul for the initial trial. These are proposed replacement direct rewards, not additions on top of all old payouts. Consolidate reward ownership during implementation so the same clear cannot pay both new and legacy rewards.

All successful combat routes pay the baseline. Starting Hard bonus: +25% combat-clear Soul, plus at most one additional artifact draft per biome earned through a Hard clear. This gives harder paths extra upside while allowing cautious routes to keep building.

Starting baseline artifact cadence: two choose-one-of-three drafts per biome, following the second and fourth combat clears. This bounds the standard gap between build choices. Hard adds the extra draft described above; shop purchases remain separate. Replace the old zero-Soul/two-artifact jackpot tradeoff in this trial. A poor draft result must not remove the direct income from the encounter.

Stage rarity availability around the chapter milestones instead of immediately reaching the final rarity distribution at biome 3. Provide early slot filling, reliable SR development, then SSR opportunities in the middle and late chapter. The late run should offer enough useful choices to outfit the deployed team without requiring perfect rolls. Draft duplicates and incompatible outcomes need to be measured because selling cannot recover their value.

An existing draft can be reused when feasible. Detailed reward placement and UI behavior remain implementation design work; do not introduce the future permanent-node drop system as a dependency for fixing basic income and equipment pacing.

## 7. Price the shop in units of earned biome income

Starting artifact price bands: R 250–400, SR 600–1,000, SSR 1,500–2,500. The intent is that an ordinary successful biome usually funds a meaningful eligible purchase, with expensive desired items requiring saving. These are proposed bands for the current definitions, not claims that every artifact within a rarity has equal value.

At each shop, make a useful affordable option available for the deployed team. A purchase should have an understandable effect: filling a gap, improving a relevant stat/passive, or acquiring a desired teammate. An all-unaffordable or irrelevant shop cannot satisfy the purchasing-power target even if average income is sufficient.

Stage the shop's rarity with the run's equipment milestones so it remains a credible source of build completion. Perfect rolls are not required for the baseline. Character acquisition is a distinct spending decision; price its access against run budgets without making duplicate purchases a requirement to clear Normal.

Measure the current reroll-wide price escalation separately. The quoted purchase bands assume base prices. If finding a relevant item routinely pushes it outside the expected wallet, improve offer relevance or revise reroll economics before raising all income again.

There is no artifact/fragment sale recovery. Track direct income, spending, unspent Soul, and actual upgrades rather than assigning a cash value to unusable drops.

## 8. Give permanent progression its own replay budget

Potential remains permanent on failure and between runs according to the existing permanence policy. Its acquisition cadence is a separate design budget from run levels and artifacts. The first successful Normal clear should not require max Potential, and later permanent growth should make that same challenge noticeably easier.

The biome at which a mature run build becomes possible is proposed above. The number of replays required for full Potential cannot be calculated until the per-character node/fragment reward rate and reward targeting are selected.

For illustration, if the future system has 36 nodes per character and a player receives six relevant nodes per completed chapter, completion takes about six chapters from zero, before failed-run rewards or other sources. This is an arithmetic scenario, not an approved completion budget. One full node for every one of forty combat clears could instead complete a focused character within one chapter; reward frequency must be chosen deliberately to preserve replayability.

Frequent run rewards can come from Soul, levels, and equipment while permanent node rewards are less frequent and more consequential. Avoid requiring new players to farm an unenjoyable Normal loop before their first viable build. A higher challenge mode should be available as permanent power develops; repeat play should offer interesting build decisions and clearer mastery goals as well as larger numbers.

## Validation and adjustment rules

Use the owner's benchmark characters without changing their DPS. Test fresh-profile and developed-profile saves at biome 1, 3, and 10, including two- and three-character teams. The starting 30-second dummy test should retain a consistent resource state and record opening damage separately.

| Area | Starting pass criterion | If it fails |
| --- | --- | --- |
| Level pacing | Guaranteed baseline reaches Lv.10 at biome 3 and Lv.30 at biome 10 | Fix event access/award cadence before retuning enemies |
| Normal first-clear viability | Zero-Potential reference build can complete Normal through readable mistakes and recovery | Reduce encounter damage/pressure or improve reward access; do not require permanent farming |
| Ordinary room pacing | Typical matched build's median is 30–45 seconds, with no repeated prolonged trash duels | Reduce health/quota if fighting dominates; reduce overhead if traversal/spawn delay dominates |
| Boss pacing | Typical matched build's median is 45–75 seconds and intended attacks are expressed | Increase/decrease health budget based on damage time; inspect opening burst and phase timing before adding HP |
| Nightmare | Developed profile encounters remain meaningful; target team spread is accepted explicitly | Retune fixed difficulty health, keeping enemy attack separate |
| Layout fairness | Comparable-risk layouts' Soul per minute are within roughly 20% | Adjust quota, traversal, or authored payout |
| Soul economy | Ordinary successful biomes usually allow a relevant purchase; repeated poor drafts do not create zero-income clears | Adjust direct income, prices, or stock relevance |
| Artifact pacing | A useful build choice occurs at least every two ordinary combat clears; team rarity milestones are attainable | Improve draft quality/rarity timing or cadence; do not equate extra junk with progress |
| Replay motivation | Players perceive retained progress and voluntarily choose another run or greater challenge | Revisit the permanent reward schedule and variety; do not substitute an HP grind |

Test a cautious route, a mixed route, a practiced Hard-focused route, repeated short-layout choices, and a sequence of poor artifact draws. The user's human playtest review determines whether the pacing is enjoyable; passing time bands alone is insufficient.

## Suggested implementation order

1. Establish selectable difficulty and capture fresh/developed profile reference saves.
2. Guarantee level progression and direct Soul income; remove dependence on artifact jackpots for shop access.
3. Replace normal and boss health budgets and normalize room quotas.
4. Align artifact rarity/draft cadence and shop purchasing power with the chapter milestones.
5. Run the focused playtest matrix, then adjust the starting values using the rules above.
6. Design the future permanent 36-node reward cadence after the run itself is enjoyable.

Character DPS remains the owner's tuning work. The first pass needs a playtest against the reference builds before the starting numbers become a baseline.
