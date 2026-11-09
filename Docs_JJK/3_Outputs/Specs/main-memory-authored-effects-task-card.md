---
slug: main-memory-authored-effects
status: needs-human
source: manual
gdd_tags: [characters, combat]
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Main Memory authored effects revision

Source: Apricha's 2026-11-09 request. Workflow: `/refactor`, with the explicitly requested kit changes and damage fixes included. This request supersedes earlier Main Memory values where they differ, including the previous no-stun correction. The user authorizes improving the existing systems to support the revision, prohibits generated runtime definitions/projectile variants and Main-specific serialized configuration sections, and requests preserving unchanged behavior.

## Scope and acceptance

- Skill: 20%/40% vulnerability for 10s at Lv1/3. Retain Lv2 weak pull. Lv3 adds 0.5s stun and stronger pull. Lv4 is a 15s RuntimeEffect with persistent field presentation. Every 2.5s it selects the enemy nearest the active player's current position and deals 200% ATK Normal damage, then separate Additional Darkness damage equal to 8% own Max HP. The damage source remains Main after switching. Retain Lv5 allies' ADI/DM/DI = own HP / 120 for 10s and Lv6 10s cooldown.
- Ultimate: Void Area lasts 20s, ticks every 5s for 200% ATK Void damage; Lv2 15% vulnerability for 7s, refreshing; Lv3 +5 AE per damaging tick; Lv4 the Void hit deals 500% ATK Normal damage, followed by separate Additional Darkness damage equal to 6% own Max HP; Lv5 40s and any Ultimate grants 40% own HP for 12s, refreshing; Lv6 the Void hit deals 1000% ATK Normal damage, followed by separate Additional Darkness damage equal to 12% own Max HP.
- Passive: preserve Lv1-4 meter, normal unlock, critical defense bypass, Execution heal/gain, paid-cut follow-up and Void occupancy rules. Lv5 retains 850% Judgment and uses RuntimeEffect additional Darkness = 20% own HP on the final Jailbreak/Execution hit. Lv6 Judgment's last hit deals additional Darkness = 14% own HP to all room enemies, once per cast; completion restores 20 AE and halves current Skill/Ultimate cooldowns.
- Author projectile variants as assets; remove Main-specific runtime variant generation, coroutine and finisher lookup. Reuse recipes, source-stat authority, pooled projectiles and the damage processor. No build/CI/dependency/secret changes, merge or publishing.

## Impact and responsibility

| Area | Before | After |
| --- | --- | --- |
| MainMemoryKit.Projectiles | Generates variants, runs summon, selects finisher and damage | Removed; existing potential modifiers select authored assets |
| NormalHitbox / HitBoxDamage | Single source action budget | Optional mixed-stat formula evaluated from current source stats; empty preserves existing behavior |
| RuntimeEffect | Existing stats/additional damage/timers | Reusable periodic projectile formula; authored damage-tag filters and room target option |
| ProjectileData / ProjectileRuntime | Main-only auxiliary context | Generic hit recipes and cadence/AE policy |
| Existing resource effects | Flat/stat-based cooldown seconds | Optional remaining-cooldown ratio |

## Experience and validation

Numbers are supplied by the user; stronger pull retains the existing doubled Lv2 value (3 units/s). Clarity uses existing chain start/loop clips, persistent summon field and existing debuff feedback; Response preserves action inputs, cancels and costs; Satisfaction/Fit preserve hit feedback/audio; Motivation follows the requested upgrade tracks. Summon enters on Dark Chain cast, ends on duration/owner death/room clear, survives switch, and skips ticks with no target. New casts refresh without stacking. Action entry, interruption and chaining remain owned by the current FSM.

Verify authored rank boundaries, separate ATK primary and own-Max-HP Additional Darkness hits after swap, pure HP additional damage, no finisher proc on earlier hits, one room pulse with multiple targets, cooldown reduction from remaining time, timer expiry/clear/death, unchanged meter/NA regressions and Unity compilation. Human playtest: readability of summon targeting and stun, input spam, swapping, elevated/untargetable enemies and multiple overlapping casts. Architecture scope is approved by the direct request; combat feel remains a human review after implementation.

## Implementation and evidence

