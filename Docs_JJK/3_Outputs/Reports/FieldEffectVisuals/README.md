---
slug: field-effect-visual-feedback
status: needs-human
source: manual
gdd_tags: [combat, items]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Field Effect Visual Feedback — Implementation Results

## Delivered

- Glowing golden upward chevrons for power-up, blue-violet downward chevrons for power-down, and charcoal/violet Unity particle flames for Toxic.
- Enter, sustained loop, and ending phases. Normal removal stops emission and fades live visuals; cleanup clears immediately on disabled/dead targets.
- One reusable view per target/prefab rather than one loop per buff stack. Different effects retain their independent gameplay state.
- Player visuals follow inventory effects and character swaps directly, without relying on the status-icon HUD. Swaps clear old-recipient tails while preserving off-field effects.
- Shared prefabs, procedural textures/materials, targeted asset authoring, and isolated animation preview windows.
- Untimed effects retain gameplay but have no StatusContainer entries or sustained field visuals. Urn's Crit benefit is classified as Effect.
- Scales has a rounded translucent shield with a shaded sphere rim, projected hex facets, front/back orbit arcs, and bounded motes; confirmed damaging pulses emit shards and an expanding wave.
- Chrono has a timed Mark sigil with rotating halo/embers, a blood streak/core-flash/shockwave burst on consumption, and a Claw glyph using the saved 7s duration.

## Artifact Integration

| Artifact | Visual | Existing content preserved |
| --- | --- | --- |
| Blind's Boundless Ribbon | Dark flames | Saved `Allies` target includes wearer; 10s Darkness infusion. Legacy response migrated to the existing infusion recipe so visual/gameplay expiry share state. |
| Fiend-Flesh Mask | Power-up | Ally ATK stacking after Particle damage; saved recipe lasts 4s and keeps its authored stacking flags. |
| Aura-Resonating Shard | No sustained visual | Its saved Affliction remains indefinite, so the owner's new no-duration presentation rule hides it. The authored power-down prefab remains available for timed debuffs. |
| Shroud of Deprivation | Power-up | Incoming-character support for 10s when wearer switches out. |
| Soul-Merchant's Scales | Rounded blue shield and body-centered pulse | Existing 10s lifetime, 2s interval, damage/heal/mitigation retained. The body shield, actual circular radius-2 pulse and attack expansion share the player collider center with zero offset and no ground snapping. |
| Broken Chrono-Relic | Curse Mark, damage sparks, blood burst, Claw | Mark shares the existing 10s armed window and is a Debuff. Existing enemy damage flashes its seal; consumption/expiry ends its VFX; Claw remains 7s. The explosion uses a circular radius of 2, centered on its hit position. |
| Memory-Draining Urn | No status/field loop | Permanent Crit benefit is Effect; Crit modifiers and threshold logic retained. |
| Shin's Rusty Dagger | Sky lightning and ground sparks | Saved Focus/Thunder rules preserved. A world impact locks to the ground and uses the saved circular radius 3.5 for query, ring and sparks. Indefinite Focus stays hidden. |
| Rival's Broken Crest | Broken glyph and dark flame mark | Existing skill hit creates a 5s enemy-following radius-3 field. Saved 2s damage pulses, 200% ATK, damage-taken modifier and 10s cooldown drive a purple glyph flash, dark flames and subtle attack wave. |

The saved Ribbon target and Mask duration differ from the older authoring-template descriptions. The integration deliberately retains saved gameplay settings rather than rebalancing them.

## Prior Validation (Before Curse / Thunder Follow-Up)

Unity version: **6000.3.4f1**. Script/asset import and compilation succeeded. The radius-2, zero-offset follow-up was compiled and validated with the focused suite.

Prior focused EditMode run after the 2026-10-15 radius-2, zero-offset change: **84 passed / 0 failed / 0 skipped**, saved in [tests.xml](tests.xml). This XML does not validate the later curse/thunder additions.

