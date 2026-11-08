# Current balancing and economy analysis

Date: 2026-11-07 (Asia/Bangkok)

Status: analysis and provisional playtest targets. No gameplay tuning implemented.

## Scope and evidence

Analyze the current implementation using the owner's measurements. The proposed 36-node Potential system is future work. Current GDD authorities: `@tag:enemies`, `@tag:bosses`, and the Run Global Level / permanent Potential sections in [GDD_Neo-JJK.md](../../1_Inputs_Templates/GDD_Neo-JJK.md).

Owner-directed economy constraint: artifacts and fragments cannot be sold. The owner plans to remove those methods and UI; this analysis excludes all resale income and resale valuations. Removal of the gameplay implementation is outside this analysis task.

Subsequent owner constraints: Potential remains permanent and supports replayability. Character DPS tuning belongs to the owner; the desired max-investment output is approximately 10,000–20,000 DPS. A Lv.30/full-SSR/zero-Potential L'Angelique team with Shin or Rei produces approximately 1,200 DPS, averaged over the roughly 30-second dummy test. The owner estimates an approximately 8–10x reduction without Potential; treat that as an approximate, team-dependent observation. The proposed recovery plan is in [balance-economy-recovery-design-spec.md](balance-economy-recovery-design-spec.md).

The owner confirmed that DPS was recorded over approximately 30 seconds against a training dummy with zero DEF and resistance. These are window-averaged damage benchmarks, not measured average DPS against moving enemies or bosses, and not isolated opening-burst measurements. Biome numbers associated with the character-level benchmarks and intended total run duration remain unspecified.

| Character investment | Team | Dummy DPS |
| --- | --- | ---: |
| Lv.1 | Shin solo | 200–220 |
| Lv.1 | Shin / Rei | 250–300 |
| Lv.10, SR artifacts, 0 Potential | L'Angelique / Rei | 500 |
| Lv.10, SR artifacts, 0 Potential | Shin / Rei | 600–900 |
| Lv.30, SSR artifacts, 0 Potential | L'Angelique / Shin or Rei | About 1,200 |
| Lv.30, SSR artifacts, max Potential | L'Angelique / Rei | 6,000 |
| Lv.30, SSR artifacts, max Potential | Shin / Rei | 4,000–5,000 |
| Lv.30, SSR artifacts, max Potential | L'Angelique / Shin | 10,000–15,000 |

Observed room durations across biomes 1–10: combat 60–90 seconds, boss 20–40 seconds. These are aggregate observations, not a per-biome time series. The owner stopped before further biomes because instant boss kills and slow-feeling player progression were not enjoyable. Instant boss kills are therefore a reported problem within the tested range; the average boss duration should not be interpreted as every boss lasting at least 20 seconds. Biomes beyond 10 below are mathematical projections, not playtest results, and biome 10 is not an established intended run endpoint.

Observed biome rewards: 0–8 artifacts and 0–1,200 Soul. Starting Soul: 1,000. Observed ranges are not probability distributions or implementation bounds.

## Findings

The systems use different rates of growth: normal HP compounds indefinitely, boss HP grows linearly, character levels arrive slowly and cap, and artifact rarity distribution stops advancing after its third authored phase. Reward volume also differs from usable upgrade frequency. These mechanisms explain why normal encounters can drag while bosses lose their threat.

### 1. Normal encounters and bosses diverge

For biome number B, starting at 1:

- Normal HP multiplier = 1.4^(B−1).
- Normal attack multiplier = 1.15^(B−1); DEF remains authored base.
- Boss HP / attack / DEF multiplier = 1 + 0.2(B−1).
- Boss clears advance B; ordinary combat clears do not increase these stat multipliers.

Source: [RunProgressionManager.cs](../../../Assets/Project/Scripts/Core/ZoneState/RunProgressionManager.cs).

Actual normal prefab overrides matter: melee has 200 HP, ranged 100 HP, guard 260 HP, and the melee elite 600 HP. Both bosses have 4,000 base HP. The common EnemyCore's 100 HP / 10 ATK is not the effective base of every variant. The multiplier table from the earlier inspection remains valid.

| Biome | Normal HP multiplier | Melee HP | Elite HP | Boss HP |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 1.00 | 200 | 600 | 4,000 |
| 5 | 3.84 | 768 | 2,305 | 7,200 |
| 10 | 20.66 | 4,132 | 12,397 | 11,200 |
| 15 | 111.12 | 22,224 | 66,672 | 15,200 |
| 20 | 597.63 | 119,526 | 358,578 | 19,200 |

