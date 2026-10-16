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

# Test Plan: Field Effect Visual Feedback

## Recorded Result

2026-10-15: Unity 6000.3.4f1 compilation succeeded. Focused EditMode run after the radius-2, zero-offset change: **84 passed, 0 failed, 0 skipped**. XML, three original phase previews, and four regenerated advanced previews are recorded in `Docs_JJK/3_Outputs/Reports/FieldEffectVisuals/`. In-game feel, pause/death/transition observation, and crowded-combat profiling remain the human checklist below; these are not represented as completed PlayMode tests.

## Automated Checks

Use Unity menu `Tools > Project > Field Effects > Run Focused Tests`. Results are saved to `Docs_JJK/3_Outputs/Reports/FieldEffectVisuals/tests.xml`.

- Independent recipes sharing a prefab retain one view until the final owner disappears.
- Refresh preserves the loop; reapplication during ending resumes the same view.
- Gameplay expiry removes the effect immediately while starting the visual ending.
- Dark flames stop emission while preserving live particles, then clear within their maximum lifetime; live count is capped.
- Timed power-up and power-down can coexist. Untimed effects have no status entry or sustained field visual.
- Disable/enable presentation cleanup does not remove gameplay effects or change stacks.
- All four saved Artifact recipes map to the correct field style.
- Ribbon applies Toxic to its saved `Allies` targets, including the wearer, infuses outgoing damage with Darkness, and expires at the existing duration.
- Shroud applies power-up to the incoming character on switch-out.
- Shard still applies indefinite Affliction to existing living room enemies on switch-in, with no sustained visual under the owner's revised rule.
- Mask applies power-up to allies after confirmed Particle damage; saved stacking behavior is preserved while further applications retain one loop.
- Player visuals react to inventory changes and swaps without a HUD; switching away clears the old recipient's flame without erasing off-field Toxic, switching back restores it, and expiry starts ending.
- Existing Phase-1 Artifact, damage-type, and death/reset ownership regressions.
- CameraGirl's two random base crit recipes, attack buff, and all three CR/CD upgrades have golden power-up feedback. Concurrent crit buffs keep one loop until final expiry; attack-buff refresh preserves the active loop.
- Urn benefit retains Crit modifiers and is classified as an indefinite Effect.
- Scales shows no attack expansion on an empty pulse; confirmed damage animates the same radius used by gameplay. Natural expiry starts exit and pool reuse restores entry without a stale pulse.
- Chrono applies a 10s Mark, consumes it into the actual-radius blood burst and existing 7s Claw, and ends its glyph on consumption or expiry. An expired shared Mark state cannot be consumed by a rule with duration zero.
- Area animation reaches the exact world X/Y radii under scaled/flipped owners, uses at most 48 bounded blood streaks, clears disabled/destroyed targets, and does not advance when sampled with zero time.
- Rounded shield orbit arcs animate; at most 12 motes loop and stop emitting on exit.
- Circular area geometry includes targets within radius 2 around the anchor, excludes points outside the circle, and handles rotated bodies. Both actual Scales and Chrono sequences damage inside targets and reject diagonal corner targets.

## Unity Visual Preview

Use `Tools > Project > Field Effects > Preview Animations`. The isolated preview uses real player/enemy sprites and shared effect prefabs. The window allows Enter/Loop/Ending playback and scrubbing. It does not save or replace the open scene.

Use `Capture Preview` to create actual Unity-rendered snapshots for all three phases. Top row is player; bottom row is enemy. Columns are Power Up, Power Down, Dark Flame.

Use `Preview Advanced Artifacts` / `Capture Advanced Artifacts` for Scales (left), Chrono Mark/blood (middle), and Claw (right). Scrub enter/loop/attack/exit. The shield loop surrounds the body; only its attack expands to the area radius.

## In-Game Human Review

