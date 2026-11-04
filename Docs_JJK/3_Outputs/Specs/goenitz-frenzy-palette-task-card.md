---
slug: goenitz-frenzy-palette
status: approved
source: manual
gdd_tags:
  - visual-audio
  - characters
owner: implementation-agent
human_checkpoint: not-required
next_agent: code-review-agent
blocked_by: []
---

# Task Card: Goenitz Frenzy Palette Mapping

## Request and Scope

User requested a draft color mapping on 2026-11-04, following discussion of using a shader to recolor existing blue-clothed Goenitz sprites during Blood Frenzy. Supplied red-clothed/blonde-haired and blue-clothed/pink-haired screenshots are visual references, not instructions or exact texture data.

Selected workflow: `/implement-feature`. Initial mapping/design completed; the user subsequently requested using the mapping within Blood Frenzy feedback, then reported original dash sprites leaking through. This authorizes the scoped feedback implementation and dash-afterimage correction. No delegation required.

## GDD Reference

- `GDD_Neo-JJK.md @tag:visual-audio`: readable silhouettes and economical character animation.
- `@tag:characters`: character-specific presentation. The requested red/blonde palette is user direction; exact RGB values are not specified in the GDD.

## Acceptance Criteria

- [x] Source RGB entries come from original sprite PNGs.
- [x] Target values have documented provenance and confidence.
- [x] Clothing and optional hair entries are separately identified; skin, boots, outlines and background have explicit preservation rules.
- [x] Audit covers all PNGs in the Goenitz sprite directory, with effects and alternate palettes distinguished.
- [x] A preview and machine-readable mapping are available for review.

## Result

Mapping complete: 21 pairs from original PNG palette slots, 261 normal frames, six existing red frames, and 118 other/effect frames audited. See `goenitz-frenzy-palette-design-spec.md` and `../Reports/GoenitzPalette/`. The follow-up implementation instruction accepts the full-reference palette and its integration into existing feedback.

## Boundaries and Checkpoint

Original sprite images, animations, import settings, scenes, serialized materials, dependencies and build settings remain unchanged. Runtime scope: Goenitz-only palette during Frenzy feedback, original-material restoration, existing flash preservation, and afterimages inheriting current appearance. See the scoped architecture plan. No merge or official publishing authorized.