These are raw HP, not effective HP after mitigation and guard behavior. At biome 10 one elite already has more raw HP than the boss. This divergence is already relevant within the played range. Biomes 15 and 20 illustrate future consequences only: at biome 20 that elite has about 18.7 times the boss's HP, and normal attack reaches 14.23 times base. The immediate tuning priority is the observed experience in biomes 1–10.

Player investment has finite caps while enemy stats continue growing. Eventually any capped build is overtaken by the exponential normal curve; a stronger boss curve alone cannot fix this progression mismatch.

### 2. Dummy DPS must be converted into encounter performance

`Boss damage time = HP / effective live DPS`.

Live DPS depends on attack opportunities, movement, execution, defenses, conditional bonuses, and phase behavior. Intros and other fixed delays must be recorded separately. For rooms, add traversal and spawn delays; multi-target damage means that aggregate monster HP divided by single-dummy DPS is not an exact room-duration model.

At biome 10 the boss has 11,200 raw HP. That is equivalent to only 0.75–2.8 seconds at the reported max-investment 30-second average damage rate. This is a damage-budget comparison, not a predicted kill time: a 30-second average does not reveal startup, opening burst, resource availability, or damage timing. The owner's direct report of instant boss kills within biomes 1–10 is stronger evidence of the present problem than this extrapolation. Aggregate 20–40-second boss durations cannot establish attack uptime without matched biome/build records and phase timelines.

Record the opening damage separately from the full 30-second average and identify which actions/resources produced it. A boss may die to a front-loaded rotation even when the sustained average looks acceptable. Conversely, a build that ramps during the dummy test may not deliver that average against a short-lived boss. Match the opening resource state to actual boss entry; retain area coverage and target access as separate variables for normal rooms.

At max investment, L'Angelique / Shin produces 2–3.75 times Shin / Rei's DPS and 1.67–2.5 times L'Angelique / Rei's DPS. With equal attack access, a shared boss's damage time varies by those same ratios.

A boss HP increase cannot independently solve this spread. For example, if the goal were to keep all teams between 45 and 90 seconds, that window permits a 2x damage-time spread. The observed 3.75x team spread exceeds it. A deliberate exceptional-build speed advantage is valid, but needs an explicit allowance.

The measurements also change level, artifact rarity, and Potential together. They do not isolate the contribution of any one progression system. Rei's weak Potential is an owner observation; these team totals alone cannot distinguish weak nodes from team synergy, rotation uptime, or damage attribution.

### 3. Character leveling is sparse relative to time spent

The current event awards +1/+2/+3 with weights 65%/25%/10%, averaging +1.45 levels. The default act has four Combat nodes and one optional Level node, plus Shop, Rest, and Boss. The Level room is one-shot per biome; skipping it makes progression slower.

Sources: [RunGlobalLevelReward.cs](../../../Assets/Project/Scripts/Progression/RunGlobalLevelReward.cs), [RoomChoiceDirector.cs](../../../Assets/Project/Scripts/Core/ZoneState/RoomChoiceDirector.cs).

Assumption: take one Level event in every biome. Expected event counts to first reach the target, accounting for overshoot, are approximately 6.47 to reach Lv.10 and 20.26 to reach Lv.30. Calculation: E(0)=0; E(n)=1+0.65E(max(0,n−1))+0.25E(max(0,n−2))+0.10E(max(0,n−3)).

Four measured combat rooms plus a boss consume 4:20–6:40 of fighting per biome. Using that as a rough duration budget, reaching Lv.10 implies about 28–43 minutes of fighting; reaching Lv.30 implies 88–135 minutes. Menus, travel outside timed rooms, extra combats, and skipped Level rooms add time. The Level event occurs within a biome, so these are approximate pacing budgets rather than exact arrival times.

At the duration midpoints, four combat rooms consume 300 seconds and the boss 30 seconds: about 91% of this fighting time is in normal rooms. If the desired run is substantially shorter than the implied progression budget, the max-investment benchmark is not a representative normal-run endpoint.

For the actual tested range, ten biomes with four combats and a boss each imply approximately 43–67 minutes of fighting under these aggregate-duration assumptions. Ten completed Level events yield roughly character Lv.15.5 on average; skipped events reduce that result. This is a configured pacing scenario, not a claim about the owner's actual character level or session duration. The priority is to deliver meaningful growth throughout the played first ten biomes rather than using an unplayed max-level endpoint as the sole balance reference.

### 4. Current rewards can make Hard the required growth route

The authored reward configuration currently disables the ordinary artifact-chance branch (`grantArtifacts: 0`). Hard's patterned rewards are handled before that flag and still award artifact draws when the picker is configured.

| Route | Baseline mean Soul per clear | Mean artifact draws per clear |
| --- | ---: | ---: |
| Easy | 90 | 0 |
| Medium | 135 | 0 |
| Hard | 202.5 | 1 |

