---
slug: field-effect-visual-feedback
status: superseded
source: manual
gdd_tags: [combat, items]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Field Effect Visual Feedback — Design Session

Historical design-stage notes. Current implementation and validation are recorded in `2026-10-14-field-effect-visual-feedback.md` and the FieldEffectVisuals report.

## Completed

- Inspected the supplied reference, project stack, feature workflow, GDD combat/items sections, approved Phase-1 Artifact task, ADR-010, field visual recipe presentation, receiver lifecycle, and Artifact authoring code.
- Verified the existing requested Artifact effects and their recipient relationships.
- Created a task card, design specification, and procedural static storyboard with enter/loop/ending poses for power-up, power-down, and Toxic.
- Defined starting visual values with readability and lifecycle test criteria.
- Visually checked the generated storyboard for layout and motif direction.

## Findings

- Field visual slots are available but empty in relevant Phase-1 recipes.
- Current receiver destroys field visuals immediately; ending animation will need presentation lifecycle support after design and architecture approval.
- Affliction is indefinite in current content; its visual must follow effect presence instead of an invented timer.
- “Fiend Flesh Mark” is provisionally mapped to existing Fiend-Flesh Mask; explicitly called out for owner correction.

## Checkpoint

The initial proposal paused at the design checkpoint. The owner then authorized a closer Genshin-inspired chevron treatment and dark flame particles. That revised direction is recorded as approved, with final in-game feel review still pending. The integration plan is now drafted and awaits the required architecture approval under implement-feature Step 4. Unity runtime implementation has not started. No compilation, runtime tests, or in-game visual validation are claimed.

## Owner Revision and Architecture Draft

- Replaced the low-opacity arrows with brighter golden/blue-violet chevrons, soft halos, and short trails.
- Replaced plum wisps with charcoal/violet dark flames using Unity Particle System; defined ignition, sustained emission, natural fade after emission stops, and immediate teardown cleanup.
- Verified Unity's documented `StopEmitting` behavior and inspected the existing project ParticleSystem setup and sorting conventions in `EnemyLifecycleVFX`.
- Updated the procedural storyboard and inspected it visually.
- Drafted `field-effect-visual-feedback-arch-plan.md`, preserving existing effect authority and targeting, adding opt-in visual phases/reuse, and authoring three shared prefabs for the four requested recipes.

## Changes

Only design documents and a storyboard were added. Existing user edits are preserved.