| Fixture | Passed |
| --- | ---: |
| FieldEffectVisualTests | 10 |
| FieldEffectArtifactSequenceTests | 9 |
| CameraGirlFieldVisualTests | 4 |
| ArtifactAdvancedVisualTests | 9 |
| ArtifactAreaGeometryTests | 6 |
| ArtifactPassivePhase1Tests | 5 |
| CombatDamageTypeTests | 18 |
| DeathResetOwnershipTests | 23 |

Tests cover shared ownership, reuse while ending, unchanged stacks across presentation cleanup, gameplay expiry, particle stop/fade/cap, hidden untimed effects, actual Artifact sequences, and player inventory/swap feedback without a HUD. Advanced checks cover Mark consumption/expiry, the unchanged Claw duration, shared-state expiry across rules, successful-damage-only shield attacks, exact world-space radii under scaled/flipped parents, particle bounds, pool reuse, disable/destroy cleanup, and zero-delta pause sampling. Area geometry checks cover rejected outside-circle targets, rotated bodies, and actual damage from both Scales and Chrono at the anchor center. The radius and offset fields are included in the artifact sequence regression tests.

## CameraGirl Follow-Up

CameraGirl's attack and critical buffs initially retained empty field visual slots because the first content wiring covered Artifacts only. Nine recipe presentation slots now reference the golden power-up prefab: both base random crit outcomes, the base attack buff, and CR/CD buffs at Skill levels 1–3. The asset authoring utility now wires these recipes as well, with a targeted menu for CameraGirl.

A before/after snapshot comparison verified exactly nine `fieldVisualPrefab` assignments changed and every other character-asset line was preserved. Four new checks cover recipe coverage, simultaneous CR/CD ownership until the last expiry, and attack-buff refresh retaining one loop. Targeting still follows the saved recipes, including teammate-only buffs where authored.

An older damage-type test created a second `EnemyDamageTypeState`, while the controller read the first. Its fixture now initializes the existing cached component. Gameplay damage code was not changed.

## Actual Unity Preview Captures

These are isolated Unity renders using project sprites and the authored prefabs, not screenshots of a combat playthrough. Top row: player. Bottom row: enemy. Columns: Power Up, Power Down, Dark Flame.

- [Enter](enter.png)
- [Loop](loop.png)
- [Ending](ending.png)

Open **Tools > Project > Field Effects > Preview Animations** to animate or scrub each phase in the Unity Editor. The preview does not save or replace the open scene. **Create or Update Visual Assets** rebuilds shared procedural assets and wires the four existing recipes without running the broader Phase-1 content generator.

Advanced captures now show five columns: Scales, Chrono curse/blood, Claw, Dagger thunder and Rival's Crest. Ground lines provide preview context. Scales/Chrono use radius 2; Dagger uses its saved radius 3.5 and Crest uses radius 3. Attack queries and visuals share the same resolved centers and dimensions. The shield shell and attack expansion both center on the player's collider.

- [Advanced enter](advanced-enter.png)
- [Advanced loop](advanced-loop.png)
- [Advanced attack](advanced-attack.png)
- [Advanced exit](advanced-exit.png)
- [Curse damage and lightning impact](curse-thunder-impact.png)

Use **Preview Advanced Artifacts** for animation/scrubbing, **Capture Advanced Artifacts** for renders, and **Create Advanced Artifact Visuals** for targeted regeneration/wiring. The sphere rim/facets, streak textures, Mark halo, particle layers and final rendered phases were inspected. No saved scene is modified by these preview tools.

## 2026-10-15 Owner Clarifications and Tuning

Urn remains entirely hidden. Scales and Chrono use equal X/Y radii of 2 and a vertical multiplier of 1. The latest owner correction supersedes Scales' ground lift: Scales follows the player collider center, with zero offset and no ground snap; Chrono centers on the target body collider. Shared area geometry still supports optional vertical scaling and ground offsets for other authored content. No additional gameplay colliders are created.