Hard patterns: 25% two draws / zero Soul; 50% one draw / 180–280 Soul; 25% zero draws / 300–400 Soul. Soul scales by 1+0.4(B−1); this is linear. Source: [RouteOfferTables.asset](../../../Assets/Project/Resources/MapSO/RouteOfferTables.asset), [RouteOfferGenerator.cs](../../../Assets/Project/Scripts/Core/ZoneState/RouteOfferGenerator.cs).

Conditional on four Hard clears: expected rewards are four artifact draws and 810 Soul at biome 1. Artifact count can be 0–8, but each extreme has probability 0.25^4 = 0.39% under independent draws. The theoretical direct Soul range is 0–1,600 before biome scaling or other direct income. The owner's 0–1,200 is an observed range, not a configured upper limit.

For four clears chosen blindly from the 40/35/25 Easy/Medium/Hard roll weights, the illustration becomes one expected artifact draw and 535.5 expected Soul. Actual route selection changes these values: card roll weights are not the distribution of routes players choose. Extra Combat choices at safe nodes also change the per-biome totals.

A cautious player who avoids Hard receives no combat artifact draws through this configuration while still facing the next biome's 40% HP increase. That can create a feedback loop: low power -> avoid Hard -> less build growth -> fall further behind.

### 5. Rewards ignore large differences in room workload

The current mapped layouts have these chamber counts:

| Layout | 01 | 02 | 03 | 04 | 05 | 06 | 07 | 08 | 09 | 10 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Chambers | 3 | 4 | 4 | 4 | 4 | 3 | 4 | 3 | 6 | 5 |

The active Easy/Medium/Hard encounter palettes supply 4/6/8 enemies per chamber. Consequently a three-chamber Hard layout has a quota of 24 enemies and a six-chamber Hard layout 48. Role mix and the final elite affect actual effort further.

The reward builder uses route difficulty and biome, with no chamber-count term. Thus equivalent difficulty cards can promise the same reward distribution for double the enemy quota. This encourages selecting short layouts and makes Soul per minute unpredictable.

Sources: [CombatStageController.cs](../../../Assets/Project/Scripts/Core/ZoneState/CombatStageController.cs), [Encounter_Hard.asset](../../../Assets/Project/Resources/EncounterSO/Encounter_Hard.asset), mapped prefabs under `Assets/Project/Resources/RoomLayouts/LV/`.

### 6. Direct Soul income and usable upgrades must each sustain progression

Current artifact retail prices: R 275–450 (mean 355), SR 800–1,200 (mean 993.75), SSR 1,900–4,800 (mean 3,305.56). These means weight authored definitions equally; they are not measured purchase or draft distributions. Shop prices rise with rerolls; base prices do not rise directly with biome. The current reroll default adds 15% of base price per reroll and charges no separate upfront Soul fee in ShopManager.

At baseline, four Hard clears generate 810 expected cash: about 0.82 average-priced SR purchases or 0.25 SSR purchases. Four blind-weighted clears generate about 0.54 SR purchases. Starting Soul 1,000 exceeds either expected biome cash flow, so the first purchase can feel stronger than the subsequent replenishment rate.

Under the owner's no-selling constraint, artifacts and fragments provide zero Soul income. Their purchase prices cannot be used as cash equivalents for reward comparisons. The Hard branches therefore trade direct build opportunities against purchasing power: two draws / zero Soul, one draw / some Soul, or no draws / more Soul.

If artifact draws fail to produce useful equipment, the player can receive neither a usable upgrade nor any shop funding from that reward. This becomes more consequential as slots fill. Evaluate direct Soul flow, actual equipment improvements, and time between meaningful rewards separately; the current data does not establish a cash-equivalent value for an artifact draw.

Rarity availability also favors Hard rewards:

| Biome phase | Shop R / SR / SSR weights | Hard reward SR / SSR weights |
| --- | --- | --- |
| 1 | 90 / 10 / 0 | 100 / 0 |
| 2 | 55 / 45 / 0 | 70 / 30 |
| 3+ | 25 / 45 / 30 | 35 / 65 |

Source: [ArtifactRollTable.asset](../../../Assets/Project/Resources/ItemSO/Artifacts/ArtifactRollTable.asset). Later biome indices clamp to the final phase. Authored rarity profiles control generated stat-line counts and amount bands, so repeated higher biome indices do not imply indefinitely stronger rolls within a rarity.

The early shop mostly sells R while Hard awards SR directly; biome 2 Hard can award SSR before the shop can roll SSR. That makes the shop's artifact role unclear unless it offers specific build completion or another reliable advantage.

