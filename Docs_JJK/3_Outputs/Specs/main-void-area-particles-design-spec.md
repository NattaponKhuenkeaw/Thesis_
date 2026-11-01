---
slug: main-void-area-particles
status: needs-human
source: manual
gdd_tags: [visual-audio, combat]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Void Area presentation

## Current reference-driven revision

Latest color feedback: Apricha supplied blue/violet flame references with luminous roots and requested brighter base glow. Shift the lower flame to pale cyan/blue highlights, transition through vivid blue-violet folds, and retain dark purple/black upper smoke. Use a soft additive floor-glow material and world-height-based hot-root color inside the flame shader. Starting values: root glow 1.8, fading over the bottom 55% of the two-unit presentation height; additive floor-glow opacity 0.8. Pass: roots visibly glow while the upper smoke and actor silhouettes remain readable. Reduce root glow first if white highlights spread too far upward. Scene bloom, lighting and post-processing are unchanged.

Apricha rejected the hair-like wisps and supplied two dark-flame references: thick curling black smoke with magenta/violet fire inside it, and a violet energy base with billowing smoke and tiny sparks. This direction supersedes the narrow outlined fragments described below. Use broad, shaded flame folds and dark smoke pockets, soft violet pools at the roots, and sparse filled cool-violet embers. Avoid strand silhouettes, contour-only highlights and a uniform opaque curtain. Retain approximately two-unit height and hitbox-width emission; individual clusters should vary and breathe rather than repeat in a row.

Starting values: 1.65-unit-wide flame puffs, 2.1-unit smoke puffs, flame density 1.15 per unit per second, and 0.065-unit-wide embers. Low-frequency warped noise supplies broad internal shading; a world-space ground mask keeps the puffs above the floor. Pass: visibly broad flame masses and smoky gaps, no leaf/hair outlines, readable actor silhouettes and unchanged horizontal damage coverage. Tune color contrast/opacity down if the highlights dominate, and density up if hazard coverage becomes unclear. Validate in the regenerated combat-scale and animated Unity previews, followed by human in-game review.

The player should see a continuous supernatural hazard spread along the floor: purple flame roots, rolling violet wisps and charcoal smoke, following the supplied visual reference. Use actual Unity ParticleSystem emitters with moving procedural shading rather than an animation sprite. Entry is immediately populated to avoid an invisible active area; exit clears with the authoritative field lifetime.

Starting values revised from Apricha's feedback: visual height 2 units, lower saturation/brightness and opacity in violet flames/ground glow, and wider 1.8-unit charcoal smoke particles layered over the flame. Flame/glow emission is reduced so it reads as spread smoke with dark purple roots. Width is derived from data, not a tuning constant. Verify against the 45-unit authored field and a narrower combat crop. Pass: twice the previous vertical reach, subdued color and continuous dark wisps while character silhouettes remain readable. Tune smoke opacity down first if bodies disappear, then density if field continuity is weak, then motion if it looks static.

Five-component check: Clarity comes from a continuous floor band matching horizontal damage reach; Fit/Satisfaction from purple-black flame and existing action/impact audio; Response and Motivation retain current ability behavior. This is presentation only: existing action state entry, costs, cancel rules, damage and effects remain authoritative. No new player state transitions or audio layer is required.

## Void halo and rising flame fragments

Apricha requested black glow and floating flame pieces, authorizing aesthetic choices. Add a soft near-black halo with a muted violet fringe behind the existing smoke, plus sparse pointed fragments with black cores and dull amethyst edges in front. Fragments rise with a gentle sideways curl, shrink and fade out; they suggest pieces peeling off the field rather than bright sparks. Keep the existing two-unit smoke field and hitbox-width source.

Starting values: halo opacity 0.24 with 2.6-unit puffs; fragments 0.16 units wide, approximately 0.3–0.44 units tall, lifetime up to 2.4 seconds, about 0.7 fragments per unit per second. Inspect the combat crop and motion capture: pass if individual pieces visibly detach and disappear, the soft black fringe reads against the background, and the floor/actors stay readable without returning to the original saturated purple band. Reduce fragment density or rim opacity if distracting; increase rim contrast if pieces disappear against the smoke. Extend the same pool cleanup to both emitters. Verify rise and eventual death through ParticleSystem simulation as well as existing geometry/lifecycle checks.

Review scenarios: infer the field bounds as an observer; cast facing both directions; cast again after expiration; test size/lifetime overrides; pause and resume; clear the room/run; compare character/enemy readability. Existing hitbox has taller damage reach than the requested flame, so this visual communicates horizontal coverage only. Final design-feel approval is pending the human's review of the implementation and captures.
