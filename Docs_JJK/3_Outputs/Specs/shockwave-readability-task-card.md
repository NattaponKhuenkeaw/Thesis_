---
slug: shockwave-readability
status: needs-human
source: manual
gdd_tags: [visual-audio, combat]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Shockwave readability

Workflow: `/implement-feature`, presentation tuning within the existing ArtifactAreaVisual architecture.

User requests larger, longer, more noticeable Shockwave effects everywhere they are used. This authorizes the visual tuning; no new architecture is required.

GDD `@tag:visual-audio`: "prioritize readable silhouettes, large hit VFX, strong attack shapes, and simple enemy tells."

## Implementation and starting values

- ScalesHexShield, ChronoBloodExplosion, DaggerThunderStrike, and CrestDarkFlameMark share ArtifactAreaVisual and the Shockwave child sprite.
- Starting values: decorative wave radius 1.5 times the hit radius; expansion and fade each last twice their existing duration. Alpha increases from 0.65 to 0.85, or 0.3 to 0.55 for DarkFlameMark.
- Keep the attack ring, hit geometry, particle motion, damage, and gameplay timing unchanged. One-shot views remain active for their wave tail. Actor teardown and explicit ending still bound sustained visuals.
- Public component settings and saved prefab values allow further tuning. Existing asset generators inherit the same component defaults.

## Experience evaluation and validation

Clarity: retain the radius-accurate attack ring. Satisfaction: strengthen the existing visual channel. Response and motivation: unchanged. Fit: large burst circles match the GDD.

At normal combat zoom, trigger each of the four effects alone and during overlapping combat. Pass if the expanding wave is distinguishable in at least 8/10 triggers while enemy silhouettes and danger zones stay readable. If weak, increase opacity first; if cluttered, reduce scale or fade duration. Confirm death, pause, room exit, repeated pulses, and reuse leave no stale wave.

Added parameterized EditMode regression coverage for all four saved prefabs: decorative dimensions, unchanged hit radii, extended visible tail, finite one-shot cleanup, and reuse. Updated the existing blood-burst lifetime assertion.

Scoped diff whitespace validation passes. Unity tests and combat-view observation have not been run; final visual feel remains human review.

## Owner follow-up: larger waves and Dimension Slash

Owner requested Shockwave "larger it like 150%" and Dimension Slash width and height larger by 50%. Interpreted Shockwave as 150% larger than original: updated the starting scale to 2.5x across all four prefabs and the component default. The doubled wave duration remains.

Dimension Slash's judgment field and final-cut ribbon use the existing overallScale setting: 1.3 -> 1.95 (50% increase to both dimensions). Updated saved action data and authoring defaults, preserving all other in-progress action edits. No damage dimensions or timings changed. Updated regression expectations, including explicit mesh width and height checks.

Use the same combat-zoom readability test for the enlarged waves and both Dimension Slash stages. If effects obscure danger cues, reduce scale. Runtime tests and visual review remain pending.

Latest owner tuning: Shockwave is now 300% larger than original (4x radius/diameter), across all four saved prefabs and the component default. Extended duration and Dimension Slash settings remain as above. Updated dimension regression expectations. Combat-view validation remains pending; use the same readability criteria.

## Explosion footprint correction, 2026-11-10

The user reported that explosion visuals looked several times wider than their hits. The two authored Explosion Splash definitions both use `ChronoBloodExplosion`. Its wave now reaches the damage boundary at 1x radius. Broken Chrono Relic uses 2 world units and Rei's Captured splash uses 3. The change is specific to explosion splashes and retains the extended wave duration. The fallback explosion ring now uses the same horizontal and vertical radii as the damage query.

`Tools/Project/Field Effects/Validate Explosion Footprints` passed 13 isolated EditMode checks, including both saved wave footprints and both damage boundary paths. Live combat visual review remains pending.

The user also flagged Wrath of Demon, Throne Barrier and Lightning Strike. Their saved waves now use the same 1x scale as the explosion splashes. The authored hit radii are 3, 2 and 3.5 world units respectively. Rei's Captured splash uses a yellow tint on a saved variant of the shared explosion prefab. The focused run passed all 13 checks with the new assets.
