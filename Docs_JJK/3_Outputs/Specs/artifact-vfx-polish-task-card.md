---
slug: artifact-vfx-polish
type: feature
status: needs-human
source: manual
gdd_tags: [combat, items]
owner: Codex
human_checkpoint: required
workflow: implement-feature
blocked_by: []
---

# Artifact VFX Polish

Owner requests real VFX rather than flat debug-style shapes: a circular, volumetric-looking Scales shield inspired by the attached reference, particle shield attacks/blood explosions, and improved Mark. Scales and Chrono use circular radius 2. The latest owner correction supersedes the attempted ground lift: Scales' hit area and attack expansion now share the player collider center, with zero offset and no ground snap; Chrono remains centered on its target. Urn remains completely hidden. This is an extension of the approved procedural/presentation architecture. No dependencies, build settings, or scene replacement.

GDD @tag:combat / @tag:items: preserve the underlying artifact triggers and readable combat. Authority stays in ParticleBehaviorRuntime and ArtifactPassiveRuntimeSystem; views receive dimensions from those definitions. Scales and Chrono use equal X/Y radii of 2 at their player/target body collider centers. The optional vertical multiplier remains 1 by default. Existing target caps, filters, damage ratios, timers, and heals remain authored.

Design: transparent sphere rim and projected hex facets, front/back orbit arcs and bounded motes; stronger entry and short exit dissolves. Shield attacks use radial shards and a fading area wave. Blood bursts combine a procedural shockwave/core flash with irregular streak particles. Mark uses an animated sigil, halo and ember particles until consumption/10s expiry. Urn gets no world/status VFX.

Starting values: circular hit radius 2 centered on its anchor, shield body aspect 1, shield loop alpha under 0.18, at most 48 attack particles and 12 persistent motes. All animation uses scaled time. Readability test: identify Mark/shield activation/attack in 8/10 combat-zoom demonstrations while keeping silhouettes visible; reduce fill/count if distracting and strengthen rims/shape if unreadable. Validate targets inside the circle and out-of-range diagonal corners. No delayed hits or added gameplay colliders.

Validation: Unity compilation, focused regressions, vertical hit-volume boundary tests, exact VFX-to-area dimensions, pool/expiry cleanup, real rendered phase previews. Final in-game feel and profiler measurement remain human review.

Delivered: rounded sphere/facet shield, animated depth arcs and loop motes, world-space shield shards and shockwave, Mark halo/embers, blood core/streak/wave layers. Both authored hit areas use circular X=2/Y=2 radii at their actor body centers. Urn stays hidden. The earlier run passed 84 focused EditMode checks; later Scales/Crest work and current validation are tracked in artifact-crest-body-feedback-task-card.md. Final in-game feel/profiling remains human review.
