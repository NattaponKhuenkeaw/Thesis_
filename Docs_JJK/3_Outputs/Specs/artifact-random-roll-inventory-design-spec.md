---
slug: "artifact-random-roll-inventory"
status: approved
source: manual
gdd_tags:
  - "core-loop"
  - "ui"
  - "progression"
  - "items"
owner: "game-design-agent"
human_checkpoint: required
next_agent: architect-agent
blocked_by: []
---

# Design Spec: Artifact Random Rolls and Inventory Discovery

> **Task Card:** `artifact-random-roll-inventory-task-card.md`  
> **GDD Source:** `@tag:core-loop`, `@tag:ui`, `@tag:progression`, `@tag:items`  
> **Date:** 2026-08-17

## 1. Player Goal and Experience

The Artifact hunt changes from collecting one permanent copy and upgrading it into repeatedly finding distinct copies, reading their rolls, and choosing the best fit for a character's Primary, DOT, or Additional Damage path.

Artifacts do not have rarity, stars, or rarity-colored frames. Their value comes from three readable facts:

- Which stat types rolled.
- How many stat lines rolled, from one to four.
- How strong each value is within that stat's allowed range.

The inventory supports discovery rather than a new item-management acronym: players sort, filter, preview, equip through the existing loadout flow, and mark important instances as Favourite.

## 2. System Rules

### 2.1 Definition and Instance

An Artifact's name, icon, lore, passive, price, and authored Additional Damage effects belong to a reusable **Artifact Definition**. Every acquisition creates a separate **Artifact Instance** containing:

- A stable unique instance ID.
- The source definition ID.
- One to four rolled stat entries.
- Favourite/new/acquisition-order state.
- Runtime equip ownership by instance ID, never by definition reference.
- Migration provenance when created from legacy data.

Two instances of the same definition may coexist. Numeric rolls stack normally when separate instances are equipped. Authored passive stacking continues to follow the passive's own runtime policy; duplicate ownership does not silently alter passive rules.

There is deliberately no `ArtifactRarity` field. Stat-line count and roll quality remain independent sortable data.

### 2.2 Number of Stat Lines

The supplied percentages describe the chance that an Artifact has *at least* that many contiguous lines. One percentile roll determines line count so a generated Artifact can never contain a missing second line but have a third line.

| Lines generated | Exact chance | Cumulative chance |
| ---: | ---: | ---: |
| 1 | 50% | 100% have at least 1 |
| 2 | 15% | 50% have at least 2 |
| 3 | 20% | 35% have at least 3 |
| 4 | 15% | 15% have 4 |

Equivalent percentile bands are `[0,15) → 4`, `[15,35) → 3`, `[35,50) → 2`, and `[50,100) → 1`.

The UI may say `3 Stat Rolls`; it must not translate that count into Common/Rare/Epic/Legendary or stars.

### 2.3 Random Stat Pool

Each line selects from the complete pool with equal type weight as the transparent starting rule. Selection is with replacement, so the same type may appear more than once on one Artifact. Each entry remains visible separately; the preview may additionally show an aggregate such as `CR +13.42% (2 rolls)`.

| Roll type | Inclusive range | Runtime application | Display precision |
| --- | ---: | --- | ---: |
| Flat HP | 153–1,025 | Add directly to Max HP | Integer |
| Flat ATK | 18–116 | Add directly to ATK | Integer |
| Flat DEF | 22–138 | Add directly to DEF | Integer |
| HP% | 7.00–34.98 | Percentage of clean base Max HP | 2 decimals |
| ATK% | 7.00–34.98 | Percentage of clean base ATK | 2 decimals |
| DEF% | 8.74–43.74 | Percentage of clean base DEF | 2 decimals |
| CM% | 5.12–25.50 | Add percentage points to CM | 2 decimals |
| CR% | 4.66–23.34 | Add percentage points to CR | 2 decimals |
| CD% | 5.12–25.50 | Add percentage points to CD | 2 decimals |
| AER% | 7.78–38.88 | Add percentage points to AER | 2 decimals |
| DM% | 9.32–56.23 | Add percentage points to DM | 2 decimals |
| DI% | 9.32–56.23 | Add percentage points to DI | 2 decimals |
| ADI% | 9.32–56.23 | Add percentage points to ADI | 2 decimals |

SPD remains system-only and is never selected by random Artifact generation.

### 2.4 Weighted Amount Roll

Every stat definition owns its min/max and an authorable normalized weight table. The following is a **starting distribution**, not a final balance claim:

| Portion of min-to-max range | Starting selection weight |
| --- | ---: |
| 0–20% | 35 |
| 20–40% | 30 |
| 40–60% | 20 |
| 60–80% | 10 |
| 80–100% | 5 |

After choosing a band, the amount is uniformly sampled inside that band and quantized to the stat's display precision. The inclusive maximum remains reachable.

**Tuning test:** generate 10,000 deterministic rolls for every stat. Pass if every value is in range, forced-boundary tests reach both endpoints, and observed band rates are within ±1 percentage point of configured rates. If almost all generated Artifacts look interchangeable, increase separation between the bands; if useful values almost never appear, shift weight upward.

### 2.5 Roll Quality

Roll quality is a numeric measurement, not rarity:

```text
lineQuality = (rolledValue - minimumValue) / (maximumValue - minimumValue)
artifactQuality = average(lineQuality for generated lines)
```

- Display quality as `0–100%`, rounded for UI only.
- Preserve full precision for sorting.
- Show each line's quality in the preview and the average as `Average Roll Quality`.
- Do not add S/A/B tiers, stars, rarity names, or rarity colors.
- Keep line count as a separate filter/sort option because average quality does not measure total stat volume.
- A legacy-import value outside the random range shows `Imported` and quality `—`; it is excluded from the average rather than clamped into a dishonest score.
- If every line on an Artifact is imported, its average quality is unavailable and it sorts after generated quality values unless the player selects another sort.

### 2.6 Decimal Stats and Integer Damage

- Artifact rolls, resolved Character stats, modifiers, mitigation, DOT snapshots, and previews keep decimal precision.
- Final damage is quantized exactly once after all modifiers and mitigation, immediately before target HP application.
- The applied HP delta, `DamageResult`, damage number, Training Debug value, and hit notifications use that same integer.
- Rounding policy: nearest integer, clamped to a minimum of zero.
- Positive pre-rounded damage below `0.5` becomes zero and does not trigger positive-damage side effects.
- Pre-quantized DOT strength remains available only for stronger/equal/weaker replacement comparison; each applied tick uses its quantized integer result.

### 2.7 Acquisition Flow

```text
Shop/reward selects an Artifact definition
  → Acquisition creates a new unique instance
    → One roll determines 1–4 stat lines
      → Each line independently rolls type and weighted amount
        → Instance is saved before reveal UI opens
          → Reveal shows its lines and roll quality
            → Player closes, previews, equips, or later marks Favourite
```

Acquisition never checks whether the definition is already owned and never routes Artifact duplicates into Meta Progression. Character duplicate conversion remains unchanged.

## 3. Inventory Sorting and Filtering

The grid keeps active filters visible as removable chips and displays the active sort label. Changing a sort or filter never changes ownership, equipment, or Favourite state.

### Sort Options

- Favourite first.
- Stat-line count.
- Average roll quality.
- Selected stat total, with the stat chosen from the roll catalog.
- Newest or oldest acquisition.
- Artifact definition name.
- Equipped or unequipped first.

### Filter Options

- Search by Artifact definition name.
- Stat-line count: one, two, three, or four.
- Contains selected stat types, with Match Any / Match All.
- Primary preset: ATK, ATK%, CR, CD, DI.
- DOT preset: ATK, ATK%, DM, CM.
- Additional preset: ATK, ATK%, DM, ADI.
- Favourite / Not Favourite.
- Equipped / Unequipped.
- New / Seen.
- Current Artifact definition only.

The three build presets are editable filter shortcuts, not hidden power scores.

## 4. Favourite-Selection Mode

Favourite is a persisted designation, not a lock and not a combat modifier.

```text
Normal Browse
  → Player activates Favourite button
    → Favourite Mode becomes visibly active
      → Player clicks one or more Artifact cards
        → Each click toggles that instance Favourite / Not Favourite
      → Player activates Favourite button again or exits the panel
        → Return to Normal Browse
```

### Interaction Contract

- **Entry condition:** Artifact grid is open and not currently dragging an item.
- **Active behavior:** A left click toggles Favourite on the clicked instance instead of opening preview or initiating equip.
- **Feedback:** The button remains highlighted, a `Select Artifacts to Favourite` banner is visible, and designated cards show a persistent Favourite icon.
- **Exit conditions:** Click the active Favourite button again, press Back/Escape, close the inventory, switch out of the Artifact grid, or begin a scene transition.
- **Interruption:** Exiting mode keeps all toggles already made; there is no Apply step.
- **Drag protection:** Dragging/equipping is disabled while Favourite Mode is active so one click has one meaning.
- **Persistence:** Favourite changes save immediately by instance ID.
- **Read-only contexts:** Combat Zone lookup displays Favourite icons but cannot enter Favourite Mode.