Scales uses up to 12 loop motes and 32 attack streaks. Chrono uses at most 48 blood streaks. The actual Scales attack and Chrono explosion are circular, with X/Y radius 2 and zero center offset. Scales expands concentrically from its body shield. Attack particles simulate in world space, so movement after a pulse does not drag its burst away from the hit position; local Mark/shield motes follow their target. Crest has up to 12 dark flames and 16 attack streaks. Natural exit stops emission and fades live particles; cleanup clears immediately. Urn has no added activation flash, world loop, or status entry.

## Review and Limits

Code inspection confirms cached animation references, shared assets, bounded particle counts, opt-in lifecycle behavior for the new views, and preserved damage/heal ratios. Authorized gameplay changes are Mark's existing 10s window applying to both its recipe/consuming rule, and the Scales/Chrono circular areas centered on their anchors with radius 2. The updated attack capture confirms round rings and bursts.

Final in-game visual feel, pause/death/scene-transition observation, and crowded-combat profiling remain in the [test plan](../../TestPlans/field-effect-visual-feedback-test-plan.md). No PlayMode run, player build, allocation profile, or measured frame-rate budget is claimed.

Work remains uncommitted on `codex/field-effect-visual-feedback`. Existing user edits, build/dependency settings, and scenes were preserved.

## Curse / Thunder Follow-Up Status

Implemented a procedural curse glyph, faint body seal, and up to 16 local sparks on the marked enemy's existing damage event. Mark removal unsubscribes the view. Existing consumption/Bleed/Claw behavior remains unchanged. Dagger uses an authored lightning prefab with a branched yellow sky bolt, yellow glow/core flash, 32 yellow ground sparks and a circular wave driven by saved radius 3.5. A world strike locks its impact center at spawn. Scales now follows the player collider center for its radius-2 query and expansion. Dagger ground queries ignore triggers and unsupported normals; absent ground preserves the original anchor plus authored offset.

Initial script import, compilation and procedural prefab generation succeeded in Unity. Latest runtime and Editor assemblies also passed a standalone compile using the same Unity Roslyn compiler and response files, with outputs isolated in Temp. New regression cases cover ground resolution, curse damage subscriptions, lightning radius/scale/pause/tail/reuse, and the actual saved Dagger proc. The current runtime immediately evaluates thunder at its five-hit Focus threshold; this existing behavior differs from the old next-hit description and was preserved.

The new [isolated artifact run](curse-thunder-tests.xml) passed **29 tests, 0 failed, 0 skipped** using Unity's main-thread test executor in an empty additive scene. This covers ArtifactAdvancedVisualTests, ArtifactAreaGeometryTests, ArtifactCurseThunderTests and FieldEffectArtifactSequenceTests. It avoids the normal test-job save/reload prompt, which the user cancelled. Test_Lobby was restored active and remained dirty with 18 roots. Do not count the older 84-test XML as validation of this follow-up. Unity reports no errors after this run. Updated captures were inspected for yellow lightning and complete circular footprints; final combat feel and profiling remain human review.

## Latest Scales / Crest Validation

The final [Scales/Crest report](scales-crest-tests.xml) passed **31 tests, 0 failed, 0 skipped** in Unity 6000.3.4f1. It covers the new player-collider center and actual saved Crest trigger, radius, damage interval, following, cooldown and flame cleanup alongside the existing artifact fixtures. Older Chrono/Dagger fixtures now place their actors at their simulated hit coordinates. Unity compiled cleanly; the temporary batch session read the saved lobby, ran in an isolated additive scene, rendered all five-column previews and exited successfully without saving a scene. Enter/loop/attack/exit captures were inspected. Scales' shell and expansion are concentric, and Crest's glyph/dark flame loop is distinct from Chrono. Combat playthrough feel and profiling remain human review.
