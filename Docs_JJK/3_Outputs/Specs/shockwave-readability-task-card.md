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