## 5. Feedback Loops and Juice

All timings below are **starting values** requiring UI playtest approval.

| Trigger | Visual | Audio | Starting timing |
| --- | --- | --- | ---: |
| Acquisition reveal | Card fades/scales in; stat count appears before lines | Short reveal swell | 180 ms |
| Each rolled line | Line slides in; high-quality value gets a stronger neutral pulse | Tick with small pitch rise | 120 ms per line |
| Favourite Mode entry | Button fills; instruction banner fades in | Mode-select click | 120 ms |
| Favourite toggle | Icon pops on/off; card border flashes without changing category color | Short mark/unmark click | 100 ms |
| Sort/filter change | Grid reorders; active chip updates | Soft UI switch | 120 ms |

- Reveal is fully skippable with one click; skipping presents the complete result without changing RNG.
- No screen shake or camera movement is used in inventory UI.
- High-quality indication combines icon/value treatment, not color alone.
- A newly acquired instance retains a `New` badge until previewed, equipped, favourited, or explicitly marked seen.

**Timing test:** a new player should understand stat count and rolled values within five seconds of reveal. In Favourite Mode, the player should correctly toggle five intended cards with zero accidental previews or drags.

## 6. Five-Component Evaluation

| Component | Design response | Acceptance signal |
| --- | --- | --- |
| Clarity | No rarity language; stat count, value, and quality are shown as separate facts; mode state and active filters remain visible. | A new player correctly explains why two same-name Artifacts differ in 8/10 observations. |
| Motivation | Every duplicate is another build candidate and good values can be favourited for later testing. | Players inspect and favourite useful rolls without expecting an upgrade screen. |
| Response | Sort/filter are non-destructive; Favourite Mode gives clicking exactly one temporary meaning and saves immediately. | No accidental preview, drag, or equip during Favourite tests. |
| Satisfaction | Reveal cadence and favourite feedback make strong rolls legible without inventing rarity spectacle. | Players identify a high-quality line without consulting the min/max table. |
| Fit | The system strengthens the rogue-lite reward/build loop and the DI/DM/ADI paths. | Filtered choices produce observable Training Debug differences in the intended path. |

## 7. Complete Existing-Data Migration

All nine existing Artifact assets must move into the definition/instance architecture. Migration preserves:

- Asset ID and definition identity.
- Item name, icon, description/lore, price, and equippable flag.
- Passive description and every authored Additional Damage effect/reference.
- Every nonzero current `bonusStats` value.
- Every inventory, shop, codex, team-loadout, prefab, scene, and saved-slot reference.

The current serialized stat audit is:

| Existing asset | Current nonzero stats to preserve |
| --- | --- |
| Blind's Boundless Ribbon | ATK% 15, DM% 15, CM% 5 |
| Fiend-Flesh Mask | CR% 12, CD% 30, DI% 7 |
| Aura-Resonating Shard | DM% 10, DI% 10, AER% 35 |
| Memory-Draining Urn (`a_4`) | HP% 15, hidden SPD -1, DI% 5 |
| Rival's Broken Crest | ATK% 10, CR% 7, DI% 20 |
| Shroud of Deprivation | ATK% 5, DEF% 15, DM% 20, AER% 25 |
| Memory-Draining Urn (`a_7`) | HP% 45, DM% 7 |
| Aura-Charged Shackle | HP% 15, DM% 25, CM% 15 |
| Secret-Observer | ATK% 7, DM% 35, CM% 7 |

### Legacy Stat Mapping

- Existing HP/ATK/DEF bonuses map to their percentage roll types because the current runtime interprets them as percentages of clean base values.
- CM, CR, CD, DM, DI, ADI, and AER map directly to percentage-point roll types.
- `a_4_Memory-Draining Urn` currently contains hidden `SPD -1`. Preserve it as a migration-only system-stat entry. Random generation can never produce SPD, and normal player-facing roll filters do not expose it.
- Preserve values exactly even when outside the new random range. Mark those entries `Imported`; their quality is `—`.
- Imported entries remain functional but are never used as a generation template for future drops.

### Reference Migration

1. Create an Artifact Definition for every current Artifact asset while retaining stable definition IDs.
2. Convert each legacy owned/equipped Artifact reference into one Artifact Instance with a stable generated instance ID and exact imported stat entries.
3. If one legacy reference is equipped in multiple serialized locations that represent the same logical ownership, point them to the same instance. If they represent separate ownership, create separate instances and report the split.
4. Replace `ownedItems`, team slots, TeamLoadoutProfile, PlayerRunInventory saves, TeamLoader snapshots, shop entries, grid cards, and previews with definition/instance-aware references.
5. Remove Artifact duplicate-to-fragment conversion. Character duplicate-to-Potential-fragment behavior remains intact.
6. Report and remove only `ArtifactTranscendence` fragment stacks after the dry-run migration is approved; do not touch Character Potential progress.
7. Validate every current Artifact asset and serialized reference before removing legacy `bonusStats` and compatibility adapters.