1. Equip each requested Artifact and trigger its actual sequence. Check the same recipients and durations as the existing gameplay, including off-field teammates and swap-in recipients.
2. On player and enemy, check that arrows point/travel upward for buffs and downward for debuffs. Flames should read as dark fire, with purple rims visible against dark backgrounds.
3. At normal combat camera zoom, the effect should be identifiable while character silhouettes, weapon swings, enemy attack warnings, and hit feedback remain dominant.
4. Pause during all three phases. Visual motion and flame lifetimes should freeze with gameplay, then resume without a burst.
5. Refresh a buff repeatedly up to its existing stack cap. Confirm density remains bounded. Apply different power-up effects simultaneously; expiry of one must not end the remaining one.
6. Rapidly swap between a Toxic recipient and an unaffected character. No flames should follow the unaffected recipient; switching back before Toxic expires should restore the flames.
7. Kill or disable a target while a loop or ending is active. No lingering emission or detached visuals should remain. Re-enable a living target with retained effects and confirm appropriate restoration.
8. Change facing, sprite pose, and character skin; preview a scaled enemy/boss. Travel stays vertical and the effect stays near the body. Check jump, hitstun, and movement without animation/input locks.
9. Clear a room or change scene while effects are active. New enemies that never received Affliction must not display it, and the previous scene must leave no visual instances behind.
10. Stress several enemies with overlapping buffs/debuffs. Inspect the Unity Profiler for allocations and render cost before claiming performance targets.
11. Use CameraGirl's base skill and each crit-buff upgrade, then her authored attack-buff trigger. Observe golden feedback on actual recipients. Where the skill targets teammates only, switch to a buffed teammate before expecting its field visual; concurrent buffs should not multiply arrows.
12. Equip Urn at its active thresholds: Crit stats change but no permanent status icon or field loop appears. Confirm the same for indefinite Shard Affliction.
13. Trigger Scales: verify rounded sphere/hex entry, orbit arcs/motes loop, successful-damage-only particle expansion, and exit at 10s. Move while pulsing; the world-space burst must stay at its anchor and expand to a circular radius of 2. Death, pool reuse, and scene transitions must leave no shield behind.
14. Apply Chrono Mark and wait 10s without consuming: its glyph fades and a later hit cannot explode it. Apply a fresh Mark, consume with an eligible action, and observe the circular blood burst at the hit position/Bleed/7s Claw. Check 8/10 successful identification demonstrations at combat zoom.
15. Confirm Scales' body shield and circular attack start at the player collider center with zero offset and no ground snap. Chrono uses equal X/Y radii of 2 at the target body center; diagonal targets beyond either radius remain unharmed. Urn must stay hidden throughout.

## Starting-Value Acceptance

The timing, size, opacity, emission rate, and particle cap are starting values, not established balance standards. Ask an observer to identify each style and onset/removal in at least 8/10 trials. If clarity fails, tune shape/direction first; if combat is obscured, reduce opacity/footprint/count. Final visual feel and crowded-combat profiling remain human checks.

## Curse / Thunder / Ground Follow-Up

- Ground queries ignore triggers, use the nearest Ground/OneWayPlatform support, and preserve the original center if no ground exists within the authored probe distance. Grounding is opt-in for Dagger only; the latest owner correction centers Scales on the player collider.
- Scales retains its 10s lifetime and 2s pulse interval. Its body bubble, circular radius-2 damage and attack expansion share the player collider center. Cached collider bounds follow movement and character changes, with pivot fallback when no enabled body collider exists. The pulse itself keeps its emitted world center.
- Chrono's curse glyph and faint body seal enter/loop/end with its existing 10s Mark. Confirmed enemy damage flashes the seal and emits up to 16 sparks; consumption keeps the blood burst/Bleed/7s Claw sequence. Ending/disable removes the damage subscription and clears particles. No new Mark damage rule is added.
- Dagger's saved five critical-hit Focus stacks arm thunder. Current runtime immediately evaluates this armed rule at that threshold; this differs from the older next-hit description and remains unchanged by VFX work. The ground query and spark circle share radius 3.5; the existing damage/crit-scaling values, 1s volume lifetime, 1s interval and 2s cooldown remain saved. Its world impact locks when spawned, including on a moving platform.
- Target-triggered artifact area effects use the enemy body collider's bounds center for both the damage query and visual center. Crest follows this center for 5s with saved radius 3 and 2s damage ticks. Its dark flame mark enters, loops with a distinct broken glyph, flashes on confirmed damaging pulses and fades/clears on expiry or target loss.
- Thunder has a procedural branched bolt from above, core flash, 32 bounded sparks and a finite fade. Zero-time sampling preserves the bolt geometry; scaled/flipped parents retain exact world radius; volume expiry does not truncate an active one-shot tail. Empty strikes finish, and pool reuse does not replay a previous bolt.

Human checks: move/jump while Scales pulses and verify the new burst starts concentrically at its body collider without changing timing; test Dagger grounding on slopes/one-way platforms/rooms without support; damage/consume/expire/kill a cursed enemy; trigger Dagger against targets just inside/outside radius 3.5; trigger Crest, move its marked enemy, observe periodic damage and kill/expire it; pause at impact; stress overlapping strikes without hiding enemy attack cues. Existing damage numbers and hit presentation remain the other feedback channel. Audio/camera changes are outside this request.

Preview columns are now Scales, Chrono curse/blood, Claw, Dagger thunder and Rival's Crest. `curse-thunder-impact.png` captures early damage flashes/yellow bolt; `advanced-attack.png` captures expanded radii. The latest isolated Unity main-thread batch run passed **31 tests, 0 failed, 0 skipped**, including the new collider-center and actual Crest sequence checks; results are recorded in `scales-crest-tests.xml`. The earlier 29-test `curse-thunder-tests.xml` and 84-test `tests.xml` predate these additions. All five-column phase captures were refreshed and inspected. No scene was saved by the final batch run. Manual combat feel/profiling checks remain open.