Implemented using saved ProjectileData assets, existing potential replacement modifiers, RuntimeEffect recipes and the unified damage processor. Removed `MainMemoryKit.Projectiles.cs`, its generated projectile cache/coroutine, the special finisher resolver and the Main-only fields on PotentialNodeDefinition. Optional mixed-stat hit formulas leave empty/unchanged action formulas intact. Explicit inventory effect removal now invokes expiry callbacks so owned field presentation is released.

Unity compiled without errors. `Temp/main-authored-memory-tests.xml`: 161 passed, 4 failed (165 total), including all 9 new direct damage/cooldown checks passing. The four remaining failures are unchanged dash/target expectations: `DashCachesBoxesWithoutTargetAndNormalConsumesOneCutPerInput`, `LessThanOneBoxDoesNotCacheAndNoTargetDoesNotSpendCache`, and the two false cases of `MotionRadiusGatesActionAvailabilityAndCommit`. The action table was compared to HEAD and differs only in three final-hit damage tags after removing serializer defaults; no dash availability data or logic was changed. Earlier Main task documentation also records outstanding dash/target expectations.

`Temp/main-summon-play-tests.xml`: 1 passed, 0 failed. This enters Play mode and uses the real inventory effect application/tick/removal path plus deployed source-stat resolution. It verifies no strike before 2.5s, nearest targeting, a fixed field anchor after switching, Main's live Max HP changing from 1000 to 2000 (damage 280 to 360 at 100 ATK), explicit removal cleanup and room-clear cancellation. Human feel review is the remaining checkpoint; no merge or publication was performed.

## Damage split clarification

Apricha clarified that ATK + own-Max-HP entries are two damage events, not one combined elemental hit. The Skill Lv4 chain and Ultimate Lv4/Lv6 Void hits now use ATK-only primary damage (Normal for those upgraded variants) and a separate `AdditionalDamageOnHitRuntimeEffectDefinition` event for own-Max-HP Darkness. The base Void rank keeps its existing Void element. Updated the existing direct damage test and playtest expectations; tests have not been rerun after this correction.


## Field icon clarification

The summon indicator is a small procedural dark-purple pixel-chain icon beside the active player, using `RuntimeEffectPresentation.fieldVisualPrefab` and the existing pooled `FieldEffectVisual`. The recipe opts into `followActiveCharacter`; ownership, stats and the 15s timer stay on Main through switches. The periodic formula now spawns only attack strikes. Removed the unused `fieldProjectile` parameter and the mistakenly authored chain-field ProjectileData. Attack origin/target selection and damage are unchanged by this visual correction.


Field icon validation (2026-11-09): Unity compilation completed without C# errors. `Temp/main-recipe-field-icon-tests.xml`: 17 passed, 0 failed (following icon movement, switch ownership, off-field expiry, six shared visual lifecycle checks, authored summon asset and direct damage/cooldown regressions). Inspected `Temp/main-dark-chain-icon-preview.png` rendered beside Main. The initial broad artifact selection also encountered 16 failures in legacy artifact setup/recipe lookup; those unrelated fixtures were not changed. The native summon Play mode run was cancelled at Unity's unsaved-scene prompt to preserve Test_Playground, so the earlier native-run evidence does not validate this icon correction.


## Switch regression: HUD overwrote shared presentation

The team inventory already updates Main's summon while Main is off field. However, `PlayerHUD.RebuildRuntimeEffects` also called `SyncVisuals` with only the active character's effects, removing Main's team-visible icon after the receiver had restored it. Removed that duplicate world-presentation write: the HUD owns its status row and shield, and RuntimeEffectReceiver owns field visuals from the team inventory. The summon retains one instance, source and timer; switching does not copy it onto allies or restart it. Regression now executes repeated incoming-character HUD rebuilds after the actual SetCharacter path, plus an isolated no-target summon scheduler check across switching and expiry.

Validation of the switch fix: the HUD regression failed before the fix with the icon in Ending after an incoming-character HUD rebuild. After removing the duplicate HUD SyncVisuals call, `Temp/main-recipe-field-icon-tests.xml` reports 18 passed, 0 failed. This includes real SetCharacter/inventory timer handling in isolated EditMode with no target projectiles, repeated HUD rebuilds, off-field expiry and existing damage/visual checks. Unity compiled without C# errors. Native PlayMode projectile verification was not rerun in this correction.