No existing Artifact is randomly rerolled during migration. Its current numeric data becomes its first imported instance data, so current builds remain numerically unchanged.

## 8. Risks and Abuse Cases

- **Instance/definition confusion:** Saving definition IDs alone would collapse duplicates. Ownership, equipment, Favourite, and deployment use instance IDs.
- **Quality mistaken for power:** Average quality does not account for relevance or line count. UI keeps selected stats and stat count visible beside quality.
- **Legacy score misinformation:** Out-of-range imported values never receive a fabricated 0% or 100% score.
- **Favourite Mode ambiguity:** Drag/equip and preview are disabled while the mode is active, and the persistent banner explains click behavior.
- **Duplicate stat opacity:** Individual lines remain visible even when an aggregate is shown.
- **Passive stacking abuse:** Each authored passive needs an explicit stacking policy before multiple copies are equipped together.
- **Inventory growth:** Duplicate-friendly acquisition requires pooled/virtualized grid population and allocation-free sorting keys where practical.
- **Orphan IDs:** Missing definitions or instances clear invalid slot references safely and produce migration diagnostics.
- **RNG reroll abuse:** Save the generated instance before reveal and never regenerate on reopen or reload.
- **Rounding mismatch:** Quantize damage once and reuse the integer everywhere.

## 9. Playtest Scenarios

1. **New player:** Acquire two same-name Artifacts with different lines; verify the player understands they are separate without rarity cues.
2. **Readability:** Roll the same stat three times; verify individual values, aggregate total, and quality are distinguishable.
3. **Favourite mode:** Activate the mode, toggle several cards, exit through every allowed path, and verify no preview/drag/equip occurs.
4. **Sorting:** Combine Favourite-first, selected-stat, quality, stat-count, and newest sorts with stable deterministic tie-breaks.
5. **Build path:** Apply Primary, DOT, and Additional filters and verify controlled Training Debug changes affect the expected path.
6. **Flat/percentage:** Compare Flat ATK and ATK% across characters with different base ATK; deployed results match previews.
7. **Stress:** Populate at least 500 instances and repeatedly sort/filter without lost selection or visible frame spikes.
8. **Persistence:** Save/reload duplicates, Favourites, equipped instances, and New badges; identity remains stable.
9. **RNG:** Reload during reveal; instance IDs and rolls never change. Seeded tests reproduce results.
10. **Damage rounding:** Fractional Primary, DOT, and Additional results produce matching HP loss, feedback, result records, and Training Debug integers.
11. **Migration:** Dry-run every current Artifact asset and legacy save; all nine definitions, exact stats, passives, prices, and references appear in the report with no missing data.

## 10. Tuning Priority

If the hunt feels unrewarding, tune in this order:

1. Clarify stat count, values, quality, and active filters.
2. Reduce discovery friction through filter presets and Favourite Mode.
3. Adjust Artifact acquisition cadence.
4. Adjust amount-band weights.
5. Adjust line-count probabilities only after the earlier problems are ruled out.

## 11. GDD Alignment and Required Updates

The design supports `@tag:core-loop` by turning Artifact rewards into repeatable build decisions and supports `@tag:ui` through Safe Zone filtering, sorting, Favourite designation, and existing equip flow. It intentionally supersedes:

- Artifact duplicates converting into Transcendence fragments.
- Artifact scarcity/one-time ownership.
- Fixed per-definition `bonusStats` as the active Artifact stat model.
- Planned permanent Artifact Transcendence levels and Ascend UI.
- Any implied Artifact rarity presentation.

After design and architecture approval, update `@tag:ui`, `@tag:progression`, and `@tag:items` together so the GDD does not describe both systems as active.

## Approved Design Decisions

Approved by the user on 2026-08-17:

1. Artifacts have no rarity; stat-line count and numeric roll quality are independent.
2. CD uses **5.12–25.50%**.
3. There is no CURL system; inventory additions are sorting, filtering, and Favourite Mode only.
4. Favourite Mode activates explicitly and temporarily turns card clicks into Favourite toggles.
5. All existing Artifact data migrates exactly; no legacy Artifact is randomly rerolled.
6. The starting amount bands remain 35/30/20/10/5 from low to high unless replaced.