Three equipped slots per character also mean that drop count stops representing progression after slots fill. The useful metric is meaningful upgrades per minute, not artifacts collected per biome. Unwanted drops provide no currency recovery and can instead add inventory work.

## Provisional tuning direction

These are starting targets for discussion and playtesting, not approved values or external benchmarks. Intended run length and biome-to-investment milestones must be selected before fitting replacement stat curves.

1. Establish expected and exceptional build benchmarks at each biome. Keep artifact rarity, Potential, and character-level effects separately measurable. Current permanent Potential also requires a veteran-at-low-character-level benchmark.
2. Starting duration targets: typical-build normal rooms 45–60 seconds including traversal, bosses 60–90 seconds excluding intros. Test matched early/mid/late saves over repeated clears; pass when the typical build's median fits each band and players experience the intended boss attacks. If combat damage time is excessive, reduce the health budget; if travel/spawn delay dominates, reduce that overhead. If bosses disappear during their first punish opportunity, increase their damage budget or revise encounter sequencing. Exceptional builds may clear faster by deliberate design.
3. Build enemy HP budgets from expected live DPS and desired damage time. Use the biome's expected player power, not automatic scaling against the player's currently equipped damage. Regular enemies should retain short individual lifetimes even when room composition becomes harder. Bosses need enough damage budget to express their mechanics.
4. Normalize room workload and reward together: either constrain quotas across layouts or pay for the extra workload. Validate Soul and usable upgrades per minute across three- and six-chamber routes. If longer routes consistently pay less per minute at comparable risk, raise their rewards or reduce their quota.
5. Starting purchasing-power target: ordinary successful biome play funds roughly one meaningful shop purchase through direct Soul income. Test median actual purchases and upgrade usefulness across cautious, mixed, and Hard-focused routes; if ordinary players cannot purchase an upgrade, adjust income/price/offer relevance; if they routinely buy out useful stock, strengthen deliberate spending choices. When redistributing guaranteed cash and jackpots, account explicitly for changes to mean Soul income and usable-upgrade frequency.
6. Give cautious routes reliable growth and Hard routes extra upside. Audit each reward branch for both usable-upgrade likelihood and progress toward a meaningful purchase. Decide whether the shop supplies roster acquisition, build completion, or broad power; tune its offers and prices to that purpose. No artifact or fragment resale income is available to cover a shortfall.

Illustrative boss-budget calculation only: 6,000 dummy DPS, assumed 60% effective damage delivery, and 60 seconds of damage time imply 216,000 raw HP before other modifiers. The same health takes a 4,000-DPS team 90 seconds and a 15,000-DPS team 24 seconds at identical delivery. The 60% assumption is not measured; do not apply 216,000 HP as a global setting. Measure live damage delivery first and choose how much faster an exceptional team should clear.

## Future 36-node Potential implications

Splitting twelve nodes into thirty-six does not itself accelerate felt growth. At unchanged nodes earned per minute, collecting the full set takes three times as long; splitting the same total power across nodes reduces the average effect per node. More frequent useful rewards, accessible early identity changes, and relevant random offers are necessary for the proposed system to solve the current pacing problem.

The owner has confirmed that Potential remains permanent. Whether the proposed 36 nodes are counted per character or across a shared scope remains a future design detail. Do not price or tune the current game as though the new node-reward system already exists. A profile's permanent progress and a run's level/equipment progress must be modeled separately.

## Experience filter and validation

- Clarity: expose expected reward terms and make the next biome's threat understandable. Artifact count alone hides upgrade likelihood and does not indicate shop funding.
- Motivation: ensure completed combat gives visible progress; current cautious routes and infrequent leveling can leave long gaps.
- Response: these numbers do not establish input or counterplay quality. Record lost attack opportunities and interruption causes separately.
- Satisfaction: test whether upgrades change an observable outcome, such as clearing a group faster or enabling a rotation, rather than merely adding inventory.
- Fit: long individual trash lifetimes conflict with the GDD's fast-clear trash role; bosses that cannot express their attacks lose their intended dodge/punish loop.

Record per run: biome, character level, Potential, team, equipped artifacts, live damage by source, attackable boss time, active combat time, traversal/spawn time, room chamber/quota count, rewards offered and chosen, direct Soul earned/spent, and upgrades actually equipped. For boss records include the number of attack patterns completed. For team analysis credit support buffs, energy, survivability, and off-field contribution rather than comparing personal damage alone.

Validate with a cautious new-player route, an ordinary mixed route, a practiced high-synergy route, repeated short-layout selection, and a sequence of artifact rewards that fail to improve equipped items. Human playtest review should judge whether rewards feel earned and whether stronger builds still leave readable and meaningful combat decisions.

No gameplay files were changed and no Unity playtests were run for this analysis.
